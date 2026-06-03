using MarketingApp.Application.DTOs;

namespace MarketingApp.Application.Interfaces;

/// <summary>Plans + subscriptions + live usage (Phase 2). Payment wiring (Stripe/Telr) lands later.</summary>
public interface IBillingService
{
    /// <summary>Active pricing tiers, ordered for the pricing page.</summary>
    Task<IEnumerable<PlanDto>> ListPlansAsync(CancellationToken ct = default);

    /// <summary>
    /// The user's current subscription + live usage for this period. Auto-provisions a Free
    /// subscription on first access so every user always has one.
    /// </summary>
    Task<SubscriptionDto> GetMySubscriptionAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Switch the user to a different plan code (no payment yet — manual/admin or post-Stripe).
    /// Throws if the plan code doesn't exist.
    /// </summary>
    Task<SubscriptionDto> ChangePlanAsync(Guid userId, string planCode, CancellationToken ct = default);

    /// <summary>A single plan by code (or null). Used by the checkout flow for price/name.</summary>
    Task<PlanDto?> GetPlanAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// Activate a paid plan after a confirmed payment, recording the provider's external ids
    /// (P2.2). Idempotent — safe to call again from a retried webhook.
    /// </summary>
    Task<SubscriptionDto> ActivatePaidPlanAsync(Guid userId, string planCode,
        string? externalCustomerId, string? externalSubscriptionId, CancellationToken ct = default);
}
