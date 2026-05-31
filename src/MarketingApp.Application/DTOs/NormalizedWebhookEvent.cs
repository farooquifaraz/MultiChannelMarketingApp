namespace MarketingApp.Application.DTOs;

/// <summary>
/// Provider-agnostic representation of a single inbound delivery / open / click / bounce event.
/// The webhook handlers (SendGrid / Brevo / Mailgun) all flatten their native payloads into this shape
/// so the downstream processor doesn't care about provider differences.
/// </summary>
public sealed record NormalizedWebhookEvent
{
    /// <summary>Provider name: "sendgrid" | "brevo" | "mailgun" — used for idempotency keying + audit.</summary>
    public string Provider { get; init; } = string.Empty;

    /// <summary>
    /// Unique event ID from the provider — guarantees idempotency.
    /// For Brevo (no native event id), we compose one from message-id + event + timestamp.
    /// </summary>
    public string ProviderEventId { get; init; } = string.Empty;

    /// <summary>CampaignMessage.Id resolved from custom args / X-Campaign-Message-Id header. Null when we can't correlate.</summary>
    public Guid? CampaignMessageId { get; init; }

    /// <summary>
    /// Normalized event type:
    ///   "delivered" | "bounced" | "dropped" | "deferred" | "opened" | "clicked" | "unsubscribed" | "spam_report"
    /// Unknown / future event types are passed through as-is so we don't drop signals.
    /// </summary>
    public string EventType { get; init; } = string.Empty;

    /// <summary>When the event happened at the provider (UTC).</summary>
    public DateTime OccurredAt { get; init; }

    /// <summary>For bounce/drop/spam: provider-supplied reason text.</summary>
    public string? Reason { get; init; }

    /// <summary>True only for hard bounces (permanent failure). Used to auto-flag contacts.</summary>
    public bool IsHardBounce { get; init; }
}
