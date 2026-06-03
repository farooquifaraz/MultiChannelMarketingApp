namespace MarketingApp.Application.DTOs;

/// <summary>Read-only view of platform settings — visible to everyone (but only admin can edit).</summary>
public record SystemSettingsDto
{
    public int BatchSize { get; init; }
    public int DelayBetweenBatchesMs { get; init; }
    public int DelayBetweenMessagesMs { get; init; }
    public int MaxMessagesPerMinute { get; init; }
    public bool AllowUsersToSeeSharedTemplates { get; init; }
    public bool AllowUsersToSeeSharedContacts { get; init; }
    public bool EnableQuotas { get; init; }
    public bool EnableMultiTenancy { get; init; }

    // Branding
    public string PlatformName { get; init; } = "MarketPro";
    public string? LogoUrl { get; init; }
    public string PrimaryColor { get; init; } = "#4f46e5";
    // Avatar service URL template — placeholders: {name}, {size}, {bg}, {fg}
    public string AvatarServiceUrl { get; init; } = "";

    // Localization
    public string DefaultLocale { get; init; } = "en-US";
    public string DefaultDateFormat { get; init; } = "MMMM dd, yyyy";

    // Validation / limits
    public int PasswordMinLength { get; init; } = 8;
    public int MaxFileUploadSizeMb { get; init; } = 10;

    // Day 7 G3
    public string InboxPollingCron { get; init; } = "*/2 * * * *";

    // Day 7 G5 — AI settings (API key returned MASKED to prevent leakage)
    public string AiProvider { get; init; } = "disabled";
    public string? AiApiKeyMasked { get; init; }
    public string? AiBaseUrl { get; init; }
    public string AiModel { get; init; } = "claude-sonnet-4-5";
    public string AiSystemPrompt { get; init; } = string.Empty;
    public int AiMaxTokens { get; init; } = 800;
    public decimal AiTemperature { get; init; } = 0.4m;
    public int AiTimeoutSeconds { get; init; } = 30;
    public bool AiAllowSendRecipientPii { get; init; } = true;
    public string AiAllowedCategoriesCsv { get; init; } = "question,interested,complaint,unsubscribe,spam,other";

    // Day 10 — fallback provider (key masked on read)
    public string AiFallbackProvider { get; init; } = "disabled";
    public string? AiFallbackApiKeyMasked { get; init; }
    public string? AiFallbackBaseUrl { get; init; }
    public string AiFallbackModel { get; init; } = "llama-3.3-70b-versatile";

    // Phase 3 — image generation (key masked on read)
    public string ImageProvider { get; init; } = "mock";
    public string? ImageApiKeyMasked { get; init; }
    public string? ImageBaseUrl { get; init; }
    public string ImageModel { get; init; } = "dall-e-3";

    public DateTime UpdatedAt { get; init; }
}

public record UpdateSystemSettingsDto
{
    public int BatchSize { get; init; } = 50;
    public int DelayBetweenBatchesMs { get; init; } = 500;
    public int DelayBetweenMessagesMs { get; init; } = 1000;
    public int MaxMessagesPerMinute { get; init; } = 0;
    public bool AllowUsersToSeeSharedTemplates { get; init; } = true;
    public bool AllowUsersToSeeSharedContacts { get; init; } = false;

    public string PlatformName { get; init; } = "MarketPro";
    public string? LogoUrl { get; init; }
    public string PrimaryColor { get; init; } = "#4f46e5";
    public string AvatarServiceUrl { get; init; } = "https://ui-avatars.com/api/?name={name}&size={size}&background={bg}&color={fg}&bold=true&rounded=true";

    public string DefaultLocale { get; init; } = "en-US";
    public string DefaultDateFormat { get; init; } = "MMMM dd, yyyy";

    public int PasswordMinLength { get; init; } = 8;
    public int MaxFileUploadSizeMb { get; init; } = 10;

    // Day 7 G3
    public string InboxPollingCron { get; init; } = "*/2 * * * *";

    // Day 7 G5 — AI settings (API key sent in plain via PUT; backend masks on GET)
    public string AiProvider { get; init; } = "disabled";
    public string? AiApiKey { get; init; }
    public string? AiBaseUrl { get; init; }
    public string AiModel { get; init; } = "claude-sonnet-4-5";
    public string AiSystemPrompt { get; init; } = string.Empty;
    public int AiMaxTokens { get; init; } = 800;
    public decimal AiTemperature { get; init; } = 0.4m;
    public int AiTimeoutSeconds { get; init; } = 30;
    public bool AiAllowSendRecipientPii { get; init; } = true;
    public string AiAllowedCategoriesCsv { get; init; } = "question,interested,complaint,unsubscribe,spam,other";

    // Day 10 — fallback provider (raw key on update; null = keep existing)
    public string AiFallbackProvider { get; init; } = "disabled";
    public string? AiFallbackApiKey { get; init; }
    public string? AiFallbackBaseUrl { get; init; }
    public string AiFallbackModel { get; init; } = "llama-3.3-70b-versatile";

    // Phase 3 — image generation (raw key on update; null = keep existing)
    public string ImageProvider { get; init; } = "mock";
    public string? ImageApiKey { get; init; }
    public string? ImageBaseUrl { get; init; }
    public string ImageModel { get; init; } = "dall-e-3";
}
