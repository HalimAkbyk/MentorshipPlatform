using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Domain.Entities;

public class TopicProgress : BaseEntity
{
    public Guid StudentCurriculumEnrollmentId { get; private set; }
    public Guid CurriculumTopicId { get; private set; }
    public TopicStatus Status { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? MentorNote { get; private set; }
    public Guid? BookingId { get; private set; }

    // Navigation
    public StudentCurriculumEnrollment Enrollment { get; private set; } = null!;
    public CurriculumTopic Topic { get; private set; } = null!;

    private TopicProgress() { }

    public static TopicProgress Create(
        Guid studentCurriculumEnrollmentId,
        Guid curriculumTopicId)
    {
        return new TopicProgress
        {
            StudentCurriculumEnrollmentId = studentCurriculumEnrollmentId,
            CurriculumTopicId = curriculumTopicId,
            Status = TopicStatus.NotStarted
        };
    }

    public void UpdateStatus(TopicStatus status, string? mentorNote = null, Guid? bookingId = null)
    {
        Status = status;
        MentorNote = mentorNote;
        BookingId = bookingId;
        if (status == TopicStatus.Completed)
            CompletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
