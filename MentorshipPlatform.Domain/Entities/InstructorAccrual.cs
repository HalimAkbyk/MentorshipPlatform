using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Domain.Entities;

public class InstructorAccrual : BaseEntity
{
    public Guid InstructorId { get; private set; }
    public DateTime PeriodStart { get; private set; }
    public DateTime PeriodEnd { get; private set; }
    public int PrivateLessonCount { get; private set; }
    public decimal PrivateLessonUnitPrice { get; private set; }
    public int GroupLessonCount { get; private set; }
    public decimal GroupLessonUnitPrice { get; private set; }
    public int VideoContentCount { get; private set; }
    public decimal VideoUnitPrice { get; private set; }
    public decimal BonusAmount { get; private set; }
    public string? BonusDescription { get; private set; }
    public decimal TotalAccrual { get; private set; }
    public AccrualStatus Status { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public string? Notes { get; private set; }

    public User Instructor { get; private set; } = null!;

    private InstructorAccrual() { }

    public static InstructorAccrual Create(
        Guid instructorId,
        DateTime periodStart,
        DateTime periodEnd,
        int privateLessonCount,
        decimal privateLessonUnitPrice,
        int groupLessonCount,
        decimal groupLessonUnitPrice,
        int videoContentCount,
        decimal videoUnitPrice,
        decimal bonusAmount = 0,
        string? bonusDescription = null)
    {
        var total = (privateLessonCount * privateLessonUnitPrice)
                  + (groupLessonCount * groupLessonUnitPrice)
                  + (videoContentCount * videoUnitPrice)
                  + bonusAmount;

        return new InstructorAccrual
        {
            InstructorId = instructorId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            PrivateLessonCount = privateLessonCount,
            PrivateLessonUnitPrice = privateLessonUnitPrice,
            GroupLessonCount = groupLessonCount,
            GroupLessonUnitPrice = groupLessonUnitPrice,
            VideoContentCount = videoContentCount,
            VideoUnitPrice = videoUnitPrice,
            BonusAmount = bonusAmount,
            BonusDescription = bonusDescription,
            TotalAccrual = total,
            Status = AccrualStatus.Draft
        };
    }

    public void Approve(Guid approvedBy)
    {
        Status = AccrualStatus.Approved;
        ApprovedBy = approvedBy;
        ApprovedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkPaid()
    {
        Status = AccrualStatus.Paid;
        PaidAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        Status = AccrualStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddNote(string note)
    {
        Notes = note;
        UpdatedAt = DateTime.UtcNow;
    }
}
