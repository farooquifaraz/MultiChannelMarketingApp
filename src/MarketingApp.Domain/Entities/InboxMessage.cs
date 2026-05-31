namespace MarketingApp.Domain.Entities;

/// <summary>
/// An inbound reply pulled by IMAP polling (Day 7 G3).
///
/// CRITICAL: every InboxMessage carries an explicit <see cref="OwnerUserId"/> so per-user inbox
/// isolation is a SIMPLE column filter — no joins needed at query time. Owner resolution at insert:
///   1. In-Reply-To header  -> match to CampaignMessage.SmtpMessageId -> OwnerUserId = campaign.UserId
///   2. From email          -> single Contact owner -> OwnerUserId = contact.UserId
///   3. Catch-all           -> SmtpGroup.DefaultInboxOwnerUserId (admin's nominee); IsOrphanReply=true
/// </summary>
public class InboxMessage
{
    public Guid Id { get; set; }

    /// <summary>The user this reply belongs to. Used for per-user list scoping (A5 in Day 7 plan).</summary>
    public Guid OwnerUserId { get; set; }

    public Guid SmtpGroupId { get; set; }

    /// <summary>IMAP unique ID within the folder — combined with (SmtpGroupId, ImapFolder) for dedup.</summary>
    public uint ImapUid { get; set; }
    public string ImapFolder { get; set; } = "INBOX";

    public string FromEmail { get; set; } = string.Empty;
    public string? FromName { get; set; }
    public string ToEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? HtmlBody { get; set; }
    public string? TextBody { get; set; }
    public DateTime ReceivedAt { get; set; }

    /// <summary>This email's OWN RFC 5322 Message-Id header. Critical for threading: a later reply's
    /// In-Reply-To points to THIS value, letting us link the conversation chain (Day 8 A1/B2).</summary>
    public string? MessageId { get; set; }
    /// <summary>RFC 5322 In-Reply-To header (Message-Id of the email being replied to).</summary>
    public string? InReplyToMessageId { get; set; }
    /// <summary>RFC 5322 References header chain — for long thread depth.</summary>
    public string? ReferencesHeader { get; set; }

    // === Day 8 threading ===
    /// <summary>Conversation grouping key. All messages in one Gmail-style thread share this.</summary>
    public Guid ThreadId { get; set; }
    /// <summary>Subject with Re:/Fwd:/Fw: prefixes stripped + lowercased — fallback grouping key.</summary>
    public string? NormalizedSubject { get; set; }

    /// <summary>Linked CampaignMessage if In-Reply-To matched. Null for cold replies.</summary>
    public Guid? MatchedCampaignMessageId { get; set; }
    /// <summary>Linked Contact if FromEmail matched a known address. Null when sender is new.</summary>
    public Guid? MatchedContactId { get; set; }
    /// <summary>True when neither match worked and the message went to the catch-all owner.</summary>
    public bool IsOrphanReply { get; set; }

    public bool IsRead { get; set; }
    public bool IsArchived { get; set; }

    // === AI fields populated by Feature 3 (G5/G6) ===
    public string? AiCategory { get; set; }
    public string? AiSummary { get; set; }
    /// <summary>Immutable AI-generated draft. User can revert to this via "Reset to AI version".</summary>
    public string? AiSuggestedReply { get; set; }
    /// <summary>User's edited version of the draft (auto-saved). Distinct from AiSuggestedReply so the original is never lost.</summary>
    public string? UserEditedReply { get; set; }
    public DateTime? AiGeneratedAt { get; set; }
    public DateTime? DraftSavedAt { get; set; }
    public string? AiGenerationError { get; set; }
    /// <summary>JSON array of 3 AI-generated, email-specific questions (Gmail-style chips). Day 9.</summary>
    public string? AiSuggestedQuestions { get; set; }
    /// <summary>Provider used to generate the suggestion — useful for cost tracking + admin display.</summary>
    public string? AiProviderUsed { get; set; }
    public int? AiInputTokens { get; set; }
    public int? AiOutputTokens { get; set; }

    // === Reply send tracking ===
    public DateTime? RepliedAt { get; set; }
    public Guid? RepliedByUserId { get; set; }
    /// <summary>True if the user modified the AI draft before sending.</summary>
    public bool ReplyWasEdited { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation (lazy — not all queries need these)
    public User? OwnerUser { get; set; }
    public SmtpGroup? SmtpGroup { get; set; }
    public CampaignMessage? MatchedCampaignMessage { get; set; }
    public Contact? MatchedContact { get; set; }
}
