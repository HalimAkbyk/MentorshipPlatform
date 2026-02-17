using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Events;

public record GroupClassCancelledEvent(Guid ClassId, Guid MentorUserId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
