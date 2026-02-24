using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Jobs;

/// <summary>
/// Recurring job (every 10 minutes): Completes group classes
/// whose EndAt + grace period has passed. This is the "soft"
/// completer — EnforceSessionEndJob handles the Twilio room
/// termination; this job only marks classes as Completed.
/// </summary>
public class AutoCompleteGroupClassesJob
{
    private readonly IApplicationDbContext _context;
    private readonly IPlatformSettingService _settings;
    private readonly ILogger<AutoCompleteGroupClassesJob> _logger;

    public AutoCompleteGroupClassesJob(
        IApplicationDbContext context,
        IPlatformSettingService settings,
        ILogger<AutoCompleteGroupClassesJob> logger)
    {
        _context = context;
        _settings = settings;
        _logger = logger;
    }

    public async Task Execute()
    {
        var gracePeriod = await _settings.GetIntAsync(
            PlatformSettings.GroupClassGracePeriodMinutes, 15);
        var cutoff = DateTime.UtcNow.AddMinutes(-gracePeriod);

        // Find published classes whose end time + grace period has passed
        var expiredClasses = await _context.GroupClasses
            .Include(c => c.Enrollments)
            .Where(c => c.Status == ClassStatus.Published && c.EndAt < cutoff)
            .ToListAsync();

        if (expiredClasses.Count == 0) return;

        foreach (var groupClass in expiredClasses)
        {
            // Mark class as completed
            groupClass.Complete();

            // Mark confirmed enrollments as attended
            foreach (var enrollment in groupClass.Enrollments)
            {
                if (enrollment.Status == EnrollmentStatus.Confirmed)
                {
                    enrollment.MarkAttended();
                }
            }

            _logger.LogInformation(
                "Auto-completed group class: {ClassId} '{Title}' (EndAt: {EndAt}, GracePeriod: {Grace}dk)",
                groupClass.Id, groupClass.Title, groupClass.EndAt, gracePeriod);
        }

        await _context.SaveChangesAsync(CancellationToken.None);

        _logger.LogInformation("Auto-completed {Count} expired group classes", expiredClasses.Count);
    }
}
