using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MentorshipPlatform.Api.Hubs;

[Authorize]
public class ChatHub : Hub
{
    // Track online users: userId -> set of connectionIds
    public static readonly ConcurrentDictionary<Guid, HashSet<string>> OnlineUsers = new();
    private static readonly object _lock = new();

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId == null)
        {
            Context.Abort();
            return;
        }

        lock (_lock)
        {
            if (!OnlineUsers.TryGetValue(userId.Value, out var connections))
            {
                connections = new HashSet<string>();
                OnlineUsers[userId.Value] = connections;
            }
            connections.Add(Context.ConnectionId);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, userId.Value.ToString());
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId != null)
        {
            lock (_lock)
            {
                if (OnlineUsers.TryGetValue(userId.Value, out var connections))
                {
                    connections.Remove(Context.ConnectionId);
                    if (connections.Count == 0)
                        OnlineUsers.TryRemove(userId.Value, out _);
                }
            }

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId.Value.ToString());
        }

        await base.OnDisconnectedAsync(exception);
    }

    private Guid? GetUserId()
    {
        var claim = Context.User?.FindFirst(ClaimTypes.NameIdentifier);
        if (claim != null && Guid.TryParse(claim.Value, out var userId))
            return userId;
        return null;
    }
}
