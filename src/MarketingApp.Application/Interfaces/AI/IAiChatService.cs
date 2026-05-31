using MarketingApp.Application.DTOs;

namespace MarketingApp.Application.Interfaces.AI;

/// <summary>
/// Per-thread "Ask AI" chat agent (Day 9). Lets the USER ask questions ABOUT an email conversation
/// (analysis/understanding) — distinct from the reply-draft feature which writes a reply to the recipient.
/// Multi-turn + persisted. Uses the same admin-configured AI provider as reply drafts.
/// </summary>
public interface IAiChatService
{
    /// <summary>Ask a question about a thread; persists the user turn + assistant turn; returns the assistant turn.</summary>
    Task<AiChatTurnDto> AskAsync(Guid threadId, string question, Guid requesterUserId, bool isAdmin, CancellationToken ct);

    /// <summary>Chronological chat history for a thread (owner-scoped).</summary>
    Task<IReadOnlyList<AiChatTurnDto>> GetHistoryAsync(Guid threadId, Guid requesterUserId, bool isAdmin, CancellationToken ct);

    /// <summary>Wipe a thread's chat history.</summary>
    Task ClearHistoryAsync(Guid threadId, Guid requesterUserId, bool isAdmin, CancellationToken ct);
}
