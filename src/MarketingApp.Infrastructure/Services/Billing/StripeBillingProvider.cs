using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MarketingApp.Application.Interfaces.Billing;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Infrastructure.Services.Billing;

/// <summary>
/// Stripe Checkout provider (Phase 2 / P2.2). Creates a hosted subscription Checkout Session via the
/// Stripe REST API (no SDK dependency — plain form-POST) and verifies the signed webhook that confirms
/// payment. Activates only when SystemSettings.PaymentProvider="stripe" + a secret key is configured;
/// until then the factory resolves to the mock provider so keyless operation is never blocked.
/// The request-builder, signature computation, and event parser are pure + unit-tested.
/// </summary>
public class StripeBillingProvider : IBillingProvider
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<StripeBillingProvider> _logger;

    public StripeBillingProvider(IHttpClientFactory httpFactory, ILogger<StripeBillingProvider> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public string Provider => "stripe";

    public async Task<CheckoutResult> CreateCheckoutAsync(CheckoutRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey))
            return new CheckoutResult(false, false, null, null, "Stripe secret key missing — set it in admin Payment settings.");

        try
        {
            var client = _httpFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", request.ApiKey);
            var baseUrl = string.IsNullOrWhiteSpace(request.BaseUrl) ? "https://api.stripe.com" : request.BaseUrl!.TrimEnd('/');

            var form = new FormUrlEncodedContent(BuildCheckoutForm(request));
            var resp = await client.PostAsync($"{baseUrl}/v1/checkout/sessions", form, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Stripe checkout failed: {Status} {Body}", resp.StatusCode, body);
                return new CheckoutResult(false, false, null, null, $"Stripe returned {(int)resp.StatusCode}.");
            }

            using var doc = JsonDocument.Parse(body);
            var url = doc.RootElement.TryGetProperty("url", out var u) ? u.GetString() : null;
            var id = doc.RootElement.TryGetProperty("id", out var i) ? i.GetString() : null;
            if (string.IsNullOrEmpty(url))
                return new CheckoutResult(false, false, null, id, "Stripe response had no checkout url.");

            return new CheckoutResult(true, false, url, id, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stripe checkout threw");
            return new CheckoutResult(false, false, null, null, ex.Message);
        }
    }

    public Task<WebhookResult> HandleWebhookAsync(string payload, string? signature, string? webhookSecret, CancellationToken ct)
    {
        // Verify the Stripe-Signature header when a webhook secret is configured.
        if (!string.IsNullOrWhiteSpace(webhookSecret))
        {
            if (!VerifySignature(payload, signature, webhookSecret!, out var verr))
                return Task.FromResult(new WebhookResult(false, false, null, null, null, null, null, verr));
        }

        try
        {
            return Task.FromResult(ParseEvent(payload));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new WebhookResult(false, false, null, null, null, null, null, ex.Message));
        }
    }

    // ---- pure, unit-tested helpers ----

    /// <summary>Builds the Stripe Checkout Session form for a monthly subscription using inline
    /// price_data (so no pre-created Stripe Price IDs are required). Pure + deterministic.</summary>
    internal static IEnumerable<KeyValuePair<string, string>> BuildCheckoutForm(CheckoutRequest r)
    {
        var unitAmountFils = ((long)Math.Round(r.PriceAedMonthly * 100m)).ToString(CultureInfo.InvariantCulture);
        return new Dictionary<string, string>
        {
            ["mode"] = "subscription",
            ["success_url"] = r.SuccessUrl,
            ["cancel_url"] = r.CancelUrl,
            ["client_reference_id"] = r.UserId.ToString(),
            ["metadata[plan_code]"] = r.PlanCode,
            ["line_items[0][quantity]"] = "1",
            ["line_items[0][price_data][currency]"] = "aed",
            ["line_items[0][price_data][unit_amount]"] = unitAmountFils,
            ["line_items[0][price_data][recurring][interval]"] = "month",
            ["line_items[0][price_data][product_data][name]"] = r.PlanName,
        };
    }

    /// <summary>Stripe signature scheme: HMAC-SHA256 over "{t}.{payload}" with the webhook secret,
    /// compared against the v1 scheme in the Stripe-Signature header (t=...,v1=...).</summary>
    internal static bool VerifySignature(string payload, string? sigHeader, string secret, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(sigHeader)) { error = "missing signature header"; return false; }
        string? t = null, v1 = null;
        foreach (var part in sigHeader.Split(','))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            if (kv[0].Trim() == "t") t = kv[1].Trim();
            else if (kv[0].Trim() == "v1") v1 = kv[1].Trim();
        }
        if (t is null || v1 is null) { error = "malformed signature header"; return false; }

        var expected = ComputeSignature(t, payload, secret);
        // Constant-time compare.
        var a = Encoding.UTF8.GetBytes(expected);
        var b = Encoding.UTF8.GetBytes(v1);
        if (!CryptographicOperations.FixedTimeEquals(a, b)) { error = "signature mismatch"; return false; }
        return true;
    }

    /// <summary>Lowercase-hex HMAC-SHA256 of "{timestamp}.{payload}". Pure → testable.</summary>
    internal static string ComputeSignature(string timestamp, string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>Parses a checkout.session.completed event into a normalized WebhookResult. Other
    /// event types are acknowledged but not actioned. Pure → testable.</summary>
    internal static WebhookResult ParseEvent(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
        if (type != "checkout.session.completed")
            return new WebhookResult(true, false, null, null, null, null, type, null);

        if (!root.TryGetProperty("data", out var data) || !data.TryGetProperty("object", out var obj))
            return new WebhookResult(true, false, null, null, null, null, type, "no session object");

        Guid? userId = null;
        if (obj.TryGetProperty("client_reference_id", out var cr) && Guid.TryParse(cr.GetString(), out var g))
            userId = g;

        string? planCode = null;
        if (obj.TryGetProperty("metadata", out var meta) && meta.TryGetProperty("plan_code", out var pc))
            planCode = pc.GetString();

        string? customer = obj.TryGetProperty("customer", out var c) ? c.GetString() : null;
        string? subscription = obj.TryGetProperty("subscription", out var s) ? s.GetString() : null;

        var activate = userId is not null && !string.IsNullOrEmpty(planCode);
        return new WebhookResult(true, activate, userId, planCode, customer, subscription, type,
            activate ? null : "missing client_reference_id / plan_code");
    }
}
