namespace MarketingApp.Application.DTOs;

/// <summary>One row in the conversation list — represents an entire thread, not a single message.</summary>
public class InboxThreadListItemDto
{
    public Guid ThreadId { get; set; }
    /// <summary>L3 — "email" or "whatsapp"; drives the channel badge in the conversation list.</summary>
    public string Channel { get; set; } = "email";
    /// <summary>The other party in the conversation (recipient).</summary>
    public string ParticipantEmail { get; set; } = string.Empty;
    public string? ParticipantName { get; set; }
    public string Subject { get; set; } = string.Empty;
    /// <summary>Preview of the most recent message in the thread.</summary>
    public string? LatestPreview { get; set; }
    public DateTime LastActivityAt { get; set; }
    public int MessageCount { get; set; }
    public int UnreadCount { get; set; }
    /// <summary>AI category of the latest inbound message.</summary>
    public string? AiCategory { get; set; }
    public bool HasAiSuggestion { get; set; }
    public Guid? MatchedCampaignId { get; set; }
    public string? MatchedCampaignName { get; set; }
    public string? SmtpGroupName { get; set; }
    public bool IsOrphan { get; set; }
    /// <summary>Id of the latest INBOUND message — the one the reply composer + AI draft target.</summary>
    public Guid? LatestInboundMessageId { get; set; }
}

/// <summary>One message within a thread timeline — either an inbound reply or our outbound reply.</summary>
public class ThreadMessageDto
{
    public Guid Id { get; set; }
    /// <summary>"in" = recipient → us; "out" = us → recipient.</summary>
    public string Direction { get; set; } = "in";
    public string FromEmail { get; set; } = string.Empty;
    public string? FromName { get; set; }
    public string ToEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? HtmlBody { get; set; }
    public string? TextBody { get; set; }
    public DateTime At { get; set; }
    public bool IsRead { get; set; }
    /// <summary>For inbound messages with AI processing — surfaced so the UI can show category chips inline.</summary>
    public string? AiCategory { get; set; }
}

/// <summary>Full conversation: chronological timeline + the active AI draft for the latest inbound message.</summary>
public class InboxThreadDetailDto
{
    public Guid ThreadId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string ParticipantEmail { get; set; } = string.Empty;
    public string? ParticipantName { get; set; }
    public string? SmtpGroupName { get; set; }
    public Guid? MatchedCampaignId { get; set; }
    public string? MatchedCampaignName { get; set; }
    public List<ThreadMessageDto> Messages { get; set; } = new();

    // === Reply composer state — bound to the latest INBOUND message ===
    public Guid? LatestInboundMessageId { get; set; }
    public string? AiCategory { get; set; }
    public string? AiSummary { get; set; }
    public string? AiSuggestedReply { get; set; }
    public string? UserEditedReply { get; set; }
    public string? AiGenerationError { get; set; }
    public string? AiProviderUsed { get; set; }
    public int? AiInputTokens { get; set; }
    public int? AiOutputTokens { get; set; }
    public DateTime? AiGeneratedAt { get; set; }
    /// <summary>Day 9: 3 AI-suggested questions for the "Ask AI" chip row.</summary>
    public List<string> AiSuggestedQuestions { get; set; } = new();
}
