using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Video.Commands.GenerateVideoToken;

public record GenerateVideoTokenCommand(
    string RoomName,
    bool IsHost = false) : IRequest<Result<VideoTokenDto>>;

public record VideoTokenDto(string Token, string RoomName, int ExpiresInSeconds);

public class GenerateVideoTokenCommandHandler
    : IRequestHandler<GenerateVideoTokenCommand, Result<VideoTokenDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IVideoService _videoService;
    private readonly IProcessHistoryService _history;

    public GenerateVideoTokenCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IVideoService videoService,
        IProcessHistoryService history)
    {
        _context = context;
        _currentUser = currentUser;
        _videoService = videoService;
        _history = history;
    }

    public async Task<Result<VideoTokenDto>> Handle(
        GenerateVideoTokenCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<VideoTokenDto>.Failure("User not authenticated");

        var userId = _currentUser.UserId.Value;

        // Booking status kontrolü — iptal edilen/tamamlanan seanslara token verilmez
        if (Guid.TryParse(request.RoomName, out var parsedBookingId))
        {
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.Id == parsedBookingId, cancellationToken);
            if (booking != null && booking.Status != Domain.Enums.BookingStatus.Confirmed)
                return Result<VideoTokenDto>.Failure(
                    $"Bu seans için video token oluşturulamaz. Seans durumu: {booking.Status}");
        }

        // Get user info
        var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user == null)
            return Result<VideoTokenDto>.Failure("User not found");

        // Generate token
        var tokenResult = await _videoService.GenerateTokenAsync(
            request.RoomName,
            userId,
            user.DisplayName,
            request.IsHost,
            cancellationToken);

        if (!tokenResult.Success)
            return Result<VideoTokenDto>.Failure(tokenResult.ErrorMessage ?? "Failed to generate token");

        // Eğer host (mentor) ise, VideoSession'ı Live olarak işaretle
        if (request.IsHost)
        {
            if (Guid.TryParse(request.RoomName, out var bookingId))
            {
                var session = await _context.VideoSessions
                    .FirstOrDefaultAsync(s => s.RoomName == request.RoomName, cancellationToken);

                if (session == null)
                {
                    session = VideoSession.Create("Booking", bookingId, request.RoomName);
                    _context.VideoSessions.Add(session);
                }

                session.MarkAsLive();
                await _context.SaveChangesAsync(cancellationToken);

                await _history.LogAsync("VideoSession", session.Id, "StatusChanged",
                    "Scheduled", "Live",
                    $"Mentor odaya katıldı, session başlatıldı. Room: {request.RoomName}",
                    userId, "Mentor", ct: cancellationToken);
            }
        }

        // Track participant
        var existingSession = await _context.VideoSessions
            .FirstOrDefaultAsync(s => s.RoomName == request.RoomName, cancellationToken);

        if (existingSession != null)
        {
            var participant = VideoParticipant.Create(existingSession.Id, userId);
            _context.VideoParticipants.Add(participant);
            await _context.SaveChangesAsync(cancellationToken);

            var role = request.IsHost ? "Mentor" : "Student";
            await _history.LogAsync("VideoSession", existingSession.Id, "ParticipantJoined",
                null, null,
                $"{role} ({user.DisplayName}) odaya katıldı. Room: {request.RoomName}",
                userId, role, ct: cancellationToken);
        }

        return Result<VideoTokenDto>.Success(
            new VideoTokenDto(tokenResult.Token!, request.RoomName, 14400));
    }
}