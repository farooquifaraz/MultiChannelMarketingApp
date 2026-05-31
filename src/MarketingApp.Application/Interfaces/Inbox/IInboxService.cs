using MarketingApp.Application.DTOs;

namespace MarketingApp.Application.Interfaces.Inbox;

/// <summary>
/// Per-user inbox queries + actions (Day 7 G4). All methods enforce that
/// the requester owns the message (or is admin with viewAll=true).
/// </summary>
public interface IInboxService
{
    Task<PagedResponse<InboxMessageListItemDto>> GetAllAsync(
        Guid requesterUserId, bool requesterIsAdmin, bool viewAll,
        int pageNumber, int pageSize,
        bool? unreadOnly, string? category, Guid? smtpGroupId, string? search,
        CancellationToken ct);

    Task<InboxMessageDetailDto> GetByIdAsync(Guid id, Guid requesterUserId, bool requesterIsAdmin, CancellationToken ct);

    Task<InboxUnreadCountDto> GetUnreadCountAsync(Guid requesterUserId, CancellationToken ct);

    Task MarkReadAsync(Guid id, Guid requesterUserId, bool requesterIsAdmin, CancellationToken ct);
    Task MarkAllReadAsync(Guid requesterUserId, CancellationToken ct);
    Task ArchiveAsync(Guid id, Guid requesterUserId, bool requesterIsAdmin, CancellationToken ct);
    Task SaveDraftAsync(Guid id, string html, Guid requesterUserId, bool requesterIsAdmin, CancellationToken ct);

    /// <summary>Send a reply via the owner's SmtpGroup; preserves In-Reply-To + References headers so it threads in Gmail/Outlook.</summary>
    Task SendReplyAsync(Guid id, SendReplyDto dto, Guid requesterUserId, bool requesterIsAdmin, CancellationToken ct);

    // === Day 8: conversation (thread) views ===
    /// <summary>List of conversations (one row per thread) for the inbox, scoped to the user.</summary>
    Task<PagedResponse<InboxThreadListItemDto>> GetThreadsAsync(
        Guid requesterUserId, bool requesterIsAdmin, bool viewAll,
        int pageNumber, int pageSize,
        bool? unreadOnly, string? category, string? search,
        CancellationToken ct);

    /// <summary>Full thread: inbound + outbound messages merged chronologically + active AI draft.</summary>
    Task<InboxThreadDetailDto> GetThreadByIdAsync(Guid threadId, Guid requesterUserId, bool requesterIsAdmin, CancellationToken ct);

    /// <summary>Permanently delete one conversation (all inbound + outbound rows for the thread).</summary>
    Task DeleteThreadAsync(Guid threadId, Guid requesterUserId, bool requesterIsAdmin, CancellationToken ct);

    /// <summary>
    /// Wipe the requester's entire inbox (all their inbox messages + outbound replies) and optionally
    /// reset the IMAP poll cursor so the SAME emails get re-fetched on the next poll — i.e. "start from scratch".
    /// Returns the number of messages deleted.
    /// </summary>
    Task<int> ClearAllAsync(Guid requesterUserId, bool requesterIsAdmin, bool resetImapCursor, CancellationToken ct);
}
