using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Domain.Entities;

public class CourseEnrollment : BaseEntity
{
    public Guid CourseId { get; private set; }
    public Guid StudentUserId { get; private set; }
    public CourseEnrollmentStatus Status { get; private set; }
    public decimal CompletionPercentage { get; private set; }
    public DateTime? LastAccessedAt { get; private set; }

    // Navigation
    public Course Course { get; private set; } = null!;
    public User StudentUser { get; private set; } = null!;

    private CourseEnrollment() { }

    public static CourseEnrollment Create(Guid courseId, Guid studentUserId)
    {
        return new CourseEnrollment
        {
            CourseId = courseId,
            StudentUserId = studentUserId,
            Status = CourseEnrollmentStatus.PendingPayment
        };
    }

    public void Confirm()
    {
        Status = CourseEnrollmentStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Refund()
    {
        Status = CourseEnrollmentStatus.Refunded;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateProgress(decimal completionPercentage)
    {
        CompletionPercentage = Math.Clamp(completionPercentage, 0, 100);
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateLastAccessed()
    {
        LastAccessedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
