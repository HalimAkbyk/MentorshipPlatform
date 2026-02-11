using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Domain.Events;

public record OrderPaidEvent(
    Guid OrderId,
    Guid BuyerUserId,
    OrderType Type,
    Guid ResourceId,
    decimal Amount) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}