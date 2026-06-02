using MarketingApp.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketingApp.API.Controllers;

/// <summary>
/// Meta WhatsApp Cloud API webhook (L3). Two jobs:
///   GET  — the one-time subscription handshake: echo hub.challenge when hub.verify_token matches.
///   POST — inbound messages: hand the raw payload to the ingestion service, always 200 so Meta
///          doesn't retry/disable the webhook.
/// Anonymous: Meta can't send a bearer token. The GET verify-token is the shared secret; POST
/// authenticity can later be hardened with X-Hub-Signature-256 (app secret) once configured.
///
/// Note: a dedicated route ("…/webhooks/whatsapp") wins over the generic
/// "…/webhooks/{provider}" delivery-event route because literal segments outrank route params.
/// </summary>
[ApiController]
[Route("api/v1/webhooks/whatsapp")]
[AllowAnonymous]
public class WhatsAppWebhookController : ControllerBase
{
    private readonly IWhatsAppInboundService _inbound;
    private readonly IConfiguration _config;
    private readonly ILogger<WhatsAppWebhookController> _logger;

    public WhatsAppWebhookController(IWhatsAppInboundService inbound, IConfiguration config, ILogger<WhatsAppWebhookController> logger)
    {
        _inbound = inbound;
        _config = config;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.challenge")] string? challenge,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken)
    {
        var expected = _config["WhatsApp:WebhookVerifyToken"];
        if (string.IsNullOrWhiteSpace(expected)) expected = "marketpro-whatsapp-verify";

        if (mode == "subscribe" && !string.IsNullOrEmpty(challenge) && string.Equals(verifyToken, expected, StringComparison.Ordinal))
        {
            _logger.LogInformation("[WA WEBHOOK] Verification handshake OK");
            return Content(challenge, "text/plain");
        }
        _logger.LogWarning("[WA WEBHOOK] Verification failed (mode={Mode}, tokenMatch={Match})", mode, verifyToken == expected);
        return Unauthorized();
    }

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        string body;
        using (var reader = new StreamReader(Request.Body))
            body = await reader.ReadToEndAsync(ct);

        try
        {
            var count = await _inbound.IngestAsync(body, ct);
            return Ok(new { ingested = count });
        }
        catch (Exception ex)
        {
            // Swallow + 200: Meta retries/disables webhooks that return errors. We logged it.
            _logger.LogError(ex, "[WA WEBHOOK] Ingest failed");
            return Ok(new { ingested = 0 });
        }
    }
}
