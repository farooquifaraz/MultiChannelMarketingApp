using System.Text;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Application.Interfaces.Webhooks;
using MarketingApp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketingApp.API.Controllers;

/// <summary>
/// Inbound provider webhook receiver (Day 7 Feature 1).
/// Anonymous endpoint — security comes from per-provider signature verification using a secret stored
/// on the SmtpGroup. Webhook URL pattern:  POST {PublicBaseUrl}/api/v1/webhooks/{provider}?smtpGroupId={guid}
/// </summary>
[ApiController]
[Route("api/v1/webhooks")]
[AllowAnonymous]
public class WebhooksController : ControllerBase
{
    private readonly IWebhookHandlerFactory _factory;
    private readonly IWebhookProcessor _processor;
    private readonly IGenericRepository<SmtpGroup> _groupRepo;
    private readonly IAuditService _audit;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(
        IWebhookHandlerFactory factory,
        IWebhookProcessor processor,
        IGenericRepository<SmtpGroup> groupRepo,
        IAuditService audit,
        ILogger<WebhooksController> logger)
    {
        _factory = factory;
        _processor = processor;
        _groupRepo = groupRepo;
        _audit = audit;
        _logger = logger;
    }

    [HttpPost("{provider}")]
    public async Task<IActionResult> Receive(
        string provider,
        [FromQuery] Guid? smtpGroupId,
        CancellationToken ct)
    {
        var handler = _factory.Resolve(provider);
        if (handler is null) return NotFound(new { error = $"Unknown webhook provider '{provider}'." });

        // Buffer the raw body so signature verifiers can compute HMAC over it.
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms, ct);
        var rawBody = ms.ToArray();
        var bodyText = Encoding.UTF8.GetString(rawBody);

        var headers = Request.Headers.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);
        var query = Request.Query.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);

        var wr = new WebhookRequest
        {
            RawBody = rawBody,
            BodyText = bodyText,
            Headers = headers,
            Query = query,
        };

        // Look up secret for this group + provider. If smtpGroupId is omitted (some providers don't let
        // us round-trip query params), fall back to the platform's default SmtpGroup secret.
        string? secret = null;
        if (smtpGroupId.HasValue)
        {
            var group = await _groupRepo.GetByIdAsync(smtpGroupId.Value, ct);
            secret = PickSecret(group, provider);
        }
        if (string.IsNullOrEmpty(secret))
        {
            var defaultGroup = (await _groupRepo.FindAsync(g => g.IsDefault, ct)).FirstOrDefault();
            secret = PickSecret(defaultGroup, provider);
        }

        var verified = await handler.VerifySignatureAsync(wr, secret ?? string.Empty, ct);
        if (!verified)
        {
            await _audit.LogAsync(null, "WebhookSignatureFailed", "Webhook", null,
                details: new { provider, smtpGroupId }, ct: ct);
            return Unauthorized(new { error = "Webhook signature verification failed." });
        }

        var events = await handler.ParseAsync(wr, ct);
        if (events.Count == 0)
        {
            _logger.LogInformation("[{Provider}] Webhook payload contained no events.", provider);
            return Ok(new { applied = 0 });
        }

        var applied = await _processor.ProcessAsync(events, ct);
        _logger.LogInformation("[{Provider}] Applied {Applied}/{Total} webhook events.", provider, applied, events.Count);
        return Ok(new { applied, received = events.Count });
    }

    private static string? PickSecret(SmtpGroup? g, string provider) => g is null ? null : provider switch
    {
        "sendgrid" => g.SendGridWebhookSecret,
        "brevo" => g.BrevoWebhookSecret,
        "mailgun" => g.MailgunWebhookSecret,
        _ => null,
    };
}
