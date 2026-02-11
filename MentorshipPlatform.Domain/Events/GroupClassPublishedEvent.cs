using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Events;

public record GroupClassPublishedEvent(
    Guid ClassId,
    string Title,
    DateTime StartAt) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}