using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Jobs;

/// <summary>
/// Recurring job (daily at 02:00 UTC): Calculates instructor performance summaries.
/// - Daily summary every day (for yesterday)
/// - Weekly summary on Mondays (for last week)
/// - Monthly summary on 1st of month (for previous month)
/// </summary>
public class CalculatePerformanceSummaryJob
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<CalculatePerformanceSummaryJob> _logger;

    public CalculatePerformanceSummaryJob(
        IApplicationDbContext context,
        ILogger<CalculatePerformanceSummaryJob> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Execute()
    {
        try
        {
            // Get all active instructors
            var allUsers = await _context.Users.AsNoTracking().ToListAsync();
            var instructors = allUsers
                .Where(u => u.Roles.Contains(UserRole.Mentor)
                         && u.InstructorStatus == InstructorStatus.Active)
                .ToList();

            if (!instructors.Any())
            {
                _logger.LogInformation("No active instructors found, skipping performance summary calculation.");
                return;
            }

            var now = DateTime.UtcNow;

            // Always calculate Daily summary for yesterday
            var yesterdayStart = now.Date.AddDays(-1);
            var yesterdayEnd = now.Date.AddTicks(-1); // end of yesterday
            await CalculateForPeriod(instructors, PerformancePeriodType.Daily, yesterdayStart, yesterdayEnd);

            // Calculate Weekly summary on Mondays (for last week Mon-Sun)
            if (now.DayOfWeek == DayOfWeek.Monday)
            {
                var weekStart = now.Date.AddDays(-7); // last Monday
                var weekEnd = now.Date.AddTicks(-1);   // end of last Sunday
                await CalculateForPeriod(instructors, PerformancePeriodType.Weekly, weekStart, weekEnd);
            }

            // Calculate Monthly summary on the 1st (for previous month)
            if (now.Day == 1)
            {
                var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1);
                var monthEnd = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(-1);
                await CalculateForPeriod(instructors, PerformancePeriodType.Monthly, monthStart, monthEnd);
            }

            _logger.LogInformation("Performance summary calculation completed for {Count} instructors.", instructors.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CalculatePerformanceSummaryJob");
        }
    }

    private async Task CalculateForPeriod(
        List<User> instructors,
        PerformancePeriodType periodType,
        DateTime periodStart,
        DateTime periodEnd)
    {
        _logger.LogInformation("Calculating {PeriodType} summaries for {Start:yyyy-MM-dd} to {End:yyyy-MM-dd}",
            periodType, periodStart, periodEnd);

        foreach (var instructor in instructors)
        {
            try
            {
                await CalculateInstructorSummary(instructor.Id, periodType, periodStart, periodEnd);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating {PeriodType} summary for instructor {InstructorId}",
                    periodType, instructor.Id);
            }
        }
    }

    private async Task CalculateInstructorSummary(
        Guid instructorId,
        PerformancePeriodType periodType,
        DateTime periodStart,
        DateTime periodEnd)
    {
        // Count completed private lessons (bookings)
        var totalPrivateLessons = await _context.Bookings
            .AsNoTracking()
            .CountAsync(b =>
                b.MentorUserId == instructorId &&
                b.Status == BookingStatus.Completed &&
                b.EndAt >= periodStart && b.EndAt <= periodEnd);

        // Count completed group lessons
        var totalGroupLessons = await _context.GroupClasses
            .AsNoTracking()
            .CountAsync(g =>
                g.MentorUserId == instructorId &&
                g.Status == ClassStatus.Completed &&
                g.StartAt >= periodStart && g.StartAt <= periodEnd);

        // Count video views for instructor's courses
        var totalVideoViews = await _context.VideoWatchLogs
            .AsNoTracking()
            .CountAsync(v =>
                v.InstructorId == instructorId &&
                v.WatchStartedAt >= periodStart && v.WatchStartedAt <= periodEnd);

        // Sum live duration from InstructorSessionLogs (in minutes)
        var sessionLogs = await _context.InstructorSessionLogs
            .AsNoTracking()
            .Where(sl =>
                sl.InstructorId == instructorId &&
                sl.JoinedAt >= periodStart && sl.JoinedAt <= periodEnd &&
                sl.LeftAt.HasValue)
            .ToListAsync();

        var totalLiveDurationMinutes = (int)sessionLogs
            .Sum(sl => (sl.LeftAt!.Value - sl.JoinedAt).TotalMinutes);

        // Sum video watch minutes
        var totalVideoWatchSeconds = await _context.VideoWatchLogs
            .AsNoTracking()
            .Where(v =>
                v.InstructorId == instructorId &&
                v.WatchStartedAt >= periodStart && v.WatchStartedAt <= periodEnd)
            .SumAsync(v => (int?)v.WatchedDurationSeconds) ?? 0;
        var totalVideoWatchMinutes = totalVideoWatchSeconds / 60;

        // Count unique students
        var privateStudents = await _context.Bookings
            .AsNoTracking()
            .Where(b =>
                b.MentorUserId == instructorId &&
                b.Status == BookingStatus.Completed &&
                b.EndAt >= periodStart && b.EndAt <= periodEnd)
            .Select(b => b.StudentUserId)
            .Distinct()
            .ToListAsync();

        var groupStudents = await _context.ClassEnrollments
            .AsNoTracking()
            .Where(e =>
                _context.GroupClasses.Any(g =>
                    g.Id == e.ClassId &&
                    g.MentorUserId == instructorId &&
                    g.Status == ClassStatus.Completed &&
                    g.StartAt >= periodStart && g.StartAt <= periodEnd) &&
                (e.Status == EnrollmentStatus.Confirmed || e.Status == EnrollmentStatus.Attended))
            .Select(e => e.StudentUserId)
            .Distinct()
            .ToListAsync();

        var videoStudents = await _context.VideoWatchLogs
            .AsNoTracking()
            .Where(v =>
                v.InstructorId == instructorId &&
                v.WatchStartedAt >= periodStart && v.WatchStartedAt <= periodEnd)
            .Select(v => v.StudentId)
            .Distinct()
            .ToListAsync();

        var totalStudentsServed = privateStudents
            .Union(groupStudents)
            .Union(videoStudents)
            .Distinct()
            .Count();

        // Revenue from completed bookings (direct payment)
        var totalDirectRevenue = await _context.Orders
            .AsNoTracking()
            .Where(o =>
                o.Status == OrderStatus.Paid &&
                o.Type == OrderType.Booking &&
                o.CreatedAt >= periodStart && o.CreatedAt <= periodEnd &&
                _context.Bookings.Any(b =>
                    b.Id == o.ResourceId &&
                    b.MentorUserId == instructorId))
            .SumAsync(o => (decimal?)o.AmountTotal) ?? 0;

        // Demand rate: completed / (completed + cancelled + noshow) for private lessons
        var totalPrivateRequested = await _context.Bookings
            .AsNoTracking()
            .CountAsync(b =>
                b.MentorUserId == instructorId &&
                b.EndAt >= periodStart && b.EndAt <= periodEnd &&
                (b.Status == BookingStatus.Completed ||
                 b.Status == BookingStatus.Cancelled ||
                 b.Status == BookingStatus.NoShow ||
                 b.Status == BookingStatus.StudentNoShow ||
                 b.Status == BookingStatus.MentorNoShow));

        var privateLessonDemandRate = totalPrivateRequested > 0
            ? (decimal)totalPrivateLessons / totalPrivateRequested * 100
            : 0;

        // Fill rate for group lessons: enrolled / capacity
        var groupClassData = await _context.GroupClasses
            .AsNoTracking()
            .Where(g =>
                g.MentorUserId == instructorId &&
                g.Status == ClassStatus.Completed &&
                g.StartAt >= periodStart && g.StartAt <= periodEnd)
            .Select(g => new { g.Id, g.Capacity })
            .ToListAsync();

        decimal groupLessonFillRate = 0;
        if (groupClassData.Any())
        {
            var classIds = groupClassData.Select(g => g.Id).ToList();
            var totalCapacity = groupClassData.Sum(g => g.Capacity);
            var totalEnrolled = await _context.ClassEnrollments
                .AsNoTracking()
                .CountAsync(e =>
                    classIds.Contains(e.ClassId) &&
                    (e.Status == EnrollmentStatus.Confirmed || e.Status == EnrollmentStatus.Attended));

            groupLessonFillRate = totalCapacity > 0
                ? (decimal)totalEnrolled / totalCapacity * 100
                : 0;
        }

        // Upsert: find existing summary or create new
        var existingSummary = await _context.InstructorPerformanceSummaries
            .FirstOrDefaultAsync(s =>
                s.InstructorId == instructorId &&
                s.PeriodType == periodType &&
                s.PeriodStart == periodStart);

        if (existingSummary != null)
        {
            existingSummary.UpdateMetrics(
                totalPrivateLessons, totalGroupLessons, totalVideoViews,
                totalLiveDurationMinutes, totalVideoWatchMinutes, totalStudentsServed,
                0, totalDirectRevenue, 0,
                privateLessonDemandRate, groupLessonFillRate);
        }
        else
        {
            var summary = InstructorPerformanceSummary.Create(
                instructorId, periodType, periodStart, periodEnd);

            summary.UpdateMetrics(
                totalPrivateLessons, totalGroupLessons, totalVideoViews,
                totalLiveDurationMinutes, totalVideoWatchMinutes, totalStudentsServed,
                0, totalDirectRevenue, 0,
                privateLessonDemandRate, groupLessonFillRate);

            _context.InstructorPerformanceSummaries.Add(summary);
        }

        await _context.SaveChangesAsync();
    }
}
