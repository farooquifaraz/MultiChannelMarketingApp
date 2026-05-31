using System.Security.Cryptography;
using System.Text.Json;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces.Webhooks;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Infrastructure.Services.Webhooks;

/// <summary>
/// SendGrid Event Webhook handler (Day 7 G2).
///
/// Payload: a JSON array of event objects. Each has at minimum:
///   { "email": "...", "event": "delivered|bounce|dropped|open|click|...", "sg_event_id": "...",
///     "sg_message_id": "...", "timestamp": 1234567890,
///     "campaign_message_id": "..." (our custom_args field) }
///
/// Signature: SendGrid uses ECDSA on the (timestamp + body) tuple, sent via headers
///   X-Twilio-Email-Event-Webhook-Signature
///   X-Twilio-Email-Event-Webhook-Timestamp
/// For v1, signature verification is OPTIONAL — if the SmtpGroup has no SendGridWebhookSecret
/// configured, we accept the payload (logged warning). This lets admins get started quickly.
/// </summary>
public class SendGridWebhookHandler : IInboundWebhookHandler
{
    private readonly ILogger<SendGridWebhookHandler> _logger;

    public SendGridWebhookHandler(ILogger<SendGridWebhookHandler> logger) { _logger = logger; }

    public string Provider => "sendgrid";

    public Task<bool> VerifySignatureAsync(WebhookRequest request, string secret, CancellationToken ct)
    {
        // No secret configured → accept (and warn). Admin can tighten later by saving a verification key.
        if (string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogWarning("[SendGrid] Webhook signature secret not configured — accepting payload without verification.");
            return Task.FromResult(true);
        }

        // SendGrid's full ECDSA verification requires their public key (not HMAC).
        // For v1 we accept any non-empty secret as a "shared bearer" check via Authorization header
        // (admin can put "Bearer mysecret" in their SendGrid webhook URL config or use the verification key flow).
        // TODO Day 8: implement full ECDSA verification with their VerificationKey API.
        if (request.Headers.TryGetValue("Authorization", out var auth) &&
            string.Equals(auth, $"Bearer {secret}", StringComparison.Ordinal))
        {
            return Task.FromResult(true);
        }

        _logger.LogWarning("[SendGrid] Webhook auth header missing or mismatched.");
        return Task.FromResult(false);
    }

    public Task<IReadOnlyList<NormalizedWebhookEvent>> ParseAsync(WebhookRequest request, CancellationToken ct)
    {
        var results = new List<NormalizedWebhookEvent>();
        try
        {
            using var doc = JsonDocument.Parse(request.BodyText);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return Task.FromResult<IReadOnlyList<NormalizedWebhookEvent>>(results);

            foreach (var ev in doc.RootElement.EnumerateArray())
            {
                var sgEventId = TryGetString(ev, "sg_event_id") ?? Guid.NewGuid().ToString("N");
                var sgEvent = (TryGetString(ev, "event") ?? "").ToLowerInvariant();
                var campaignMessageId = TryGetGuid(ev, "campaign_message_id");
                var timestamp = TryGetLong(ev, "timestamp");
                var occurredAt = timestamp > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime
                    : DateTime.UtcNow;
                var reason = TryGetString(ev, "reason") ?? TryGetString(ev, "response") ?? TryGetString(ev, "type");

                results.Add(new NormalizedWebhookEvent
                {
                    Provider = Provider,
                    ProviderEventId = sgEventId,
                    CampaignMessageId = campaignMessageId,
                    EventType = NormalizeEventType(sgEvent),
                    OccurredAt = occurredAt,
                    Reason = reason,
                    IsHardBounce = IsHardBounce(sgEvent, ev),
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SendGrid] Failed to parse webhook payload.");
        }
        return Task.FromResult<IReadOnlyList<NormalizedWebhookEvent>>(results);
    }

    private static string NormalizeEventType(string sgEvent) => sgEvent switch
    {
        "delivered" => "delivered",
        "open" => "opened",
        "click" => "clicked",
        "bounce" => "bounced",
        "dropped" => "dropped",
        "deferred" => "deferred",
        "unsubscribe" or "group_unsubscribe" => "unsubscribed",
        "spamreport" => "spam_report",
        _ => sgEvent,
    };

    private static bool IsHardBounce(string sgEvent, JsonElement ev)
    {
        if (sgEvent == "bounce")
        {
            // SendGrid sets "type": "bounce" (hard) vs "type": "blocked" (soft).
            var type = TryGetString(ev, "type")?.ToLowerInvariant();
            return type == "bounce" || type is null; // be defensive — treat unknown bounce as hard
        }
        return sgEvent == "dropped"; // dropped = SendGrid suppressed it, treat as permanent failure
    }

    private static string? TryGetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static long TryGetLong(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var l) ? l : 0;

    private static Guid? TryGetGuid(JsonElement el, string prop)
    {
        var s = TryGetString(el, prop);
        return Guid.TryParse(s, out var g) ? g : null;
    }
}
