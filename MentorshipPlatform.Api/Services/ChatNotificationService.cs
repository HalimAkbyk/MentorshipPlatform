using MentorshipPlatform.Api.Hubs;
using MentorshipPlatform.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace MentorshipPlatform.Api.Services;

public class ChatNotificationService : IChatNotificationService
{
    private readonly IHubContext<ChatHub> _hubContext;

    public ChatNotificationService(IHubContext<ChatHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyNewMessage(Guid recipientUserId, object messagePayload)
    {
        await _hubContext.Clients
            .Group(recipientUserId.ToString())
            .SendAsync("ReceiveMessage", messagePayload);
    }

    public async Task NotifyMessagesRead(Guid senderUserId, Guid bookingId, List<Guid> messageIds)
    {
        await _hubContext.Clients
            .Group(senderUserId.ToString())
            .SendAsync("MessagesRead", new { bookingId, messageIds });
    }

    public async Task NotifyMessageDelivered(Guid senderUserId, Guid messageId)
    {
        await _hubContext.Clients
            .Group(senderUserId.ToString())
            .SendAsync("MessageDelivered", new { messageId });
    }

    public async Task NotifyNotificationCountUpdated(Guid userId, int unreadCount)
    {
        await _hubContext.Clients
            .Group(userId.ToString())
            .SendAsync("NotificationCountUpdated", new { unreadCount });
    }

    public async Task NotifyRoomStatusChanged(Guid userId, string roomName, bool isActive, bool hostConnected, int participantCount)
    {
        await _hubContext.Clients
            .Group(userId.ToString())
            .SendAsync("RoomStatusChanged", new { roomName, isActive, hostConnected, participantCount });
    }

    public bool IsUserOnline(Guid userId)
    {
        return ChatHub.OnlineUsers.ContainsKey(userId);
    }
}
