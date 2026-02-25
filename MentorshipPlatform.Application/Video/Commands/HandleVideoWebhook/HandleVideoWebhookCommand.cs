using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Video.Commands.HandleVideoWebhook;

public record HandleVideoWebhookCommand(
    string RoomName,
    string ParticipantIdentity,
    string EventType) : IRequest<Result>;

public record TwilioWebhookDto(
    string RoomName,
    string ParticipantIdentity,
    string StatusCallbackEvent);

public class HandleVideoWebhookCommandHandler : IRequestHandler<HandleVideoWebhookCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<HandleVideoWebhookCommandHandler> _logger;
    private readonly IProcessHistoryService _history;
    private readonly IChatNotificationService _chatNotification;

    public HandleVideoWebhookCommandHandler(
        IApplicationDbContext context,
        ILogger<HandleVideoWebhookCommandHandler> logger,
        IProcessHistoryService history,
        IChatNotificationService chatNotification)
    {
        _context = context;
        _logger = logger;
        _history = history;
        _chatNotification = chatNotification;
    }

    public async Task<Result> Handle(
        HandleVideoWebhookCommand request,
        CancellationToken cancellationToken)
    {
        var session = await _context.VideoSessions
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.RoomName == request.RoomName, cancellationToken);

        if (session == null)
        {
            _logger.LogWarning("Video session not found: {RoomName}", request.RoomName);
            return Result.Success();
        }

        // Identity format: "userId|displayName"
        var identityParts = request.ParticipantIdentity?.Split('|');
        var userIdStr = identityParts?.Length > 0 ? identityParts[0] : request.ParticipantIdentity;

        if (!Guid.TryParse(userIdStr, out var userId))
        {
            _logger.LogWarning("Invalid participant identity: {Identity}", request.ParticipantIdentity);
            return Result.Success();
        }

        switch (request.EventType)
        {
            case "participant-connected":
                var participant = VideoParticipant.Create(session.Id, userId);
                _context.VideoParticipants.Add(participant);

                if (session.Status == VideoSessionStatus.Scheduled)
                {
                    session.MarkAsLive();
                    await _history.LogAsync("VideoSession", session.Id, "StatusChanged",
                        "Scheduled", "Live",
                        $"İlk katılımcı bağlandı, session başlatıldı. Room: {request.RoomName}",
                        userId, "System", ct: cancellationToken);
                }

                await _history.LogAsync("VideoSession", session.Id, "ParticipantConnected",
                    null, null,
                    $"Katılımcı bağlandı (webhook). Identity: {request.ParticipantIdentity}",
                    userId, "System", ct: cancellationToken);
                break;

            case "participant-disconnected":
                var existingParticipant = await _context.VideoParticipants
                    .FirstOrDefaultAsync(p =>
                        p.VideoSessionId == session.Id &&
                        p.UserId == userId &&
                        !p.LeftAt.HasValue,
                        cancellationToken);

                if (existingParticipant != null)
                {
                    existingParticipant.Leave();
                }

                await _history.LogAsync("VideoSession", session.Id, "ParticipantDisconnected",
                    null, null,
                    $"Katılımcı ayrıldı (webhook). Identity: {request.ParticipantIdentity}, Süre: {existingParticipant?.DurationSec ?? 0}sn",
                    userId, "System", ct: cancellationToken);
                break;

            case "room-ended":
                var oldStatus = session.Status.ToString();
                session.MarkAsEnded();

                await _history.LogAsync("VideoSession", session.Id, "StatusChanged",
                    oldStatus, "Ended",
                    $"Oda kapandı (webhook). Room: {request.RoomName}",
                    performedByRole: "System", ct: cancellationToken);
                break;
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Notify relevant users about room status change via SignalR
        await NotifyRoomStatusToRelatedUsers(session, request.RoomName, cancellationToken);

        return Result.Success();
    }

    private async Task NotifyRoomStatusToRelatedUsers(
        VideoSession session, string roomName, CancellationToken cancellationToken)
    {
        try
        {
            var activeParticipantCount = session.Participants.Count(p => !p.LeftAt.HasValue);
            var isActive = session.Status == VideoSessionStatus.Live;

            // Determine mentor & student user IDs based on resource type
            Guid? mentorUserId = null;
            var studentUserIds = new List<Guid>();

            if (session.ResourceType == "Booking")
            {
                var booking = await _context.Bookings
                    .FirstOrDefaultAsync(b => b.Id == session.ResourceId, cancellationToken);
                if (booking != null)
                {
                    mentorUserId = booking.MentorUserId;
                    studentUserIds.Add(booking.StudentUserId);
                }
            }
            else if (session.ResourceType == "GroupClass")
            {
                var groupClass = await _context.GroupClasses
                    .FirstOrDefaultAsync(g => g.Id == session.ResourceId, cancellationToken);
                if (groupClass != null)
                {
                    mentorUserId = groupClass.MentorUserId;
                    // Get all enrolled student user IDs
                    var enrollments = await _context.ClassEnrollments
                        .Where(e => e.ClassId == groupClass.Id &&
                                    e.Status == EnrollmentStatus.Confirmed)
                        .Select(e => e.StudentUserId)
                        .ToListAsync(cancellationToken);
                    studentUserIds.AddRange(enrollments);
                }
            }

            var hostConnected = mentorUserId.HasValue &&
                session.Participants.Any(p => p.UserId == mentorUserId.Value && !p.LeftAt.HasValue);

            // Notify all students about room status
            foreach (var studentId in studentUserIds)
            {
                await _chatNotification.NotifyRoomStatusChanged(
                    studentId, roomName, isActive, hostConnected, activeParticipantCount);
            }

            // Also notify mentor about room status (participant count changes)
            if (mentorUserId.HasValue)
            {
                await _chatNotification.NotifyRoomStatusChanged(
                    mentorUserId.Value, roomName, isActive, hostConnected, activeParticipantCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send room status notification for {RoomName}", roomName);
        }
    }
}