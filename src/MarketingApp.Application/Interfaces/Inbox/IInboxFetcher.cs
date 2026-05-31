using MarketingApp.Application.DTOs;
using MarketingApp.Domain.Entities;

namespace MarketingApp.Application.Interfaces.Inbox;

/// <summary>
/// Strategy contract for pulling inbox messages from a SmtpGroup's mailbox (Day 7 G3).
/// v1 ships with <c>MailKitImapInboxFetcher</c>. Future: <c>GmailApiInboxFetcher</c>,
/// <c>MsGraphInboxFetcher</c> — no change to <c>InboxPollingService</c> needed.
/// </summary>
public interface IInboxFetcher
{
    /// <summary>
    /// Fetch messages with IMAP UID strictly greater than <paramref name="lastUid"/>.
    /// Returns an empty list when nothing is new. Implementations are PURE — no DB writes.
    /// </summary>
    Task<IReadOnlyList<RawInboxMessage>> FetchSinceAsync(SmtpGroup group, uint lastUid, CancellationToken ct);
}
