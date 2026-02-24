using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Jobs;

/// <summary>
/// Recurring job (every 15 minutes): Cleans up stale video sessions
/// that are still marked as Live but whose associated booking/group class
/// has long since ended (EndAt + grace period exceeded).
/// Unlike EnforceSessionEndJob, this does NOT call Twilio — it only
/// cleans up the DB records for sessions that somehow missed the enforcer.
/// </summary>
public class CleanupStaleSessionsJob
{
    private readonly IApplicationDbContext _context;
    private readonly IPlatformSettingService _settings;
    private readonly IProcessHistoryService _history;
    private readonly ILogger<CleanupStaleSessionsJob> _logger;
    private const int FALLBACK_GRACE_MINUTES = 30;

    public CleanupStaleSessionsJob(
        IApplicationDbContext context,
        IPlatformSettingService settings,
        IProcessHistoryService history,
        ILogger<CleanupStaleSessionsJob> logger)
    {
        _context = context;
        _settings = settings;
        _history = history;
        _logger = logger;
    }

    public async Task Execute()
    {
        try
        {
            await CleanupBookingSessions();
            await CleanupGroupClassSessions();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in CleanupStaleSessionsJob");
        }
    }

    private async Task CleanupBookingSessions()
    {
        var gracePeriod = await _settings.GetIntAsync(
            PlatformSettings.SessionGracePeriodMinutes, FALLBACK_GRACE_MINUTES);

        // Find live sessions for bookings
        var staleSessions = await _context.VideoSessions
            .Include(s => s.Participants)
            .Where(s => s.Status == VideoSessionStatus.Live && s.ResourceType == "Booking")
            .ToListAsync();

        if (!staleSessions.Any()) return;

        foreach (var session in staleSessions)
        {
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.Id == session.ResourceId);

            if (booking == null) continue;

            // Check if booking end time + grace period has passed
            if (booking.EndAt.AddMinutes(gracePeriod) > DateTime.UtcNow)
                continue; // Still within grace period

            _logger.LogWarning("⏰ Stale booking session detected: {RoomName}, booking ended at {EndAt}",
                session.RoomName, booking.EndAt);

            session.MarkAsEnded();

            // Leave all active participants
            foreach (var participant in session.Participants.Where(p => !p.LeftAt.HasValue))
            {
                participant.Leave();
            }

            await _context.SaveChangesAsync();

            await _history.LogAsync("VideoSession", session.Id, "StatusChanged",
                "Live", "Ended",
                $"Session zaman aşımına uğradı. Randevu bitiş: {booking.EndAt:yyyy-MM-dd HH:mm}. Grace period ({gracePeriod}dk) sonrası otomatik sonlandırıldı.",
                performedByRole: "System");
        }
    }

    private async Task CleanupGroupClassSessions()
    {
        var gracePeriod = await _settings.GetIntAsync(
            PlatformSettings.GroupClassGracePeriodMinutes, FALLBACK_GRACE_MINUTES);

        // Find live sessions for group classes
        var staleSessions = await _context.VideoSessions
            .Include(s => s.Participants)
            .Where(s => s.Status == VideoSessionStatus.Live && s.ResourceType == "GroupClass")
            .ToListAsync();

        if (!staleSessions.Any()) return;

        foreach (var session in staleSessions)
        {
            var groupClass = await _context.GroupClasses
                .FirstOrDefaultAsync(c => c.Id == session.ResourceId);

            if (groupClass == null) continue;

            // Check if class end time + grace period has passed
            if (groupClass.EndAt.AddMinutes(gracePeriod) > DateTime.UtcNow)
                continue;

            _logger.LogWarning("⏰ Stale group class session detected: {RoomName}, class ended at {EndAt}",
                session.RoomName, groupClass.EndAt);

            session.MarkAsEnded();

            foreach (var participant in session.Participants.Where(p => !p.LeftAt.HasValue))
            {
                participant.Leave();
            }

            await _context.SaveChangesAsync();

            await _history.LogAsync("VideoSession", session.Id, "StatusChanged",
                "Live", "Ended",
                $"Session zaman aşımına uğradı. Grup dersi bitiş: {groupClass.EndAt:yyyy-MM-dd HH:mm}. Grace period ({gracePeriod}dk) sonrası otomatik sonlandırıldı.",
                performedByRole: "System");
        }
    }
}
