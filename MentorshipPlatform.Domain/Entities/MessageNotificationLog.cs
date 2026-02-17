using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class MessageNotificationLog : BaseEntity
{
    public Guid BookingId { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public DateTime SentAt { get; private set; }
    public int MessageCount { get; private set; }

    private MessageNotificationLog() { }

    public static MessageNotificationLog Create(Guid bookingId, Guid recipientUserId, int messageCount)
    {
        return new MessageNotificationLog
        {
            BookingId = bookingId,
            RecipientUserId = recipientUserId,
            SentAt = DateTime.UtcNow,
            MessageCount = messageCount
        };
    }
}
