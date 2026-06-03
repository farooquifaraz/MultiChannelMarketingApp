using MarketingApp.Application.Interfaces.Billing;

namespace MarketingApp.Infrastructure.Services.Billing;

/// <summary>
/// Offline payment provider (Phase 2 / P2.2). Simulates a successful checkout: returns
/// ActivatedImmediately=true so the caller activates the plan right away. This makes the full
/// upgrade→activate loop work end-to-end with NO payment keys (dev / demo). Default provider.
/// </summary>
public class MockBillingProvider : IBillingProvider
{
    public string Provider => "mock";

    public Task<CheckoutResult> CreateCheckoutAsync(CheckoutRequest request, CancellationToken ct)
    {
        // Deterministic fake session id (no random/time — keeps things testable/replayable).
        var sessionId = $"mock_sess_{request.UserId:N}_{request.PlanCode}";
        return Task.FromResult(new CheckoutResult(
            IsSuccess: true,
            ActivatedImmediately: true,
            CheckoutUrl: request.SuccessUrl,
            ExternalSessionId: sessionId,
            Error: null));
    }

    public Task<WebhookResult> HandleWebhookAsync(string payload, string? signature, string? webhookSecret, CancellationToken ct) =>
        // Mock activates synchronously at checkout, so there is no async webhook to honor.
        Task.FromResult(new WebhookResult(true, false, null, null, null, null, "mock.noop", null));
}
