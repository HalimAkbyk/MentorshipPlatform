using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Jobs;

public class AutoCompleteGroupClassesJob
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<AutoCompleteGroupClassesJob> _logger;

    public AutoCompleteGroupClassesJob(
        IApplicationDbContext context,
        ILogger<AutoCompleteGroupClassesJob> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Execute()
    {
        var now = DateTime.UtcNow;

        // Find published classes whose end time has passed
        var expiredClasses = await _context.GroupClasses
            .Include(c => c.Enrollments)
            .Where(c => c.Status == ClassStatus.Published && c.EndAt < now)
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
                "Auto-completed group class: {ClassId} '{Title}' (EndAt: {EndAt})",
                groupClass.Id, groupClass.Title, groupClass.EndAt);
        }

        await _context.SaveChangesAsync(CancellationToken.None);

        _logger.LogInformation("Auto-completed {Count} expired group classes", expiredClasses.Count);
    }
}
