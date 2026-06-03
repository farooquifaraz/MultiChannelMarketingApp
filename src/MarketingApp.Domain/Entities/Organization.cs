namespace MarketingApp.Domain.Entities;

/// <summary>
/// A tenant boundary (Phase 2 / P2.4). Every existing data-owning entity gets a nullable
/// <c>OrganizationId</c> that points here. To stay zero-regression this is introduced as a pure
/// grouping layer first: all existing rows are backfilled to a single seeded "Legacy Organization"
/// (fixed id below) and query-level tenant isolation is NOT yet enforced (that is a later slice,
/// gated by <c>SystemSettings.EnableMultiTenancy</c>). Until isolation is switched on, behaviour is
/// identical to today — data is still scoped by UserId.
/// </summary>
public class Organization
{
    /// <summary>
    /// Fixed id for the seeded "Legacy Organization" that owns all pre-multi-tenancy data.
    /// Stable so the backfill migration is idempotent and the value can be referenced in code.
    /// </summary>
    public static readonly Guid LegacyOrgId = Guid.Parse("00000000-0000-0000-0000-00000000ace0");

    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>URL-safe unique handle (e.g. "acme"). Reserved for future per-org subdomains.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>The user who owns/created the org (org_owner). Nullable for the Legacy org.</summary>
    public Guid? OwnerUserId { get; set; }

    /// <summary>Plan code this org bills on. Mirrors Subscription for org-level billing later.</summary>
    public string PlanCode { get; set; } = "free";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<User> Users { get; set; } = new List<User>();
}
