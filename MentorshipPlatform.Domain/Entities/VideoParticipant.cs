using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class VideoParticipant : BaseEntity
{
    public Guid VideoSessionId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime JoinedAt { get; private set; }
    public DateTime? LeftAt { get; private set; }
    public int DurationSec { get; private set; }

    private VideoParticipant() { }

    public static VideoParticipant Create(Guid videoSessionId, Guid userId)
    {
        return new VideoParticipant
        {
            VideoSessionId = videoSessionId,
            UserId = userId,
            JoinedAt = DateTime.UtcNow
        };
    }

    public void Leave()
    {
        LeftAt = DateTime.UtcNow;
        DurationSec = (int)(LeftAt.Value - JoinedAt).TotalSeconds;
    }
}