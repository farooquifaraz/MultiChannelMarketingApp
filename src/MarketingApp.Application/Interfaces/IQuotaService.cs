namespace MarketingApp.Application.Interfaces;

/// <summary>Which plan limit a quota check is about (Phase 2 / P2.3).</summary>
public enum QuotaKind { Email, WhatsApp, Ai, Contacts }

/// <summary>
/// Outcome of a quota check. When <see cref="Enforced"/> is false the action is always allowed —
/// the numbers are informational only (the platform-wide enable_quotas flag is off).
/// </summary>
public sealed record QuotaResult(bool Allowed, int Limit, int Used, int Remaining, bool Unlimited, bool Enforced, string? Reason);

/// <summary>
/// Checks a user's action against their plan limits (Phase 2). Enforcement is gated by the
/// platform's enable_quotas flag — when off, CheckAsync always allows, so existing sends never break.
/// </summary>
public interface IQuotaService
{
    Task<QuotaResult> CheckAsync(Guid userId, QuotaKind kind, int requested = 1, CancellationToken ct = default);
}
