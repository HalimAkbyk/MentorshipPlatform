using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Video.Commands.LeaveRoom;

public record LeaveRoomCommand(string RoomName) : IRequest<Result<LeaveRoomResult>>;

public record LeaveRoomResult(bool IsRoomEmpty, bool SessionEnded);

public class LeaveRoomCommandHandler : IRequestHandler<LeaveRoomCommand, Result<LeaveRoomResult>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IProcessHistoryService _history;
    private readonly IVideoService _videoService;

    public LeaveRoomCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IProcessHistoryService history,
        IVideoService videoService)
    {
        _context = context;
        _currentUser = currentUser;
        _history = history;
        _videoService = videoService;
    }

    public async Task<Result<LeaveRoomResult>> Handle(
        LeaveRoomCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<LeaveRoomResult>.Failure("User not authenticated");

        var userId = _currentUser.UserId.Value;

        // Find active session by room name
        var session = await _context.VideoSessions
            .Include(s => s.Participants)
            .Where(s => s.RoomName == request.RoomName && s.Status != VideoSessionStatus.Ended)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        // Fallback: group-class room name pattern
        if (session == null && request.RoomName.StartsWith("group-class-"))
        {
            var idPart = request.RoomName.Replace("group-class-", "");
            if (Guid.TryParse(idPart, out var classId))
            {
                session = await _context.VideoSessions
                    .Include(s => s.Participants)
                    .Where(s => s.ResourceId == classId && s.Status != VideoSessionStatus.Ended)
                    .OrderByDescending(s => s.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);
            }
        }

        if (session == null)
            return Result<LeaveRoomResult>.Success(new LeaveRoomResult(true, false));

        // Mark this user's active participant segment as left
        var openSegment = await _context.VideoParticipants
            .FirstOrDefaultAsync(p =>
                p.VideoSessionId == session.Id &&
                p.UserId == userId &&
                !p.LeftAt.HasValue,
                cancellationToken);

        if (openSegment != null)
        {
            openSegment.Leave();
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Determine user role
        bool isHost = false;
        if (session.ResourceType == "Booking" && Guid.TryParse(request.RoomName, out var bookingId))
        {
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);
            if (booking != null) isHost = booking.MentorUserId == userId;
        }
        else if (session.ResourceType == "GroupClass")
        {
            var groupClass = await _context.GroupClasses
                .FirstOrDefaultAsync(g => g.Id == session.ResourceId, cancellationToken);
            if (groupClass != null) isHost = groupClass.MentorUserId == userId;
        }

        var role = isHost ? "Mentor" : "Student";

        await _history.LogAsync("VideoSession", session.Id, "ParticipantLeft",
            null, null,
            $"{role} odadan ayrıldı (leave). Room: {request.RoomName}",
            userId, role, ct: cancellationToken);

        // Check if room is now empty (no active participants left)
        var activeParticipantCount = await _context.VideoParticipants
            .CountAsync(p =>
                p.VideoSessionId == session.Id &&
                !p.LeftAt.HasValue,
                cancellationToken);

        if (activeParticipantCount == 0)
        {
            // Room is empty — end the session + close Twilio room
            var oldStatus = session.Status.ToString();
            session.MarkAsEnded();

            // Mark any remaining participants as left (safety)
            var remainingParticipants = await _context.VideoParticipants
                .Where(p => p.VideoSessionId == session.Id && !p.LeftAt.HasValue)
                .ToListAsync(cancellationToken);
            foreach (var p in remainingParticipants)
                p.Leave();

            await _context.SaveChangesAsync(cancellationToken);

            // Close Twilio room
            try { await _videoService.CompleteRoomAsync(request.RoomName, cancellationToken); } catch { }

            await _history.LogAsync("VideoSession", session.Id, "StatusChanged",
                oldStatus, "Ended",
                $"Oda boşaldı, session otomatik sonlandırıldı. Room: {request.RoomName}",
                userId, "System", ct: cancellationToken);

            return Result<LeaveRoomResult>.Success(new LeaveRoomResult(true, true));
        }

        return Result<LeaveRoomResult>.Success(new LeaveRoomResult(false, false));
    }
}
