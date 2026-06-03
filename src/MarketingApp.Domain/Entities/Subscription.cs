namespace MarketingApp.Domain.Entities;

/// <summary>
/// A user's current plan (Phase 2). For now scoped to a User (the "Agency Model"); when true
/// multi-tenancy lands this can move to an Organization. One active row per user — a new Free
/// subscription is auto-provisioned on first access.
/// </summary>
public class Subscription
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Plan code this subscription is on (free | starter | …). FK-by-code to Plan.</summary>
    public string PlanCode { get; set; } = "free";

    /// <summary>active | cancelled | past_due. Free is always "active".</summary>
    public string Status { get; set; } = "active";

    /// <summary>Current billing period (UTC). For Free, a rolling monthly window.</summary>
    public DateTime CurrentPeriodStart { get; set; } = DateTime.UtcNow;
    public DateTime CurrentPeriodEnd { get; set; } = DateTime.UtcNow.AddMonths(1);

    /// <summary>Set later by the payment provider (Stripe/Telr) when paid billing is wired.</summary>
    public string? ExternalCustomerId { get; set; }
    public string? ExternalSubscriptionId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
