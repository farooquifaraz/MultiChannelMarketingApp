namespace MarketingApp.Application.Interfaces.Inbox;

/// <summary>
/// Pushes real-time inbox events to the owning user's connected clients (Day 8 SignalR).
/// Implemented in the API layer over IHubContext&lt;InboxHub&gt; — kept as an interface here so the
/// Application layer (polling job, AI service) stays free of SignalR / ASP.NET types (Clean Architecture).
/// All methods are best-effort: failures are swallowed by the implementation so they never break ingest.
/// </summary>
public interface IInboxRealtimeNotifier
{
    /// <summary>A new inbound message landed — tell the owner's clients to refresh the thread list + the open thread.</summary>
    Task NotifyNewMessageAsync(Guid ownerUserId, Guid threadId, Guid inboxMessageId);

    /// <summary>An AI suggestion finished generating for a message — tell the owner's clients to refresh the draft.</summary>
    Task NotifyAiReadyAsync(Guid ownerUserId, Guid threadId, Guid inboxMessageId);
}
