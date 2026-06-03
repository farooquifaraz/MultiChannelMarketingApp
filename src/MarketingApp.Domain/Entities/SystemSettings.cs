namespace MarketingApp.Domain.Entities;

/// <summary>
/// Singleton row holding admin-configurable platform-wide settings.
/// Only one row should ever exist; the service treats id=1 as the canonical row.
/// </summary>
public class SystemSettings
{
    public int Id { get; set; } = 1;

    // Campaign rate-limit settings (admin-controllable from UI)
    public int BatchSize { get; set; } = 50;
    public int DelayBetweenBatchesMs { get; set; } = 500;
    public int DelayBetweenMessagesMs { get; set; } = 1000;
    public int MaxMessagesPerMinute { get; set; } = 0; // 0 = unlimited

    // Sharing defaults
    public bool AllowUsersToSeeSharedTemplates { get; set; } = true;
    public bool AllowUsersToSeeSharedContacts { get; set; } = false;

    /// <summary>
    /// Phase 2 — when false (default), plan quotas are tracked + shown but NEVER block a send.
    /// Flip to true only after plans/usage are validated, so existing customers are never cut off.
    /// </summary>
    public bool EnableQuotas { get; set; } = false;

    // === Branding (admin-configurable, removes "MarketPro" hardcode everywhere) ===
    public string PlatformName { get; set; } = "MarketPro";
    public string? LogoUrl { get; set; }
    public string PrimaryColor { get; set; } = "#4f46e5";

    // === Avatar service template ===
    // Use placeholders: {name}, {size}, {bg}, {fg}.  Default → ui-avatars.com.
    // Admin can swap for a self-hosted service or different provider without code changes.
    public string AvatarServiceUrl { get; set; } =
        "https://ui-avatars.com/api/?name={name}&size={size}&background={bg}&color={fg}&bold=true&rounded=true";

    // === Localization defaults ===
    public string DefaultLocale { get; set; } = "en-US";
    public string DefaultDateFormat { get; set; } = "MMMM dd, yyyy";

    // === Validation / limits (mirrors AppConstants but admin-overridable) ===
    public int PasswordMinLength { get; set; } = 8;
    public int MaxFileUploadSizeMb { get; set; } = 10;

    // === Day 7 G3 / Day 8: Inbox polling cadence ===
    // Hangfire cron expression for the inbox-poll recurring job. Empty string = job disabled (removed at startup).
    // Day 8: default lowered to every 1 minute so replies surface fast (SignalR then pushes to UI instantly).
    public string InboxPollingCron { get; set; } = "*/1 * * * *";

    // === Day 7 G5: AI assistant config (used by G6/G7) ===
    // Provider selection ("disabled" | "anthropic" | "openai" | "gemini" | "grok" | "openai-compatible")
    public string AiProvider { get; set; } = "disabled";
    public string? AiApiKey { get; set; }
    public string? AiBaseUrl { get; set; }
    public string AiModel { get; set; } = "claude-sonnet-4-5";
    public string AiSystemPrompt { get; set; } = DefaultAiSystemPrompt;
    public int AiMaxTokens { get; set; } = 2000;
    public decimal AiTemperature { get; set; } = 0.4m;
    public int AiTimeoutSeconds { get; set; } = 30;
    public bool AiAllowSendRecipientPii { get; set; } = true;
    public string AiAllowedCategoriesCsv { get; set; } = "question,interested,complaint,unsubscribe,spam,other";

    // === Day 10: Fallback AI provider ===
    // When the PRIMARY provider returns a quota / rate-limit (429) error, the system auto-retries the
    // SAME request through this fallback (typically a FREE provider like Groq/Ollama). "disabled" = no fallback.
    public string AiFallbackProvider { get; set; } = "disabled";
    public string? AiFallbackApiKey { get; set; }
    public string? AiFallbackBaseUrl { get; set; }
    public string AiFallbackModel { get; set; } = "llama-3.3-70b-versatile";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedByUserId { get; set; }

    /// <summary>Default prompt — also returned by the public default-prompt endpoint so the frontend can reset to it.</summary>
    public const string DefaultAiSystemPrompt = @"You are an email assistant. The user sent a marketing campaign and a recipient replied.
Classify the reply into ONE category from this list: question, interested, complaint, unsubscribe, spam, other.
Then write a short, professional reply in the SAME language as the recipient.
Match the tone of the original campaign. Keep the reply under 150 words.
Also produce exactly 3 short, email-specific questions the USER (not the recipient) might want to ask an AI assistant about this email — e.g. ""What pricing is the sender expecting?"", ""Should I escalate this?"". These power Gmail-style suggestion chips.

OUTPUT FORMAT (strict JSON only — no prose):
{
  ""category"": ""question|interested|complaint|unsubscribe|spam|other"",
  ""summary"": ""one-sentence gist of the recipient's reply"",
  ""questions"": [""...?"", ""...?"", ""...?""],
  ""reply"": ""HTML body of the suggested reply, with <p> paragraphs""
}";
}
