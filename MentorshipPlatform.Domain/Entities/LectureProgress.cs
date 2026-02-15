using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class LectureProgress : BaseEntity
{
    public Guid EnrollmentId { get; private set; }
    public Guid LectureId { get; private set; }
    public bool IsCompleted { get; private set; }
    public int WatchedSec { get; private set; }
    public int LastPositionSec { get; private set; }

    // Navigation
    public CourseEnrollment Enrollment { get; private set; } = null!;
    public CourseLecture Lecture { get; private set; } = null!;

    private LectureProgress() { }

    public static LectureProgress Create(Guid enrollmentId, Guid lectureId)
    {
        return new LectureProgress
        {
            EnrollmentId = enrollmentId,
            LectureId = lectureId
        };
    }

    public void UpdateProgress(int watchedSec, int lastPositionSec)
    {
        WatchedSec = watchedSec;
        LastPositionSec = lastPositionSec;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkCompleted()
    {
        IsCompleted = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
