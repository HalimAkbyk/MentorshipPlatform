using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Domain.Entities;

public class VideoSession : BaseEntity
{
    public string ResourceType { get; private set; } = string.Empty;
    public Guid ResourceId { get; private set; }
    public string Provider { get; private set; } = "Twilio";
    public string RoomName { get; private set; } = string.Empty;
    public VideoSessionStatus Status { get; private set; }

    private readonly List<VideoParticipant> _participants = new();
    public IReadOnlyCollection<VideoParticipant> Participants => _participants.AsReadOnly();

    private VideoSession() { }

    public static VideoSession Create(string resourceType, Guid resourceId, string roomName)
    {
        return new VideoSession
        {
            ResourceType = resourceType,
            ResourceId = resourceId,
            RoomName = roomName,
            Status = VideoSessionStatus.Scheduled
        };
    }

    public void MarkAsLive() => Status = VideoSessionStatus.Live;
    public void MarkAsEnded() => Status = VideoSessionStatus.Ended;

    public int GetTotalDurationSeconds()
    {
        return _participants
            .Where(p => p.LeftAt.HasValue)
            .Sum(p => p.DurationSec);
    }
}