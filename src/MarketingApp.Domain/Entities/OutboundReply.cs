namespace MarketingApp.Domain.Entities;

/// <summary>
/// Audit row created every time a user sends a reply from the App Inbox (Day 7 G4).
/// Lets us track per-user reply throughput, retry failed sends, and surface "Replied X min ago" in UI.
/// Kept as a minimal record — body lives on InboxMessage.UserEditedReply.
/// </summary>
public class OutboundReply
{
    public Guid Id { get; set; }
    public Guid InboxMessageId { get; set; }
    public Guid SentByUserId { get; set; }
    public string Subject { get; set; } = string.Empty;
    /// <summary>True if the user modified the AI draft before sending — snapshotted at send time.</summary>
    public bool WasEdited { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool SendSucceeded { get; set; }
    public string? ErrorMessage { get; set; }

    // === Day 8 threading ===
    /// <summary>The RFC 5322 Message-Id we generated + set on the outgoing reply. When the recipient
    /// replies to THIS, their In-Reply-To matches here → we thread it correctly + assign the right owner.</summary>
    public string? SmtpMessageId { get; set; }
    /// <summary>Full HTML body of the sent reply — so the thread timeline shows our side of the conversation.
    /// Previously the body lived on InboxMessage.UserEditedReply which got overwritten on each reply.</summary>
    public string? BodyHtml { get; set; }
    /// <summary>The Message-Id this reply was answering (the inbound message's MessageId).</summary>
    public string? InReplyToMessageId { get; set; }
    /// <summary>Denormalized conversation key for fast thread assembly.</summary>
    public Guid ThreadId { get; set; }

    public InboxMessage? InboxMessage { get; set; }
    public User? SentByUser { get; set; }
}
