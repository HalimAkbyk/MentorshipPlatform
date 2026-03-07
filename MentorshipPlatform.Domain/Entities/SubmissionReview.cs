using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Domain.Entities;

public class SubmissionReview : BaseEntity
{
    public Guid SubmissionId { get; private set; }
    public Guid MentorUserId { get; private set; }
    public int? Score { get; private set; }
    public string? Feedback { get; private set; }
    public ReviewStatus Status { get; private set; }
    public DateTime ReviewedAt { get; private set; }

    // Navigation
    public AssignmentSubmission Submission { get; private set; } = null!;
    public User Mentor { get; private set; } = null!;

    private SubmissionReview() { }

    public static SubmissionReview Create(
        Guid submissionId,
        Guid mentorUserId,
        int? score,
        string? feedback,
        ReviewStatus status)
    {
        return new SubmissionReview
        {
            SubmissionId = submissionId,
            MentorUserId = mentorUserId,
            Score = score,
            Feedback = feedback,
            Status = status,
            ReviewedAt = DateTime.UtcNow
        };
    }
}
