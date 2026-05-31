namespace MarketingApp.Application.Configuration;

/// <summary>
/// Configurable settings for campaign message sending.
/// Controls batch size and delays to manage send rate (helps with deliverability + spam avoidance).
/// Configured via appsettings.json under "CampaignSettings".
/// </summary>
public class CampaignSettings
{
    public const string SectionName = "CampaignSettings";

    /// <summary>How many messages to process in a single batch (default: 50)</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>Delay between batches in milliseconds (default: 500ms)</summary>
    public int DelayBetweenBatchesMs { get; set; } = 500;

    /// <summary>
    /// Delay between individual messages in milliseconds (default: 0).
    /// Set higher (e.g., 1000-3000ms) for better deliverability and to avoid spam filters.
    /// Recommended: 1000ms for Gmail SMTP, 0-200ms for SendGrid/Brevo/Mailgun (they handle rate limits).
    /// </summary>
    public int DelayBetweenMessagesMs { get; set; } = 0;

    /// <summary>Maximum messages per minute (0 = unlimited). Useful for Gmail's 500/day limit.</summary>
    public int MaxMessagesPerMinute { get; set; } = 0;
}
