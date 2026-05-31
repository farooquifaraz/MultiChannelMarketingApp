namespace MarketingApp.Domain.Entities;

/// <summary>
/// Idempotency log for inbound provider webhook events (Day 7 G2).
/// Providers can re-deliver events on transient errors; we dedup on (Provider, ProviderEventId).
/// Raw payload kept for forensic debugging if a webhook produced unexpected state.
/// </summary>
public class WebhookEventLog
{
    public Guid Id { get; set; }

    /// <summary>Provider name: "sendgrid" | "brevo" | "mailgun".</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Provider-supplied event ID — unique per event within that provider.
    ///   - SendGrid: sg_event_id
    ///   - Brevo:    message-id + event combo (Brevo doesn't supply a single id field — we compose one)
    ///   - Mailgun:  signature.token (unique per event)
    /// </summary>
    public string ProviderEventId { get; set; } = string.Empty;

    /// <summary>What the event was: "delivered" | "bounced" | "dropped" | "opened" | "clicked" | etc.</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>CampaignMessage this event refers to, if resolution succeeded.</summary>
    public Guid? CampaignMessageId { get; set; }

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Truncated raw payload (jsonb) for debugging. We DON'T keep this forever — purge job can prune &gt; 30 days.</summary>
    public string? RawPayload { get; set; }
}
