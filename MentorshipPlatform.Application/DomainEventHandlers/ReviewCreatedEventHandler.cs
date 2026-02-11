using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.DomainEventHandlers;

public class ReviewCreatedEventHandler : INotificationHandler<ReviewCreatedEvent>
{
    private readonly IApplicationDbContext _context;

    public ReviewCreatedEventHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(ReviewCreatedEvent notification, CancellationToken cancellationToken)
    {
        // Update mentor's rating
        var mentorProfile = await _context.MentorProfiles
            .FirstOrDefaultAsync(m => m.UserId == notification.MentorUserId, cancellationToken);

        if (mentorProfile != null)
        {
            mentorProfile.UpdateRating(notification.Rating);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}