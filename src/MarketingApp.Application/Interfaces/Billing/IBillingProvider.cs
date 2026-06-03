namespace MarketingApp.Application.Interfaces.Billing;

/// <summary>
/// Strategy contract for a payment provider (Phase 2 / P2.2) — mirrors the AI / image client pattern.
/// Implementations:
///   - MockBillingProvider   (offline; "activates" the plan immediately so the upgrade loop works keyless)
///   - StripeBillingProvider (real Stripe Checkout + signed webhook; activates when a key is configured)
///   - (future) TelrBillingProvider, RazorpayBillingProvider
/// Adding a new provider = drop one IBillingProvider + 1 DI line.
/// </summary>
public interface IBillingProvider
{
    /// <summary>Provider key — matched against SystemSettings.PaymentProvider. e.g. "mock", "stripe".</summary>
    string Provider { get; }

    /// <summary>Begin a checkout for the given plan. Returns either a redirect URL (real providers) or
    /// an immediate activation (mock).</summary>
    Task<CheckoutResult> CreateCheckoutAsync(CheckoutRequest request, CancellationToken ct);

    /// <summary>Verify + parse a provider webhook payload into a normalized result.</summary>
    Task<WebhookResult> HandleWebhookAsync(string payload, string? signature, string? webhookSecret, CancellationToken ct);
}

public sealed record CheckoutRequest(
    Guid UserId,
    string PlanCode,
    string PlanName,
    decimal PriceAedMonthly,
    string SuccessUrl,
    string CancelUrl,
    string? ApiKey,
    string? BaseUrl);

public sealed record CheckoutResult(
    bool IsSuccess,
    /// <summary>True for the mock provider — the caller should activate the plan right away.</summary>
    bool ActivatedImmediately,
    string? CheckoutUrl,
    string? ExternalSessionId,
    string? Error);

public sealed record WebhookResult(
    bool Handled,
    /// <summary>True only when this event means "activate the paid plan now".</summary>
    bool ShouldActivate,
    Guid? UserId,
    string? PlanCode,
    string? ExternalCustomerId,
    string? ExternalSubscriptionId,
    string? EventType,
    string? Error);

public interface IBillingProviderFactory
{
    /// <summary>Returns the provider registered for the given key, or null if none / disabled.</summary>
    IBillingProvider? Resolve(string provider);
}
