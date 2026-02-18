using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Domain.Entities;

/// <summary>
/// A per-lecture comment/flag left by admin during a review round.
/// Snapshots LectureTitle and VideoKey so history persists even if video is deleted.
/// </summary>
public class LectureReviewComment : BaseEntity
{
    public Guid ReviewRoundId { get; private set; }
    public Guid? LectureId { get; private set; }           // Nullable: lecture may be deleted later
    public string LectureTitle { get; private set; } = string.Empty;  // Snapshot
    public string? VideoKey { get; private set; }           // Snapshot of video key at review time
    public LectureReviewFlag Flag { get; private set; }
    public string Comment { get; private set; } = string.Empty;
    public Guid CreatedByUserId { get; private set; }

    // Navigation
    public CourseReviewRound ReviewRound { get; private set; } = null!;
    public CourseLecture? Lecture { get; private set; }

    private LectureReviewComment() { }

    public static LectureReviewComment Create(
        Guid reviewRoundId,
        Guid? lectureId,
        string lectureTitle,
        string? videoKey,
        LectureReviewFlag flag,
        string comment,
        Guid adminUserId)
    {
        return new LectureReviewComment
        {
            ReviewRoundId = reviewRoundId,
            LectureId = lectureId,
            LectureTitle = lectureTitle,
            VideoKey = videoKey,
            Flag = flag,
            Comment = comment,
            CreatedByUserId = adminUserId
        };
    }
}
