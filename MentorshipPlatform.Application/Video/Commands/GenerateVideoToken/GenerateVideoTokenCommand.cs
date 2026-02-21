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

        // Helper: find session by RoomName with fallback for group classes
        async Task<VideoSession?> findSessionByRoomName(string roomName)
        {
            var s = await _context.VideoSessions
                .FirstOrDefaultAsync(vs => vs.RoomName == roomName, cancellationToken);
            if (s != null) return s;

            // Fallback: try to find by ResourceId from room name (e.g., group-class-{guid})
            if (roomName.StartsWith("group-class-"))
            {
                var idPart = roomName.Replace("group-class-", "");
                if (Guid.TryParse(idPart, out var classId))
                {
                    s = await _context.VideoSessions
                        .FirstOrDefaultAsync(vs => vs.ResourceId == classId &&
                                                    vs.Status != Domain.Enums.VideoSessionStatus.Ended, cancellationToken);
                }
            }
            return s;
        }

        // Eğer host (mentor) ise, VideoSession'ı Live olarak işaretle
        if (request.IsHost)
        {
            var session = await findSessionByRoomName(request.RoomName);

            if (session != null)
            {
                session.MarkAsLive();
                await _context.SaveChangesAsync(cancellationToken);

                await _history.LogAsync("VideoSession", session.Id, "StatusChanged",
                    "Scheduled", "Live",
                    $"Mentor odaya katıldı, session başlatıldı. Room: {request.RoomName}",
                    userId, "Mentor", ct: cancellationToken);
            }
            else if (Guid.TryParse(request.RoomName, out var bookingId))
            {
                // Legacy fallback for booking rooms where session was not pre-created
                session = VideoSession.Create("Booking", bookingId, request.RoomName);
                _context.VideoSessions.Add(session);
                session.MarkAsLive();
                await _context.SaveChangesAsync(cancellationToken);

                await _history.LogAsync("VideoSession", session.Id, "StatusChanged",
                    "Scheduled", "Live",
                    $"Mentor odaya katıldı, session başlatıldı. Room: {request.RoomName}",
                    userId, "Mentor", ct: cancellationToken);
            }
        }

        // Track participant
        var existingSession = await findSessionByRoomName(request.RoomName);

        if (existingSession != null)
        {
            // Close any open (active) segment for this user in this session before creating a new one.
            // This prevents duplicate open segments when user refreshes the page or reconnects.
            var openSegment = await _context.VideoParticipants
                .FirstOrDefaultAsync(p =>
                    p.VideoSessionId == existingSession.Id &&
                    p.UserId == userId &&
                    !p.LeftAt.HasValue,
                    cancellationToken);

            if (openSegment != null)
            {
                openSegment.Leave();
            }

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