using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Jobs;

/// <summary>
/// Recurring job (every 2 minutes): Forcibly terminates video sessions
/// and completes bookings/group classes after EndAt + grace period.
/// This is the authoritative backend enforcer — even if the frontend
/// fails to disconnect, this job closes the Twilio room.
/// </summary>
public class EnforceSessionEndJob
{
    private readonly IApplicationDbContext _context;
    private readonly IVideoService _videoService;
    private readonly IPlatformSettingService _settings;
    private readonly IProcessHistoryService _history;
    private readonly ILogger<EnforceSessionEndJob> _logger;

    public EnforceSessionEndJob(
        IApplicationDbContext context,
        IVideoService videoService,
        IPlatformSettingService settings,
        IProcessHistoryService history,
        ILogger<EnforceSessionEndJob> logger)
    {
        _context = context;
        _videoService = videoService;
        _settings = settings;
        _history = history;
        _logger = logger;
    }

    public async Task Execute()
    {
        try
        {
            // DevMode bypass — skip enforcement
            var devMode = await _settings.GetBoolAsync(PlatformSettings.DevModeSessionBypass, false);
            if (devMode) return;

            await EnforceBookingSessions();
            await EnforceGroupClassSessions();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in EnforceSessionEndJob");
        }
    }

    private async Task EnforceBookingSessions()
    {
        var gracePeriod = await _settings.GetIntAsync(PlatformSettings.SessionGracePeriodMinutes, 10);
        var cutoff = DateTime.UtcNow.AddMinutes(-gracePeriod);

        // Find confirmed bookings whose EndAt + grace period has passed
        var overdueBookings = await _context.Bookings
            .Where(b => b.Status == BookingStatus.Confirmed && b.EndAt < cutoff)
            .ToListAsync();

        foreach (var booking in overdueBookings)
        {
            try
            {
                // Find any Live video session for this booking
                var session = await _context.VideoSessions
                    .Include(s => s.Participants)
                    .FirstOrDefaultAsync(s =>
                        s.ResourceType == "Booking" &&
                        s.ResourceId == booking.Id &&
                        s.Status == VideoSessionStatus.Live);

                // Forcibly close the Twilio room
                if (session != null)
                {
                    await _videoService.CompleteRoomAsync(session.RoomName);
                    session.MarkAsEnded();

                    // Mark all active participants as left
                    foreach (var p in session.Participants.Where(p => !p.LeftAt.HasValue))
                    {
                        p.Leave();
                    }

                    _logger.LogInformation("⏰ Forcibly ended video session for booking {BookingId}", booking.Id);
                }

                // Complete the booking
                booking.Complete();
                await _context.SaveChangesAsync();

                await _history.LogAsync("Booking", booking.Id, "StatusChanged",
                    "Confirmed", "Completed",
                    $"Grace period ({gracePeriod}dk) doldu. Seans otomatik tamamlandı.",
                    performedByRole: "System");

                _logger.LogInformation("⏰ Auto-completed booking {BookingId} after grace period", booking.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error enforcing session end for booking {BookingId}", booking.Id);
            }
        }
    }

    private async Task EnforceGroupClassSessions()
    {
        var gracePeriod = await _settings.GetIntAsync(PlatformSettings.GroupClassGracePeriodMinutes, 15);
        var cutoff = DateTime.UtcNow.AddMinutes(-gracePeriod);

        // Find published group classes whose EndAt + grace period has passed
        var overdueClasses = await _context.GroupClasses
            .Include(c => c.Enrollments)
            .Where(c => c.Status == ClassStatus.Published && c.EndAt < cutoff)
            .ToListAsync();

        foreach (var groupClass in overdueClasses)
        {
            try
            {
                var roomName = $"group-class-{groupClass.Id}";

                // Find any Live video session
                var session = await _context.VideoSessions
                    .Include(s => s.Participants)
                    .FirstOrDefaultAsync(s =>
                        s.ResourceType == "GroupClass" &&
                        s.ResourceId == groupClass.Id &&
                        s.Status == VideoSessionStatus.Live);

                // Forcibly close the Twilio room
                if (session != null)
                {
                    await _videoService.CompleteRoomAsync(session.RoomName);
                    session.MarkAsEnded();

                    foreach (var p in session.Participants.Where(p => !p.LeftAt.HasValue))
                    {
                        p.Leave();
                    }

                    _logger.LogInformation("⏰ Forcibly ended video session for group class {ClassId}", groupClass.Id);
                }

                // Complete the class
                groupClass.Complete();
                foreach (var enrollment in groupClass.Enrollments)
                {
                    if (enrollment.Status == EnrollmentStatus.Confirmed)
                    {
                        enrollment.MarkAttended();
                    }
                }

                await _context.SaveChangesAsync();

                await _history.LogAsync("GroupClass", groupClass.Id, "StatusChanged",
                    "Published", "Completed",
                    $"Grace period ({gracePeriod}dk) doldu. Grup dersi otomatik tamamlandı.",
                    performedByRole: "System");

                _logger.LogInformation("⏰ Auto-completed group class {ClassId} after grace period", groupClass.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error enforcing session end for group class {ClassId}", groupClass.Id);
            }
        }
    }
}
