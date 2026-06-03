namespace MarketingApp.Domain.Entities;

/// <summary>
/// A subscription plan / pricing tier (Phase 2). Seeded from the roadmap (AED-anchored). Limits of
/// -1 mean "unlimited". A user's <see cref="Subscription"/> points at one of these by Code.
/// </summary>
public class Plan
{
    public Guid Id { get; set; }

    /// <summary>Stable code: free | starter | pro | business | agency. Used by Subscription.PlanCode.</summary>
    public string Code { get; set; } = "free";

    public string Name { get; set; } = string.Empty;

    /// <summary>Monthly price in AED (0 for Free). USD ≈ AED / 3.67.</summary>
    public decimal PriceAedMonthly { get; set; }

    // Limits per billing period (-1 = unlimited).
    public int MaxContacts { get; set; }
    public int MaxEmailsPerMonth { get; set; }
    public int MaxWhatsAppPerMonth { get; set; }
    public int MaxAiPerMonth { get; set; }
    public int MaxUsers { get; set; }

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
