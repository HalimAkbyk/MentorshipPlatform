using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Events;

public record BookingConfirmedEvent(
    Guid BookingId,
    Guid StudentUserId,
    Guid MentorUserId,
    DateTime StartAt) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}