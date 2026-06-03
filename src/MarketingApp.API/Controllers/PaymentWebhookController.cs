using System.Text;
using MarketingApp.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketingApp.API.Controllers;

/// <summary>
/// Payment provider webhook receiver (P2.2). Anonymous — security comes from per-provider signature
/// verification (e.g. the Stripe-Signature header checked against the configured webhook secret).
/// URL: POST {PublicBaseUrl}/api/v1/webhooks/payments/{provider}
/// Always returns 200 so providers don't retry-storm; the body indicates whether it was actioned.
/// </summary>
[ApiController]
[Route("api/v1/webhooks/payments")]
[AllowAnonymous]
public class PaymentWebhookController : ControllerBase
{
    private readonly ICheckoutService _checkout;
    private readonly ILogger<PaymentWebhookController> _logger;

    public PaymentWebhookController(ICheckoutService checkout, ILogger<PaymentWebhookController> logger)
    {
        _checkout = checkout;
        _logger = logger;
    }

    [HttpPost("{provider}")]
    public async Task<IActionResult> Receive(string provider, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms, ct);
        var payload = Encoding.UTF8.GetString(ms.ToArray());

        // Stripe sends the signature in "Stripe-Signature". Accept a generic header too.
        var signature = Request.Headers.TryGetValue("Stripe-Signature", out var s)
            ? s.ToString()
            : Request.Headers.TryGetValue("X-Webhook-Signature", out var x) ? x.ToString() : null;

        try
        {
            var handled = await _checkout.HandleWebhookAsync(provider, payload, signature, ct);
            return Ok(new { handled });
        }
        catch (Exception ex)
        {
            // Never surface a 500 to a payment provider — log + ack.
            _logger.LogError(ex, "Payment webhook ({Provider}) threw", provider);
            return Ok(new { handled = false });
        }
    }
}
