using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces.Webhooks;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Infrastructure.Services.Webhooks;

/// <summary>
/// Mailgun Event Webhook handler (Day 7 G2).
///
/// Mailgun v3 payload:
///   { "signature": { "timestamp": "...", "token": "...", "signature": "hex" },
///     "event-data": {
///       "event": "delivered|failed|opened|clicked|...",
///       "id": "...", "timestamp": 12345.67,
///       "user-variables": { "campaign_message_id": "..." },
///       "severity": "permanent|temporary" (for failed),
///       ... } }
///
/// Signature: HMAC-SHA256(timestamp + token, key) where key = WebhookSigningKey from Mailgun dashboard.
/// </summary>
public class MailgunWebhookHandler : IInboundWebhookHandler
{
    private readonly ILogger<MailgunWebhookHandler> _logger;

    public MailgunWebhookHandler(ILogger<MailgunWebhookHandler> logger) { _logger = logger; }

    public string Provider => "mailgun";

    public Task<bool> VerifySignatureAsync(WebhookRequest request, string secret, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogWarning("[Mailgun] Webhook signing key not configured — accepting payload without verification.");
            return Task.FromResult(true);
        }

        try
        {
            using var doc = JsonDocument.Parse(request.BodyText);
            if (!doc.RootElement.TryGetProperty("signature", out var sig)) return Task.FromResult(false);
            var timestamp = sig.GetProperty("timestamp").GetString();
            var token = sig.GetProperty("token").GetString();
            var providedHex = sig.GetProperty("signature").GetString();
            if (timestamp is null || token is null || providedHex is null) return Task.FromResult(false);

            var data = Encoding.UTF8.GetBytes(timestamp + token);
            var key = Encoding.UTF8.GetBytes(secret);
            using var hmac = new HMACSHA256(key);
            var computed = Convert.ToHexString(hmac.ComputeHash(data)).ToLowerInvariant();
            var ok = string.Equals(computed, providedHex.ToLowerInvariant(), StringComparison.Ordinal);
            if (!ok) _logger.LogWarning("[Mailgun] Webhook signature mismatch.");
            return Task.FromResult(ok);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Mailgun] Signature verification crashed.");
            return Task.FromResult(false);
        }
    }

    public Task<IReadOnlyList<NormalizedWebhookEvent>> ParseAsync(WebhookRequest request, CancellationToken ct)
    {
        var results = new List<NormalizedWebhookEvent>();
        try
        {
            using var doc = JsonDocument.Parse(request.BodyText);
            if (!doc.RootElement.TryGetProperty("event-data", out var ev))
                return Task.FromResult<IReadOnlyList<NormalizedWebhookEvent>>(results);

            var eventId = TryGetString(ev, "id") ?? Guid.NewGuid().ToString("N");
            var eventName = (TryGetString(ev, "event") ?? "").ToLowerInvariant();
            var ts = ev.TryGetProperty("timestamp", out var tsEl) && tsEl.ValueKind == JsonValueKind.Number
                ? tsEl.GetDouble() : 0;
            var occurredAt = ts > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds((long)(ts * 1000)).UtcDateTime
                : DateTime.UtcNow;

            Guid? campaignMessageId = null;
            if (ev.TryGetProperty("user-variables", out var vars))
                campaignMessageId = TryGetGuid(vars, "campaign_message_id");

            string? reason = null;
            if (ev.TryGetProperty("delivery-status", out var ds))
                reason = TryGetString(ds, "description") ?? TryGetString(ds, "message");
            reason ??= TryGetString(ev, "reason");

            var severity = TryGetString(ev, "severity")?.ToLowerInvariant();

            results.Add(new NormalizedWebhookEvent
            {
                Provider = Provider,
                ProviderEventId = eventId,
                CampaignMessageId = campaignMessageId,
                EventType = NormalizeEventType(eventName, severity),
                OccurredAt = occurredAt,
                Reason = reason,
                IsHardBounce = eventName == "failed" && severity == "permanent",
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Mailgun] Failed to parse webhook payload.");
        }
        return Task.FromResult<IReadOnlyList<NormalizedWebhookEvent>>(results);
    }

    private static string NormalizeEventType(string mgEvent, string? severity) => mgEvent switch
    {
        "delivered" => "delivered",
        "opened" => "opened",
        "clicked" => "clicked",
        "failed" => severity == "permanent" ? "bounced" : "deferred",
        "rejected" => "dropped",
        "unsubscribed" => "unsubscribed",
        "complained" => "spam_report",
        _ => mgEvent,
    };

    private static string? TryGetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static Guid? TryGetGuid(JsonElement el, string prop)
    {
        var s = TryGetString(el, prop);
        return Guid.TryParse(s, out var g) ? g : null;
    }
}
