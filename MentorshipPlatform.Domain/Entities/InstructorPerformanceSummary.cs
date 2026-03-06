using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Domain.Entities;

public class InstructorPerformanceSummary : BaseEntity
{
    public Guid InstructorId { get; private set; }
    public PerformancePeriodType PeriodType { get; private set; }
    public DateTime PeriodStart { get; private set; }
    public DateTime PeriodEnd { get; private set; }
    public int TotalPrivateLessons { get; private set; }
    public int TotalGroupLessons { get; private set; }
    public int TotalVideoViews { get; private set; }
    public int TotalLiveDurationMinutes { get; private set; }
    public int TotalVideoWatchMinutes { get; private set; }
    public int TotalStudentsServed { get; private set; }
    public int TotalCreditsConsumed { get; private set; }
    public decimal TotalDirectRevenue { get; private set; }
    public decimal TotalCreditRevenue { get; private set; }
    public decimal PrivateLessonDemandRate { get; private set; }
    public decimal GroupLessonFillRate { get; private set; }
    public DateTime CalculatedAt { get; private set; }

    public User Instructor { get; private set; } = null!;

    private InstructorPerformanceSummary() { }

    public static InstructorPerformanceSummary Create(
        Guid instructorId,
        PerformancePeriodType periodType,
        DateTime periodStart,
        DateTime periodEnd)
    {
        return new InstructorPerformanceSummary
        {
            InstructorId = instructorId,
            PeriodType = periodType,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            CalculatedAt = DateTime.UtcNow
        };
    }

    public void UpdateMetrics(
        int totalPrivateLessons,
        int totalGroupLessons,
        int totalVideoViews,
        int totalLiveDurationMinutes,
        int totalVideoWatchMinutes,
        int totalStudentsServed,
        int totalCreditsConsumed,
        decimal totalDirectRevenue,
        decimal totalCreditRevenue,
        decimal privateLessonDemandRate,
        decimal groupLessonFillRate)
    {
        TotalPrivateLessons = totalPrivateLessons;
        TotalGroupLessons = totalGroupLessons;
        TotalVideoViews = totalVideoViews;
        TotalLiveDurationMinutes = totalLiveDurationMinutes;
        TotalVideoWatchMinutes = totalVideoWatchMinutes;
        TotalStudentsServed = totalStudentsServed;
        TotalCreditsConsumed = totalCreditsConsumed;
        TotalDirectRevenue = totalDirectRevenue;
        TotalCreditRevenue = totalCreditRevenue;
        PrivateLessonDemandRate = privateLessonDemandRate;
        GroupLessonFillRate = groupLessonFillRate;
        CalculatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
