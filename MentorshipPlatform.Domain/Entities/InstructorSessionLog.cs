using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Domain.Entities;

public class InstructorSessionLog : BaseEntity
{
    public Guid InstructorId { get; private set; }
    public SessionType SessionType { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid? VideoParticipantId { get; private set; }
    public DateTime JoinedAt { get; private set; }
    public DateTime? LeftAt { get; private set; }

    public User Instructor { get; private set; } = null!;
    public VideoParticipant? VideoParticipant { get; private set; }

    public int? DurationMinutes => LeftAt.HasValue
        ? (int)(LeftAt.Value - JoinedAt).TotalMinutes
        : null;

    private InstructorSessionLog() { }

    public static InstructorSessionLog Create(
        Guid instructorId,
        SessionType sessionType,
        Guid sessionId,
        DateTime joinedAt,
        Guid? videoParticipantId = null)
    {
        return new InstructorSessionLog
        {
            InstructorId = instructorId,
            SessionType = sessionType,
            SessionId = sessionId,
            JoinedAt = joinedAt,
            VideoParticipantId = videoParticipantId
        };
    }

    public void MarkLeft(DateTime leftAt)
    {
        LeftAt = leftAt;
        UpdatedAt = DateTime.UtcNow;
    }
}
