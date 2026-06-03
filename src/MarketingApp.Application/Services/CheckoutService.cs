using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Application.Interfaces.Billing;
using MarketingApp.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Application.Services;

/// <summary>
/// Upgrade → checkout → activate orchestration (P2.2). Default provider is "mock" which activates the
/// plan immediately so the whole loop works with no payment keys. Switching SystemSettings.PaymentProvider
/// to "stripe" (+ keys) routes through real hosted checkout + signed webhook with NO code change here.
/// </summary>
public class CheckoutService : ICheckoutService
{
    private readonly IBillingProviderFactory _factory;
    private readonly IBillingService _billing;
    private readonly ISystemSettingsService _settings;
    private readonly IAuditService _audit;
    private readonly ILogger<CheckoutService> _logger;

    public CheckoutService(
        IBillingProviderFactory factory,
        IBillingService billing,
        ISystemSettingsService settings,
        IAuditService audit,
        ILogger<CheckoutService> logger)
    {
        _factory = factory;
        _billing = billing;
        _settings = settings;
        _audit = audit;
        _logger = logger;
    }

    public async Task<CheckoutResultDto> StartCheckoutAsync(Guid userId, StartCheckoutDto dto, CancellationToken ct = default)
    {
        var planCode = dto.PlanCode?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrEmpty(planCode))
            throw new AppValidationException("Plan code is required.");
        if (planCode == "free")
            throw new AppValidationException("The Free plan does not require checkout — use plan change instead.");

        var plan = await _billing.GetPlanAsync(planCode, ct)
            ?? throw new AppValidationException($"Unknown plan '{planCode}'.");

        var settings = await _settings.GetAsync(ct);
        var providerKey = string.IsNullOrWhiteSpace(settings.PaymentProvider) ? "mock" : settings.PaymentProvider;
        if (string.Equals(providerKey, "disabled", StringComparison.OrdinalIgnoreCase))
            throw new AppValidationException("Payments are disabled. Enable a provider in admin settings.");

        var provider = _factory.Resolve(providerKey) ?? _factory.Resolve("mock")
            ?? throw new AppValidationException("No payment provider is available.");

        var isMock = string.Equals(provider.Provider, "mock", StringComparison.OrdinalIgnoreCase);
        var apiKey = isMock ? null : (await _settings.GetRawPaymentSecretsAsync(ct)).ApiKey;

        var successUrl = string.IsNullOrWhiteSpace(dto.SuccessUrl) ? "/billing?checkout=success" : dto.SuccessUrl!;
        var cancelUrl = string.IsNullOrWhiteSpace(dto.CancelUrl) ? "/billing?checkout=cancel" : dto.CancelUrl!;

        var request = new CheckoutRequest(userId, plan.Code, plan.Name, plan.PriceAedMonthly,
            successUrl, cancelUrl, apiKey, settings.PaymentBaseUrl);

        var result = await provider.CreateCheckoutAsync(request, ct);
        if (!result.IsSuccess)
            throw new AppValidationException(result.Error ?? "Checkout could not be started.");

        if (result.ActivatedImmediately)
        {
            await _billing.ActivatePaidPlanAsync(userId, plan.Code, result.ExternalSessionId, null, ct);
            await _audit.LogAsync(userId, "CheckoutActivated", "Subscription", null, new { plan.Code, provider = provider.Provider }, ct: ct);
            return new CheckoutResultDto { Provider = provider.Provider, PlanCode = plan.Code, Activated = true, CheckoutUrl = successUrl };
        }

        await _audit.LogAsync(userId, "CheckoutStarted", "Subscription", null, new { plan.Code, provider = provider.Provider }, ct: ct);
        return new CheckoutResultDto { Provider = provider.Provider, PlanCode = plan.Code, Activated = false, CheckoutUrl = result.CheckoutUrl };
    }

    public async Task<bool> HandleWebhookAsync(string provider, string payload, string? signature, CancellationToken ct = default)
    {
        var p = _factory.Resolve(provider);
        if (p is null)
        {
            _logger.LogWarning("Payment webhook for unknown provider '{Provider}' ignored.", provider);
            return false;
        }

        var (_, webhookSecret) = await _settings.GetRawPaymentSecretsAsync(ct);
        var result = await p.HandleWebhookAsync(payload, signature, webhookSecret, ct);

        if (result.ShouldActivate && result.UserId is Guid uid && !string.IsNullOrEmpty(result.PlanCode))
        {
            await _billing.ActivatePaidPlanAsync(uid, result.PlanCode!, result.ExternalCustomerId, result.ExternalSubscriptionId, ct);
            await _audit.LogAsync(uid, "CheckoutWebhookActivated", "Subscription", null,
                new { result.PlanCode, provider = p.Provider, result.EventType }, ct: ct);
            _logger.LogInformation("Webhook activated plan {Plan} for user {UserId} via {Provider}", result.PlanCode, uid, p.Provider);
        }
        else if (!string.IsNullOrEmpty(result.Error))
        {
            _logger.LogWarning("Payment webhook ({Provider}) not actioned: {Error}", p.Provider, result.Error);
        }

        return result.Handled;
    }
}
