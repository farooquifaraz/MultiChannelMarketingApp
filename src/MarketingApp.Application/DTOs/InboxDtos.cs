namespace MarketingApp.Application.DTOs;

public class InboxMessageListItemDto
{
    public Guid Id { get; set; }
    /// <summary>L3 — "email" or "whatsapp"; drives the channel badge in the inbox list.</summary>
    public string Channel { get; set; } = "email";
    public string FromEmail { get; set; } = string.Empty;
    public string? FromName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? Preview { get; set; }                // first 200 chars of TextBody (or stripped HTML)
    public DateTime ReceivedAt { get; set; }
    public bool IsRead { get; set; }
    public bool IsArchived { get; set; }
    public bool IsOrphanReply { get; set; }
    public string? AiCategory { get; set; }
    public string? AiSummary { get; set; }
    public bool HasAiSuggestion { get; set; }
    public DateTime? RepliedAt { get; set; }
    public Guid? MatchedCampaignId { get; set; }
    public string? MatchedCampaignName { get; set; }
    public string? SmtpGroupName { get; set; }
}

public class InboxMessageDetailDto : InboxMessageListItemDto
{
    public string? HtmlBody { get; set; }
    public string? TextBody { get; set; }
    public string? InReplyToMessageId { get; set; }
    public string? ReferencesHeader { get; set; }
    public string ToEmail { get; set; } = string.Empty;
    public Guid? MatchedCampaignMessageId { get; set; }
    public Guid? MatchedContactId { get; set; }
    public string? MatchedContactName { get; set; }
    // AI fields
    public string? AiSuggestedReply { get; set; }      // original — for Reset button
    public string? UserEditedReply { get; set; }       // current draft
    public DateTime? AiGeneratedAt { get; set; }
    public DateTime? DraftSavedAt { get; set; }
    public string? AiGenerationError { get; set; }
    public string? AiProviderUsed { get; set; }
    public int? AiInputTokens { get; set; }
    public int? AiOutputTokens { get; set; }
    public Guid OwnerUserId { get; set; }
}

public class SaveDraftDto
{
    public string Html { get; set; } = string.Empty;
}

public class SendReplyDto
{
    public string Subject { get; set; } = string.Empty;
    public string Html { get; set; } = string.Empty;
}

public class InboxUnreadCountDto
{
    public int TotalUnread { get; set; }
    public int OrphanCount { get; set; }
}
