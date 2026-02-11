using MediatR;

namespace MentorshipPlatform.Domain.Common;

public interface IDomainEvent:INotification
{
    DateTime OccurredOn { get; }
}