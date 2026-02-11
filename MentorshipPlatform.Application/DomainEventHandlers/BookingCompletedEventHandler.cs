using Hangfire;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Jobs;
using MentorshipPlatform.Domain.Events;

namespace MentorshipPlatform.Application.DomainEventHandlers;

public class BookingCompletedEventHandler : INotificationHandler<BookingCompletedEvent>
{
    private readonly IApplicationDbContext _context;
    private readonly IBackgroundJobClient _backgroundJobs;

    public BookingCompletedEventHandler(
        IApplicationDbContext context,
        IBackgroundJobClient backgroundJobs)
    {
        _context = context;
        _backgroundJobs = backgroundJobs;
    }

    public Task Handle(BookingCompletedEvent notification, CancellationToken cancellationToken)
    {
        // Schedule payout after 24 hours
        var payoutTime = DateTime.UtcNow.AddHours(24);
        
        _backgroundJobs.Schedule<ProcessMentorPayoutJob>(
            job => job.Execute(notification.BookingId),
            payoutTime);

        return Task.CompletedTask;
    }
}