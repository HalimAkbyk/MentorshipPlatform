using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Events;

public record ReviewCreatedEvent(Guid MentorUserId, int Rating) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}