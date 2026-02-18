using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Domain.Entities;

public class CourseAdminNote : BaseEntity
{
    public Guid CourseId { get; private set; }
    public Guid? LectureId { get; private set; }
    public Guid AdminUserId { get; private set; }
    public AdminNoteType NoteType { get; private set; }
    public LectureReviewFlag? Flag { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public string? LectureTitle { get; private set; }

    // Navigation
    public Course Course { get; private set; } = null!;
    public CourseLecture? Lecture { get; private set; }
    public User AdminUser { get; private set; } = null!;

    private CourseAdminNote() { }

    public static CourseAdminNote Create(
        Guid courseId,
        Guid? lectureId,
        Guid adminUserId,
        AdminNoteType noteType,
        LectureReviewFlag? flag,
        string content,
        string? lectureTitle = null)
    {
        return new CourseAdminNote
        {
            CourseId = courseId,
            LectureId = lectureId,
            AdminUserId = adminUserId,
            NoteType = noteType,
            Flag = flag,
            Content = content,
            LectureTitle = lectureTitle
        };
    }
}
