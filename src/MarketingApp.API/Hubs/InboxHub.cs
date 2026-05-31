using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MarketingApp.API.Hubs;

/// <summary>
/// Real-time inbox hub (Day 8). Each authenticated connection joins a per-user group "user-{userId}"
/// so the server can push inbox events to ONLY that user's open browser tabs — preserving the same
/// per-user isolation the REST endpoints enforce.
///
/// Client events emitted (see SignalRInboxNotifier):
///   "inbox:new"      — a new inbound message arrived  { threadId, inboxMessageId }
///   "inbox:ai-ready" — an AI suggestion finished       { threadId, inboxMessageId }
/// </summary>
[Authorize]
public class InboxHub : Hub
{
    public static string UserGroup(Guid userId) => $"user-{userId}";

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(userId, out var uid))
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(uid));
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(userId, out var uid))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, UserGroup(uid));
        await base.OnDisconnectedAsync(exception);
    }
}
