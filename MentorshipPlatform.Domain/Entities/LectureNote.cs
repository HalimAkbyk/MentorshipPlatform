using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class LectureNote : BaseEntity
{
    public Guid EnrollmentId { get; private set; }
    public Guid LectureId { get; private set; }
    public int TimestampSec { get; private set; }
    public string Content { get; private set; } = string.Empty;

    // Navigation
    public CourseEnrollment Enrollment { get; private set; } = null!;
    public CourseLecture Lecture { get; private set; } = null!;

    private LectureNote() { }

    public static LectureNote Create(Guid enrollmentId, Guid lectureId, int timestampSec, string content)
    {
        return new LectureNote
        {
            EnrollmentId = enrollmentId,
            LectureId = lectureId,
            TimestampSec = timestampSec,
            Content = content
        };
    }

    public void Update(string content, int? timestampSec = null)
    {
        Content = content;
        if (timestampSec.HasValue)
            TimestampSec = timestampSec.Value;
        UpdatedAt = DateTime.UtcNow;
    }
}
