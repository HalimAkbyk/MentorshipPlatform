using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public enum ConversationType
{
    Booking,
    Direct
}

public class Conversation : BaseEntity
{
    public Guid User1Id { get; private set; }
    public Guid User2Id { get; private set; }
    public Guid? BookingId { get; private set; }
    public ConversationType Type { get; private set; }

    // Navigation
    public User User1 { get; private set; } = null!;
    public User User2 { get; private set; } = null!;
    public Booking? Booking { get; private set; }
    public ICollection<Message> Messages { get; private set; } = new List<Message>();

    private Conversation() { }

    public static Conversation CreateDirect(Guid initiatorUserId, Guid recipientUserId)
    {
        return new Conversation
        {
            User1Id = initiatorUserId,
            User2Id = recipientUserId,
            BookingId = null,
            Type = ConversationType.Direct
        };
    }

    public static Conversation CreateForBooking(Guid user1Id, Guid user2Id, Guid bookingId)
    {
        return new Conversation
        {
            User1Id = user1Id,
            User2Id = user2Id,
            BookingId = bookingId,
            Type = ConversationType.Booking
        };
    }

    public bool IsParticipant(Guid userId) => User1Id == userId || User2Id == userId;

    public Guid GetOtherUserId(Guid userId) => User1Id == userId ? User2Id : User1Id;
}
