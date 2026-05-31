using MarketingApp.API.Hubs;
using MarketingApp.Application.Interfaces.Inbox;
using Microsoft.AspNetCore.SignalR;

namespace MarketingApp.API.Realtime;

/// <summary>
/// API-layer implementation of IInboxRealtimeNotifier (Day 8). Wraps IHubContext&lt;InboxHub&gt;
/// and pushes events to the owner's per-user group. Best-effort: swallows failures so a dropped
/// socket never breaks the background ingest pipeline.
/// </summary>
public class SignalRInboxNotifier : IInboxRealtimeNotifier
{
    private readonly IHubContext<InboxHub> _hub;
    private readonly ILogger<SignalRInboxNotifier> _logger;

    public SignalRInboxNotifier(IHubContext<InboxHub> hub, ILogger<SignalRInboxNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task NotifyNewMessageAsync(Guid ownerUserId, Guid threadId, Guid inboxMessageId)
    {
        try
        {
            await _hub.Clients.Group(InboxHub.UserGroup(ownerUserId))
                .SendAsync("inbox:new", new { threadId, inboxMessageId });
        }
        catch (Exception ex) { _logger.LogWarning(ex, "SignalR inbox:new push failed for user {UserId}", ownerUserId); }
    }

    public async Task NotifyAiReadyAsync(Guid ownerUserId, Guid threadId, Guid inboxMessageId)
    {
        try
        {
            await _hub.Clients.Group(InboxHub.UserGroup(ownerUserId))
                .SendAsync("inbox:ai-ready", new { threadId, inboxMessageId });
        }
        catch (Exception ex) { _logger.LogWarning(ex, "SignalR inbox:ai-ready push failed for user {UserId}", ownerUserId); }
    }
}
