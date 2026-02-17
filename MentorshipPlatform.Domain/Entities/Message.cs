using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class Message : BaseEntity
{
    public Guid BookingId { get; private set; }
    public Guid SenderUserId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public bool IsRead { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public DateTime? ReadAt { get; private set; }

    // Navigation
    public Booking Booking { get; private set; } = null!;
    public User SenderUser { get; private set; } = null!;

    private Message() { }

    public static Message Create(Guid bookingId, Guid senderUserId, string content)
    {
        return new Message
        {
            BookingId = bookingId,
            SenderUserId = senderUserId,
            Content = content,
            IsRead = false,
            DeliveredAt = null,
            ReadAt = null
        };
    }

    public void MarkAsDelivered()
    {
        DeliveredAt ??= DateTime.UtcNow;
    }

    public void MarkAsRead()
    {
        IsRead = true;
        ReadAt ??= DateTime.UtcNow;
        DeliveredAt ??= DateTime.UtcNow;
    }
}
