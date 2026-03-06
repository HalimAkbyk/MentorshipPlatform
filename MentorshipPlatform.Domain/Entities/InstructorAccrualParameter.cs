using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class InstructorAccrualParameter : BaseEntity
{
    public Guid? InstructorId { get; private set; }
    public decimal PrivateLessonRate { get; private set; }
    public decimal GroupLessonRate { get; private set; }
    public decimal VideoContentRate { get; private set; }
    public int? BonusThresholdLessons { get; private set; }
    public decimal? BonusPercentage { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime ValidFrom { get; private set; }
    public DateTime? ValidTo { get; private set; }
    public Guid UpdatedBy { get; private set; }

    public User? Instructor { get; private set; }

    private InstructorAccrualParameter() { }

    public static InstructorAccrualParameter Create(
        decimal privateLessonRate,
        decimal groupLessonRate,
        decimal videoContentRate,
        Guid updatedBy,
        Guid? instructorId = null,
        int? bonusThresholdLessons = null,
        decimal? bonusPercentage = null,
        DateTime? validFrom = null,
        DateTime? validTo = null)
    {
        return new InstructorAccrualParameter
        {
            InstructorId = instructorId,
            PrivateLessonRate = privateLessonRate,
            GroupLessonRate = groupLessonRate,
            VideoContentRate = videoContentRate,
            BonusThresholdLessons = bonusThresholdLessons,
            BonusPercentage = bonusPercentage,
            IsActive = true,
            ValidFrom = validFrom ?? DateTime.UtcNow,
            ValidTo = validTo,
            UpdatedBy = updatedBy
        };
    }

    public void Update(
        decimal privateLessonRate,
        decimal groupLessonRate,
        decimal videoContentRate,
        int? bonusThresholdLessons,
        decimal? bonusPercentage,
        DateTime? validTo,
        Guid updatedBy)
    {
        PrivateLessonRate = privateLessonRate;
        GroupLessonRate = groupLessonRate;
        VideoContentRate = videoContentRate;
        BonusThresholdLessons = bonusThresholdLessons;
        BonusPercentage = bonusPercentage;
        ValidTo = validTo;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
