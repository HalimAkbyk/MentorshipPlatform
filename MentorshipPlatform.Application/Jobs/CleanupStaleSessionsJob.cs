using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Jobs;

public class CleanupStaleSessionsJob
{
    private readonly IApplicationDbContext _context;
    private readonly IProcessHistoryService _history;
    private readonly ILogger<CleanupStaleSessionsJob> _logger;
    private const int STALE_AFTER_MINUTES = 30;

    public CleanupStaleSessionsJob(
        IApplicationDbContext context,
        IProcessHistoryService history,
        ILogger<CleanupStaleSessionsJob> logger)
    {
        _context = context;
        _history = history;
        _logger = logger;
    }

    public async Task Execute()
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-STALE_AFTER_MINUTES);

            // Find live sessions whose related booking has ended + grace period
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
                if (booking.EndAt.AddMinutes(STALE_AFTER_MINUTES) > DateTime.UtcNow)
                    continue; // Still within grace period

                _logger.LogWarning("⏰ Stale session detected: {RoomName}, booking ended at {EndAt}",
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
                    $"Session zaman aşımına uğradı. Randevu bitiş: {booking.EndAt:yyyy-MM-dd HH:mm}. Otomatik sonlandırıldı.",
                    performedByRole: "System");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in CleanupStaleSessionsJob");
        }
    }
}
