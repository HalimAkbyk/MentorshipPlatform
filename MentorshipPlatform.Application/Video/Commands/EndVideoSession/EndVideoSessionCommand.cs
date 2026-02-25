using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Video.Commands.EndVideoSession;

public record EndVideoSessionCommand(string RoomName) : IRequest<Result>;

public class EndVideoSessionCommandHandler : IRequestHandler<EndVideoSessionCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IProcessHistoryService _history;
    private readonly IVideoService _videoService;
    private readonly IChatNotificationService _chatNotification;

    public EndVideoSessionCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IProcessHistoryService history,
        IVideoService videoService,
        IChatNotificationService chatNotification)
    {
        _context = context;
        _currentUser = currentUser;
        _history = history;
        _videoService = videoService;
        _chatNotification = chatNotification;
    }

    public async Task<Result> Handle(
        EndVideoSessionCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        // Search by room name first, then try fallback for group classes
        var session = await _context.VideoSessions
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.RoomName == request.RoomName &&
                                      s.Status != VideoSessionStatus.Ended, cancellationToken);

        // Fallback: try to find by extracting ResourceId from room name (e.g., group-class-{guid})
        if (session == null && request.RoomName.StartsWith("group-class-"))
        {
            var idPart = request.RoomName.Replace("group-class-", "");
            if (Guid.TryParse(idPart, out var classId))
            {
                session = await _context.VideoSessions
                    .Include(s => s.Participants)
                    .FirstOrDefaultAsync(s => s.ResourceId == classId &&
                                              s.Status != VideoSessionStatus.Ended, cancellationToken);
            }
        }

        if (session == null)
            return Result.Failure("Session not found");

        var oldStatus = session.Status.ToString();

        // Mark session as ended in DB
        session.MarkAsEnded();

        // Mark all active participants as left
        var activeParticipants = await _context.VideoParticipants
            .Where(p => p.VideoSessionId == session.Id && !p.LeftAt.HasValue)
            .ToListAsync(cancellationToken);

        foreach (var participant in activeParticipants)
        {
            participant.Leave();
        }

        await _context.SaveChangesAsync(cancellationToken);

        // ──── Twilio room'u da kapat (stale room önleme) ────
        try
        {
            await _videoService.CompleteRoomAsync(request.RoomName, cancellationToken);
        }
        catch
        {
            // Twilio kapanmazsa bile DB session'ı kapalı — kritik değil
        }

        await _history.LogAsync("VideoSession", session.Id, "StatusChanged",
            oldStatus, "Ended",
            $"Session sonlandırıldı. Room: {request.RoomName}, {activeParticipants.Count} aktif katılımcı çıkartıldı, Twilio room kapatıldı",
            _currentUser.UserId, "Mentor", ct: cancellationToken);

        // Notify related users that room has ended via SignalR
        await NotifyRoomEnded(session, request.RoomName, cancellationToken);

        return Result.Success();
    }

    private async Task NotifyRoomEnded(
        Domain.Entities.VideoSession session, string roomName, CancellationToken cancellationToken)
    {
        try
        {
            var userIdsToNotify = new List<Guid>();

            if (session.ResourceType == "Booking")
            {
                var booking = await _context.Bookings
                    .FirstOrDefaultAsync(b => b.Id == session.ResourceId, cancellationToken);
                if (booking != null)
                {
                    userIdsToNotify.Add(booking.StudentUserId);
                    userIdsToNotify.Add(booking.MentorUserId);
                }
            }
            else if (session.ResourceType == "GroupClass")
            {
                var groupClass = await _context.GroupClasses
                    .FirstOrDefaultAsync(g => g.Id == session.ResourceId, cancellationToken);
                if (groupClass != null)
                {
                    userIdsToNotify.Add(groupClass.MentorUserId);
                    var enrolledStudents = await _context.ClassEnrollments
                        .Where(e => e.ClassId == groupClass.Id && e.Status == EnrollmentStatus.Confirmed)
                        .Select(e => e.StudentUserId)
                        .ToListAsync(cancellationToken);
                    userIdsToNotify.AddRange(enrolledStudents);
                }
            }

            foreach (var userId in userIdsToNotify)
            {
                await _chatNotification.NotifyRoomStatusChanged(
                    userId, roomName, isActive: false, hostConnected: false, participantCount: 0);
            }
        }
        catch
        {
            // Non-critical — room ended notification failure shouldn't affect the response
        }
    }
}
