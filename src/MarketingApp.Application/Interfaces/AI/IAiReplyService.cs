namespace MarketingApp.Application.Interfaces.AI;

/// <summary>
/// Generates AI-suggested replies for incoming inbox messages (Day 7 G6).
/// Enqueued by InboxPollingService for every new InboxMessage. Idempotent — re-running
/// just overwrites the previous suggestion.
/// </summary>
public interface IAiReplyService
{
    Task ProcessAsync(Guid inboxMessageId, CancellationToken ct);
}
