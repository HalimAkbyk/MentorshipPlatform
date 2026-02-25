namespace MentorshipPlatform.Application.Common.Interfaces;

public interface IChatNotificationService
{
    Task NotifyNewMessage(Guid recipientUserId, object messagePayload);
    Task NotifyMessagesRead(Guid senderUserId, Guid bookingId, List<Guid> messageIds);
    Task NotifyMessageDelivered(Guid senderUserId, Guid messageId);
    Task NotifyNotificationCountUpdated(Guid userId, int unreadCount);
    Task NotifyRoomStatusChanged(Guid userId, string roomName, bool isActive, bool hostConnected, int participantCount);
    bool IsUserOnline(Guid userId);
}
