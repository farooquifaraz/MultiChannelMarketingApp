using System.Text.Json;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces.Webhooks;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Infrastructure.Services.Webhooks;

/// <summary>
/// Brevo (Sendinblue) Transactional Webhook handler (Day 7 G2).
///
/// Brevo posts a SINGLE event per request (not an array). Shape:
///   { "event": "delivered|hard_bounce|soft_bounce|opened|clicked|...",
///     "email": "...", "message-id": "&lt;sg.xxx@brevo.com&gt;",
///     "date": "2026-05-30 12:34:56", ... custom headers we attached ride at top level too ... }
///
/// Brevo doesn't provide an event ID. We compose one from (message-id + event + date) to dedup.
///
/// Signature: Brevo doesn't support webhook signatures yet. We use a shared-bearer-via-URL pattern:
/// admin configures webhook URL as `.../webhooks/brevo?token={secret}` and we verify the token.
/// </summary>
public class BrevoWebhookHandler : IInboundWebhookHandler
{
    private readonly ILogger<BrevoWebhookHandler> _logger;

    public BrevoWebhookHandler(ILogger<BrevoWebhookHandler> logger) { _logger = logger; }

    public string Provider => "brevo";

    public Task<bool> VerifySignatureAsync(WebhookRequest request, string secret, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogWarning("[Brevo] Webhook secret not configured — accepting payload without verification.");
            return Task.FromResult(true);
        }
        if (request.Query.TryGetValue("token", out var token) && string.Equals(token, secret, StringComparison.Ordinal))
            return Task.FromResult(true);
        _logger.LogWarning("[Brevo] Webhook token query param missing or mismatched.");
        return Task.FromResult(false);
    }

    public Task<IReadOnlyList<NormalizedWebhookEvent>> ParseAsync(WebhookRequest request, CancellationToken ct)
    {
        var results = new List<NormalizedWebhookEvent>();
        try
        {
            using var doc = JsonDocument.Parse(request.BodyText);
            var root = doc.RootElement;
            var eventName = (TryGetString(root, "event") ?? "").ToLowerInvariant();
            var brevoMessageId = TryGetString(root, "message-id") ?? TryGetString(root, "messageId") ?? "";
            var dateStr = TryGetString(root, "date") ?? "";
            var occurredAt = DateTime.TryParse(dateStr, out var d) ? d.ToUniversalTime() : DateTime.UtcNow;

            // We attached X-Campaign-Message-Id as a custom header — Brevo echoes it under "X-Campaign-Message-Id".
            var campaignMessageId = TryGetGuid(root, "X-Campaign-Message-Id");

            var providerEventId = $"{brevoMessageId}:{eventName}:{occurredAt:O}";
            var reason = TryGetString(root, "reason");

            results.Add(new NormalizedWebhookEvent
            {
                Provider = Provider,
                ProviderEventId = providerEventId,
                CampaignMessageId = campaignMessageId,
                EventType = NormalizeEventType(eventName),
                OccurredAt = occurredAt,
                Reason = reason,
                IsHardBounce = eventName == "hard_bounce" || eventName == "blocked",
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Brevo] Failed to parse webhook payload.");
        }
        return Task.FromResult<IReadOnlyList<NormalizedWebhookEvent>>(results);
    }

    private static string NormalizeEventType(string brevoEvent) => brevoEvent switch
    {
        "delivered" => "delivered",
        "opened" or "unique_opened" => "opened",
        "click" or "clicked" => "clicked",
        "hard_bounce" => "bounced",
        "soft_bounce" => "deferred",
        "blocked" => "dropped",
        "deferred" => "deferred",
        "unsubscribed" => "unsubscribed",
        "complaint" or "spam" => "spam_report",
        _ => brevoEvent,
    };

    private static string? TryGetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static Guid? TryGetGuid(JsonElement el, string prop)
    {
        var s = TryGetString(el, prop);
        return Guid.TryParse(s, out var g) ? g : null;
    }
}
