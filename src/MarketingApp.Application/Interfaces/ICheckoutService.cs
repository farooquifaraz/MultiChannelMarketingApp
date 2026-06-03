using MarketingApp.Application.DTOs;

namespace MarketingApp.Application.Interfaces;

/// <summary>
/// Drives the upgrade → checkout → activate loop (P2.2). Resolves the configured payment provider
/// (mock by default → activates immediately, so the loop works keyless) and applies confirmed
/// payments to the user's subscription. Real providers (Stripe) redirect to hosted checkout and
/// activate via a signed webhook.
/// </summary>
public interface ICheckoutService
{
    Task<CheckoutResultDto> StartCheckoutAsync(Guid userId, StartCheckoutDto dto, CancellationToken ct = default);

    /// <summary>Process a provider webhook. Returns true if handled. Never throws on bad input.</summary>
    Task<bool> HandleWebhookAsync(string provider, string payload, string? signature, CancellationToken ct = default);
}
