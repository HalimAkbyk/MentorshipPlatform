using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class VideoWatchLog : BaseEntity
{
    public Guid LectureId { get; private set; }
    public Guid CourseId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid InstructorId { get; private set; }
    public DateTime WatchStartedAt { get; private set; }
    public DateTime? WatchEndedAt { get; private set; }
    public int WatchedDurationSeconds { get; private set; }
    public int VideoDurationSeconds { get; private set; }
    public decimal CompletionPercentage { get; private set; }
    public bool IsCompleted { get; private set; }

    public CourseLecture Lecture { get; private set; } = null!;
    public Course Course { get; private set; } = null!;
    public User Student { get; private set; } = null!;
    public User Instructor { get; private set; } = null!;

    private VideoWatchLog() { }

    public static VideoWatchLog Create(
        Guid lectureId,
        Guid courseId,
        Guid studentId,
        Guid instructorId,
        DateTime watchStartedAt,
        int videoDurationSeconds)
    {
        return new VideoWatchLog
        {
            LectureId = lectureId,
            CourseId = courseId,
            StudentId = studentId,
            InstructorId = instructorId,
            WatchStartedAt = watchStartedAt,
            VideoDurationSeconds = videoDurationSeconds,
            WatchedDurationSeconds = 0,
            CompletionPercentage = 0,
            IsCompleted = false
        };
    }

    public void UpdateProgress(int watchedDurationSeconds, decimal completionPercentage)
    {
        WatchedDurationSeconds = watchedDurationSeconds;
        CompletionPercentage = Math.Clamp(completionPercentage, 0, 100);
        IsCompleted = CompletionPercentage >= 90;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkEnded(DateTime endedAt, int watchedDurationSeconds)
    {
        WatchEndedAt = endedAt;
        WatchedDurationSeconds = watchedDurationSeconds;
        if (VideoDurationSeconds > 0)
            CompletionPercentage = Math.Clamp((decimal)watchedDurationSeconds / VideoDurationSeconds * 100, 0, 100);
        IsCompleted = CompletionPercentage >= 90;
        UpdatedAt = DateTime.UtcNow;
    }
}
