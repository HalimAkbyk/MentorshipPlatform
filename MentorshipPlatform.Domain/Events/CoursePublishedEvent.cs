using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Events;

public record CoursePublishedEvent(
    Guid CourseId,
    Guid MentorUserId,
    string Title) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
