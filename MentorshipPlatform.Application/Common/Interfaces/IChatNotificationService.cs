namespace MentorshipPlatform.Application.Common.Interfaces;

public interface IChatNotificationService
{
    Task NotifyNewMessage(Guid recipientUserId, object messagePayload);
    Task NotifyMessagesRead(Guid senderUserId, Guid bookingId, List<Guid> messageIds);
    Task NotifyMessageDelivered(Guid senderUserId, Guid messageId);
    bool IsUserOnline(Guid userId);
}
