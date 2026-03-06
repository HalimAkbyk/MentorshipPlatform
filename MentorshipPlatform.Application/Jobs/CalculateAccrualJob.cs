using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Jobs;

/// <summary>
/// Recurring job (monthly on the 1st at 04:00 UTC): Calculates instructor accruals for the previous month.
/// For each active instructor:
/// - Gets applicable AccrualParameter (instructor-specific first, then global)
/// - Counts private lessons, group lessons, and video views for the previous month
/// - Calculates: count * unitPrice for each type
/// - Applies bonus if threshold met
/// - Creates InstructorAccrual with Status = Draft
/// </summary>
public class CalculateAccrualJob
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<CalculateAccrualJob> _logger;

    public CalculateAccrualJob(
        IApplicationDbContext context,
        ILogger<CalculateAccrualJob> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Execute()
    {
        try
        {
            var now = DateTime.UtcNow;

            // Calculate for previous month
            var periodStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1);
            var periodEnd = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(-1);

            _logger.LogInformation("Calculating accruals for period {Start:yyyy-MM-dd} to {End:yyyy-MM-dd}",
                periodStart, periodEnd);

            // Get all active instructors
            var allUsers = await _context.Users.AsNoTracking().ToListAsync();
            var instructors = allUsers
                .Where(u => u.Roles.Contains(UserRole.Mentor)
                         && u.InstructorStatus == InstructorStatus.Active)
                .ToList();

            if (!instructors.Any())
            {
                _logger.LogInformation("No active instructors found, skipping accrual calculation.");
                return;
            }

            // Load all active accrual parameters
            var allParameters = await _context.InstructorAccrualParameters
                .AsNoTracking()
                .Where(p => p.IsActive)
                .ToListAsync();

            var globalParam = allParameters.FirstOrDefault(p => p.InstructorId == null);

            foreach (var instructor in instructors)
            {
                try
                {
                    await CalculateInstructorAccrual(
                        instructor.Id, periodStart, periodEnd, allParameters, globalParam);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calculating accrual for instructor {InstructorId}", instructor.Id);
                }
            }

            _logger.LogInformation("Accrual calculation completed for {Count} instructors.", instructors.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CalculateAccrualJob");
        }
    }

    private async Task CalculateInstructorAccrual(
        Guid instructorId,
        DateTime periodStart,
        DateTime periodEnd,
        List<InstructorAccrualParameter> allParameters,
        InstructorAccrualParameter? globalParam)
    {
        // Check if accrual already exists for this period
        var existingAccrual = await _context.InstructorAccruals
            .AsNoTracking()
            .AnyAsync(a =>
                a.InstructorId == instructorId &&
                a.PeriodStart == periodStart &&
                a.PeriodEnd == periodEnd);

        if (existingAccrual)
        {
            _logger.LogInformation("Accrual already exists for instructor {InstructorId}, period {Start:yyyy-MM-dd}. Skipping.",
                instructorId, periodStart);
            return;
        }

        // Get applicable parameter: instructor-specific first, then global
        var param = allParameters.FirstOrDefault(p => p.InstructorId == instructorId) ?? globalParam;

        if (param == null)
        {
            _logger.LogWarning("No accrual parameter found for instructor {InstructorId} and no global parameter exists. Skipping.",
                instructorId);
            return;
        }

        // Count completed private lessons
        var privateLessonCount = await _context.Bookings
            .AsNoTracking()
            .CountAsync(b =>
                b.MentorUserId == instructorId &&
                b.Status == BookingStatus.Completed &&
                b.EndAt >= periodStart && b.EndAt <= periodEnd);

        // Count completed group lessons
        var groupLessonCount = await _context.GroupClasses
            .AsNoTracking()
            .CountAsync(g =>
                g.MentorUserId == instructorId &&
                g.Status == ClassStatus.Completed &&
                g.StartAt >= periodStart && g.StartAt <= periodEnd);

        // Count video views (distinct lectures viewed by any student)
        var videoContentCount = await _context.VideoWatchLogs
            .AsNoTracking()
            .Where(v =>
                v.InstructorId == instructorId &&
                v.WatchStartedAt >= periodStart && v.WatchStartedAt <= periodEnd &&
                v.IsCompleted)
            .Select(v => v.LectureId)
            .Distinct()
            .CountAsync();

        // Calculate bonus
        var totalLessons = privateLessonCount + groupLessonCount;
        decimal bonusAmount = 0;
        string? bonusDescription = null;

        if (param.BonusThresholdLessons.HasValue &&
            param.BonusPercentage.HasValue &&
            totalLessons >= param.BonusThresholdLessons.Value)
        {
            var baseAmount = (privateLessonCount * param.PrivateLessonRate)
                           + (groupLessonCount * param.GroupLessonRate)
                           + (videoContentCount * param.VideoContentRate);
            bonusAmount = baseAmount * (param.BonusPercentage.Value / 100m);
            bonusDescription = $"Bonus: {totalLessons} ders >= {param.BonusThresholdLessons.Value} esik, %{param.BonusPercentage.Value}";
        }

        // Create accrual
        var accrual = InstructorAccrual.Create(
            instructorId: instructorId,
            periodStart: periodStart,
            periodEnd: periodEnd,
            privateLessonCount: privateLessonCount,
            privateLessonUnitPrice: param.PrivateLessonRate,
            groupLessonCount: groupLessonCount,
            groupLessonUnitPrice: param.GroupLessonRate,
            videoContentCount: videoContentCount,
            videoUnitPrice: param.VideoContentRate,
            bonusAmount: bonusAmount,
            bonusDescription: bonusDescription);

        _context.InstructorAccruals.Add(accrual);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Accrual created for instructor {InstructorId}: Private={Private}, Group={Group}, Video={Video}, Bonus={Bonus}, Total={Total}",
            instructorId, privateLessonCount, groupLessonCount, videoContentCount, bonusAmount, accrual.TotalAccrual);
    }
}
