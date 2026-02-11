using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Events;
using MentorshipPlatform.Domain.Exceptions;

namespace MentorshipPlatform.Domain.Entities;

public class Review : BaseEntity
{
    public Guid AuthorUserId { get; private set; }
 
    public Guid MentorUserId { get; private set; } 
    
    public string ResourceType { get; private set; } = string.Empty;
    public Guid ResourceId { get; private set; }
    public int Rating { get; private set; }
    public string? Comment { get; private set; }
    public User AuthorUser { get; private set; } = null!;
    public User MentorUser { get; private set; }= null!;
    private Review() { }

    public static Review Create(
        Guid authorUserId,
        Guid mentorUserId,
        string resourceType,
        Guid resourceId,
        int rating,
        string? comment)
    {
        if (rating < 1 || rating > 5)
            throw new DomainException("Rating must be between 1 and 5");

        var review = new Review
        {
            AuthorUserId = authorUserId,
            MentorUserId = mentorUserId,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Rating = rating,
            Comment = comment
        };

        review.AddDomainEvent(new ReviewCreatedEvent(mentorUserId, rating));
        return review;
    }
}