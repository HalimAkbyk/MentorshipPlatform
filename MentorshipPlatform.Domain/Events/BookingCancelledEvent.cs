using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Events;

public record BookingCancelledEvent(
    Guid BookingId,
    DateTime OriginalStartAt,
    string Reason) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}