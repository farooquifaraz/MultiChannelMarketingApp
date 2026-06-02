using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Application.Interfaces.Webhooks;
using MarketingApp.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Application.Services.Webhooks;

/// <summary>
/// Applies normalized provider events to the database (Day 7 G2).
/// Dedup via WebhookEventLog (Provider, ProviderEventId). Updates CampaignMessage delivery/bounce/open/click
/// state, auto-flags hard-bounced contacts (reuses Day 6 F2 logic), emits audit log entries.
///
/// Idempotent + order-tolerant — provider can deliver events out of order or re-deliver,
/// we never apply a "lower-progressed" status over a "higher-progressed" one (e.g. clicked → delivered).
/// </summary>
public class WebhookProcessor : IWebhookProcessor
{
    private readonly IGenericRepository<WebhookEventLog> _eventLogRepo;
    private readonly IGenericRepository<CampaignMessage> _messageRepo;
    private readonly IGenericRepository<Contact> _contactRepo;
    private readonly IAuditService _audit;
    private readonly ILogger<WebhookProcessor> _logger;

    public WebhookProcessor(
        IGenericRepository<WebhookEventLog> eventLogRepo,
        IGenericRepository<CampaignMessage> messageRepo,
        IGenericRepository<Contact> contactRepo,
        IAuditService audit,
        ILogger<WebhookProcessor> logger)
    {
        _eventLogRepo = eventLogRepo;
        _messageRepo = messageRepo;
        _contactRepo = contactRepo;
        _audit = audit;
        _logger = logger;
    }

    public async Task<int> ProcessAsync(IReadOnlyList<NormalizedWebhookEvent> events, CancellationToken ct)
    {
        var applied = 0;
        foreach (var evt in events)
        {
            try
            {
                // --- Idempotency: skip if we've already logged this exact event ---
                var existing = await _eventLogRepo.FindAsync(
                    e => e.Provider == evt.Provider && e.ProviderEventId == evt.ProviderEventId, ct);
                if (existing.Any())
                {
                    _logger.LogDebug("Webhook event {Provider}/{EventId} already processed — skipping.", evt.Provider, evt.ProviderEventId);
                    continue;
                }

                // --- Persist the log entry FIRST (so concurrent re-deliveries collide on the unique index) ---
                await _eventLogRepo.AddAsync(new WebhookEventLog
                {
                    Provider = evt.Provider,
                    ProviderEventId = evt.ProviderEventId,
                    EventType = evt.EventType,
                    CampaignMessageId = evt.CampaignMessageId,
                    ReceivedAt = DateTime.UtcNow,
                }, ct);

                // --- Resolve the target CampaignMessage ---
                // Primary: explicit id the provider echoed back (tags / X-Mailin-custom / custom args).
                // Fallback: recipient email → most recent SENT message for that address. The fallback is
                // what lets webhooks land for emails sent before tag-based correlation existed, and for
                // Brevo event types that drop the correlation id.
                var message = await ResolveMessageAsync(evt, ct);
                if (message is null)
                {
                    _logger.LogWarning(
                        "Webhook {Provider}/{EventType} could not be correlated to a CampaignMessage (id={MsgId}, email={Email}) — logged only.",
                        evt.Provider, evt.EventType, evt.CampaignMessageId, evt.RecipientEmail);
                    applied++; // event is recorded in WebhookEventLog; there's just nothing to update
                    continue;
                }
                var msgId = message.Id;

                ApplyEventToMessage(message, evt);
                await _messageRepo.UpdateAsync(message, ct);

                // --- Auto-flag contact on hard bounce (reuses Day 6 F2 semantics) ---
                if (evt.IsHardBounce && message.ContactId != Guid.Empty)
                {
                    var contact = await _contactRepo.GetByIdAsync(message.ContactId, ct);
                    if (contact is not null && !contact.IsBounced)
                    {
                        contact.IsBounced = true;
                        contact.BouncedAt = DateTime.UtcNow;
                        contact.BounceReason = Truncate(evt.Reason ?? $"Hard bounce reported by {evt.Provider}", 500);
                        await _contactRepo.UpdateAsync(contact, ct);
                    }
                }

                // --- Audit ---
                await _audit.LogAsync(
                    userId: null,
                    action: $"Email{Capitalize(evt.EventType)}",
                    entity: "CampaignMessage",
                    entityId: msgId,
                    details: new { evt.Provider, evt.EventType, evt.Reason },
                    ct: ct);

                applied++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process webhook event {Provider}/{EventId}", evt.Provider, evt.ProviderEventId);
                // continue with next event — one bad event shouldn't kill the batch
            }
        }
        return applied;
    }

    /// <summary>
    /// Resolve the CampaignMessage a webhook event belongs to. Explicit id wins; otherwise fall back to
    /// the recipient email and pick the most recently SENT message to that address. The email fallback is
    /// intentionally fuzzy (a contact could appear in several campaigns) but "most recent send" is the
    /// right heuristic for delivery/open/click signals, and it's the only way to correlate events for
    /// emails that went out before we started stamping correlation tags.
    /// </summary>
    private async Task<CampaignMessage?> ResolveMessageAsync(NormalizedWebhookEvent evt, CancellationToken ct)
    {
        if (evt.CampaignMessageId is Guid id)
        {
            var byId = await _messageRepo.GetByIdAsync(id, ct);
            if (byId is not null) return byId;
            _logger.LogWarning("Webhook references unknown CampaignMessageId {MsgId} — trying email fallback.", id);
        }

        if (string.IsNullOrWhiteSpace(evt.RecipientEmail)) return null;

        var email = evt.RecipientEmail.Trim().ToLowerInvariant();
        var candidates = await _messageRepo.FindAsync(
            m => m.SentAt != null
                 && m.Contact != null
                 && m.Contact.Email != null
                 && m.Contact.Email.ToLower() == email,
            ct);

        return candidates.OrderByDescending(m => m.SentAt).FirstOrDefault();
    }

    private static void ApplyEventToMessage(CampaignMessage message, NormalizedWebhookEvent evt)
    {
        switch (evt.EventType)
        {
            case "delivered":
                // Upgrade SMTP best-effort -> webhook-confirmed.
                if (message.DeliveredAt is null) message.DeliveredAt = evt.OccurredAt;
                message.DeliveryConfirmationKind = "webhook";
                // Don't downgrade higher-progressed statuses (opened/clicked).
                if (message.Status == "sent") message.Status = "delivered";
                break;

            case "opened":
                if (message.OpenedAt is null) message.OpenedAt = evt.OccurredAt;
                if (message.Status is "sent" or "delivered") message.Status = "opened";
                break;

            case "clicked":
                if (message.ClickedAt is null) message.ClickedAt = evt.OccurredAt;
                if (message.OpenedAt is null) message.OpenedAt = evt.OccurredAt;
                message.ClickCount += 1;
                if (message.Status is "sent" or "delivered" or "opened") message.Status = "clicked";
                break;

            case "bounced":
            case "dropped":
                message.Status = "bounced";
                message.ErrorMessage = evt.Reason ?? evt.EventType;
                break;

            case "deferred":
                // Provider is retrying — don't update status, just log via WebhookEventLog.
                break;

            case "unsubscribed":
            case "spam_report":
                // Future: hook into a per-contact preference / suppression list. Not in v1.
                break;

            default:
                // Unknown future event type — log only, don't touch state.
                break;
        }
    }

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max];
}
