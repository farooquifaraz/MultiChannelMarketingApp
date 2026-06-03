using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;

namespace MarketingApp.Application.Services;

/// <summary>
/// Plan-limit checks (Phase 2 / P2.3). Reuses BillingService's live usage + limits, then applies the
/// platform enable_quotas gate. When quotas are off (default) every check is allowed — purely additive.
/// </summary>
public class QuotaService : IQuotaService
{
    private readonly IBillingService _billing;

    public QuotaService(IBillingService billing) { _billing = billing; }

    public async Task<QuotaResult> CheckAsync(Guid userId, QuotaKind kind, int requested = 1, CancellationToken ct = default)
    {
        var sub = await _billing.GetMySubscriptionAsync(userId, ct);
        var m = kind switch
        {
            QuotaKind.Email => sub.Emails,
            QuotaKind.WhatsApp => sub.WhatsApp,
            QuotaKind.Ai => sub.Ai,
            QuotaKind.Contacts => sub.Contacts,
            _ => sub.Emails,
        };

        // Not enforced (flag off) OR unlimited tier → always allowed; numbers are informational.
        if (!sub.QuotasEnforced || m.Unlimited)
            return new QuotaResult(true, m.Limit, m.Used, m.Remaining, m.Unlimited, sub.QuotasEnforced, null);

        var wouldBe = m.Used + Math.Max(0, requested);
        if (wouldBe > m.Limit)
        {
            var reason = $"Your {sub.PlanName} plan allows {m.Limit:N0} {Label(kind)} this period " +
                         $"({m.Used:N0} used). This action needs {requested:N0} more — upgrade your plan to continue.";
            return new QuotaResult(false, m.Limit, m.Used, m.Remaining, false, true, reason);
        }

        return new QuotaResult(true, m.Limit, m.Used, m.Remaining, false, true, null);
    }

    private static string Label(QuotaKind k) => k switch
    {
        QuotaKind.Email => "emails",
        QuotaKind.WhatsApp => "WhatsApp messages",
        QuotaKind.Ai => "AI generations",
        QuotaKind.Contacts => "contacts",
        _ => "items",
    };
}
