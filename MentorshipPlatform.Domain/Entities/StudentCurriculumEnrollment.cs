using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class StudentCurriculumEnrollment : BaseEntity
{
    public Guid CurriculumId { get; private set; }
    public Guid StudentUserId { get; private set; }
    public Guid MentorUserId { get; private set; }
    public DateTime StartedAt { get; private set; }
    public decimal CompletionPercentage { get; private set; }
    public string Status { get; private set; } = "Active";

    // Navigation
    public Curriculum Curriculum { get; private set; } = null!;
    public User Student { get; private set; } = null!;
    public User Mentor { get; private set; } = null!;
    public ICollection<TopicProgress> TopicProgresses { get; private set; } = new List<TopicProgress>();

    private StudentCurriculumEnrollment() { }

    public static StudentCurriculumEnrollment Create(
        Guid curriculumId,
        Guid studentUserId,
        Guid mentorUserId)
    {
        return new StudentCurriculumEnrollment
        {
            CurriculumId = curriculumId,
            StudentUserId = studentUserId,
            MentorUserId = mentorUserId,
            StartedAt = DateTime.UtcNow,
            CompletionPercentage = 0,
            Status = "Active"
        };
    }

    public void UpdateCompletionPercentage(decimal percentage)
    {
        CompletionPercentage = percentage;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetStatus(string status)
    {
        Status = status;
        UpdatedAt = DateTime.UtcNow;
    }
}
