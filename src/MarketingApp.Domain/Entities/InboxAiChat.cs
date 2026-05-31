namespace MarketingApp.Domain.Entities;

/// <summary>
/// One turn of the per-thread "Ask AI" chat (Day 9). Persisted so the conversation survives reloads.
/// Scoped to the thread owner. Role is "user" or "assistant".
/// </summary>
public class InboxAiChat
{
    public Guid Id { get; set; }
    public Guid ThreadId { get; set; }
    public Guid OwnerUserId { get; set; }
    /// <summary>"user" | "assistant".</summary>
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public string? ProviderUsed { get; set; }
    /// <summary>JSON array of dynamic follow-up question suggestions (assistant turns only). Day 9.</summary>
    public string? Suggestions { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
