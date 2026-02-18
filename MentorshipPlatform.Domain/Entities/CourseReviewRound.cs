using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Domain.Entities;

/// <summary>
/// Represents one submit → review cycle for a course.
/// Each time a mentor submits (or resubmits) a course for review, a new round is created.
/// </summary>
public class CourseReviewRound : BaseEntity
{
    public Guid CourseId { get; private set; }
    public int RoundNumber { get; private set; }
    public Guid SubmittedByUserId { get; private set; }
    public DateTime SubmittedAt { get; private set; }
    public string? MentorNotes { get; private set; }

    // Review result (filled by admin)
    public Guid? ReviewedByUserId { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public ReviewOutcome? Outcome { get; private set; }
    public string? AdminGeneralNotes { get; private set; }

    // Navigation
    public Course Course { get; private set; } = null!;
    private readonly List<LectureReviewComment> _lectureComments = new();
    public IReadOnlyCollection<LectureReviewComment> LectureComments => _lectureComments.AsReadOnly();

    private CourseReviewRound() { }

    public static CourseReviewRound Create(
        Guid courseId,
        int roundNumber,
        Guid submittedByUserId,
        string? mentorNotes = null)
    {
        return new CourseReviewRound
        {
            CourseId = courseId,
            RoundNumber = roundNumber,
            SubmittedByUserId = submittedByUserId,
            SubmittedAt = DateTime.UtcNow,
            MentorNotes = mentorNotes
        };
    }

    public void Approve(Guid adminUserId, string? notes = null)
    {
        ReviewedByUserId = adminUserId;
        ReviewedAt = DateTime.UtcNow;
        Outcome = ReviewOutcome.Approved;
        AdminGeneralNotes = notes;
    }

    public void Reject(Guid adminUserId, string notes)
    {
        ReviewedByUserId = adminUserId;
        ReviewedAt = DateTime.UtcNow;
        Outcome = ReviewOutcome.Rejected;
        AdminGeneralNotes = notes;
    }

    public void RequestRevision(Guid adminUserId, string? notes = null)
    {
        ReviewedByUserId = adminUserId;
        ReviewedAt = DateTime.UtcNow;
        Outcome = ReviewOutcome.RevisionRequested;
        AdminGeneralNotes = notes;
    }
}
