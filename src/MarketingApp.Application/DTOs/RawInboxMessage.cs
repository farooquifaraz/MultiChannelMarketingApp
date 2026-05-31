namespace MarketingApp.Application.DTOs;

/// <summary>
/// Provider-agnostic representation of an inbound email pulled by a fetcher.
/// The polling service translates this into an InboxMessage entity after owner resolution.
/// </summary>
public sealed record RawInboxMessage
{
    public uint ImapUid { get; init; }
    public string Folder { get; init; } = "INBOX";
    public string FromEmail { get; init; } = string.Empty;
    public string? FromName { get; init; }
    public string ToEmail { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string? HtmlBody { get; init; }
    public string? TextBody { get; init; }
    public DateTime ReceivedAt { get; init; }
    /// <summary>This email's own Message-Id header (Day 8 threading).</summary>
    public string? MessageId { get; init; }
    public string? InReplyToMessageId { get; init; }
    public string? ReferencesHeader { get; init; }
}
