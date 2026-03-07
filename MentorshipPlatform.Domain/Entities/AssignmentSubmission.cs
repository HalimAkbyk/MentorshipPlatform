using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Domain.Entities;

public class AssignmentSubmission : BaseEntity
{
    public Guid AssignmentId { get; private set; }
    public Guid StudentUserId { get; private set; }
    public string? SubmissionText { get; private set; }
    public string? FileUrl { get; private set; }
    public string? OriginalFileName { get; private set; }
    public DateTime SubmittedAt { get; private set; }
    public bool IsLate { get; private set; }
    public SubmissionStatus Status { get; private set; }

    // Navigation
    public Assignment Assignment { get; private set; } = null!;
    public User Student { get; private set; } = null!;
    public SubmissionReview? Review { get; private set; }

    private AssignmentSubmission() { }

    public static AssignmentSubmission Create(
        Guid assignmentId,
        Guid studentUserId,
        string? submissionText,
        string? fileUrl,
        string? originalFileName,
        bool isLate)
    {
        return new AssignmentSubmission
        {
            AssignmentId = assignmentId,
            StudentUserId = studentUserId,
            SubmissionText = submissionText,
            FileUrl = fileUrl,
            OriginalFileName = originalFileName,
            SubmittedAt = DateTime.UtcNow,
            IsLate = isLate,
            Status = SubmissionStatus.Submitted
        };
    }

    public void MarkReviewed()
    {
        Status = SubmissionStatus.Reviewed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkReturned()
    {
        Status = SubmissionStatus.Returned;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkResubmitted()
    {
        Status = SubmissionStatus.Resubmitted;
        UpdatedAt = DateTime.UtcNow;
    }
}
