using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Events;

public record BookingCompletedEvent(Guid BookingId, Guid MentorUserId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}