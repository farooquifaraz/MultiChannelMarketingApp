namespace MarketingApp.Application.DTOs;

public class AiChatTurnDto
{
    public Guid Id { get; set; }
    public string Role { get; set; } = "user";       // "user" | "assistant"
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? ProviderUsed { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    /// <summary>Dynamic follow-up suggestions returned with an assistant turn (powers the chip row).</summary>
    public List<string> Suggestions { get; set; } = new();
}

public class AskAiChatDto
{
    public string Question { get; set; } = string.Empty;
}
