using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Domain.Entities;
using MarketingApp.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Application.Services;

/// <summary>
/// Plans + subscriptions + live usage (Phase 2 / P2.1). Usage is computed on read from existing
/// data (contacts, campaign messages, AI-generated inbox replies) so there's no new write path and
/// zero impact on sending. Quota enforcement is a separate, default-off concern.
/// </summary>
public class BillingService : IBillingService
{
    private readonly IGenericRepository<Plan> _planRepo;
    private readonly IGenericRepository<Subscription> _subRepo;
    private readonly IGenericRepository<Contact> _contactRepo;
    private readonly IGenericRepository<CampaignMessage> _msgRepo;
    private readonly IGenericRepository<InboxMessage> _inboxRepo;
    private readonly ISystemSettingsService _settings;
    private readonly ILogger<BillingService> _logger;

    private static readonly string[] DispatchedStatuses = { "sent", "delivered", "opened", "clicked" };

    public BillingService(
        IGenericRepository<Plan> planRepo,
        IGenericRepository<Subscription> subRepo,
        IGenericRepository<Contact> contactRepo,
        IGenericRepository<CampaignMessage> msgRepo,
        IGenericRepository<InboxMessage> inboxRepo,
        ISystemSettingsService settings,
        ILogger<BillingService> logger)
    {
        _planRepo = planRepo;
        _subRepo = subRepo;
        _contactRepo = contactRepo;
        _msgRepo = msgRepo;
        _inboxRepo = inboxRepo;
        _settings = settings;
        _logger = logger;
    }

    public async Task<IEnumerable<PlanDto>> ListPlansAsync(CancellationToken ct = default)
    {
        var plans = await _planRepo.FindAsync(p => p.IsActive, ct);
        return plans.OrderBy(p => p.SortOrder).Select(ToPlanDto);
    }

    public async Task<SubscriptionDto> GetMySubscriptionAsync(Guid userId, CancellationToken ct = default)
    {
        var sub = await GetOrCreateSubscriptionAsync(userId, ct);
        var plan = await GetPlanByCodeAsync(sub.PlanCode, ct) ?? await GetPlanByCodeAsync("free", ct)
                   ?? throw new InvalidOperationException("No plans configured.");
        var enforced = (await _settings.GetAsync(ct)).EnableQuotas;
        return await BuildDtoAsync(userId, sub, plan, enforced, ct);
    }

    public async Task<SubscriptionDto> ChangePlanAsync(Guid userId, string planCode, CancellationToken ct = default)
    {
        var plan = await GetPlanByCodeAsync(planCode, ct)
            ?? throw new AppValidationException($"Unknown plan '{planCode}'.");

        var sub = await GetOrCreateSubscriptionAsync(userId, ct);
        sub.PlanCode = plan.Code;
        sub.Status = "active";
        sub.CurrentPeriodStart = DateTime.UtcNow;
        sub.CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1);
        sub.UpdatedAt = DateTime.UtcNow;
        await _subRepo.UpdateAsync(sub, ct);
        _logger.LogInformation("User {UserId} changed plan to {Plan}", userId, plan.Code);

        var enforced = (await _settings.GetAsync(ct)).EnableQuotas;
        return await BuildDtoAsync(userId, sub, plan, enforced, ct);
    }

    // === helpers ===

    private async Task<Subscription> GetOrCreateSubscriptionAsync(Guid userId, CancellationToken ct)
    {
        var existing = (await _subRepo.FindAsync(s => s.UserId == userId, ct)).FirstOrDefault();
        if (existing is not null) return existing;

        var sub = new Subscription
        {
            UserId = userId,
            PlanCode = "free",
            Status = "active",
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1),
        };
        await _subRepo.AddAsync(sub, ct);
        _logger.LogInformation("Auto-provisioned Free subscription for user {UserId}", userId);
        return sub;
    }

    private async Task<Plan?> GetPlanByCodeAsync(string code, CancellationToken ct) =>
        (await _planRepo.FindAsync(p => p.Code == code, ct)).FirstOrDefault();

    private async Task<SubscriptionDto> BuildDtoAsync(Guid userId, Subscription sub, Plan plan, bool enforced, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var contacts = await _contactRepo.CountAsync(c => c.UserId == userId && c.IsActive, ct);
        var emails = await _msgRepo.CountAsync(m =>
            m.Campaign.UserId == userId && m.Campaign.Channel == "email"
            && m.SentAt != null && m.SentAt >= monthStart
            && DispatchedStatuses.Contains(m.Status), ct);
        var whatsapp = await _msgRepo.CountAsync(m =>
            m.Campaign.UserId == userId && m.Campaign.Channel == "whatsapp"
            && m.SentAt != null && m.SentAt >= monthStart
            && DispatchedStatuses.Contains(m.Status), ct);
        var ai = await _inboxRepo.CountAsync(x =>
            x.OwnerUserId == userId && x.AiGeneratedAt != null && x.AiGeneratedAt >= monthStart, ct);

        return new SubscriptionDto
        {
            PlanCode = plan.Code,
            PlanName = plan.Name,
            PriceAedMonthly = plan.PriceAedMonthly,
            Status = sub.Status,
            CurrentPeriodStart = sub.CurrentPeriodStart,
            CurrentPeriodEnd = sub.CurrentPeriodEnd,
            QuotasEnforced = enforced,
            Contacts = new UsageMetricDto { Used = contacts, Limit = plan.MaxContacts },
            Emails = new UsageMetricDto { Used = emails, Limit = plan.MaxEmailsPerMonth },
            WhatsApp = new UsageMetricDto { Used = whatsapp, Limit = plan.MaxWhatsAppPerMonth },
            Ai = new UsageMetricDto { Used = ai, Limit = plan.MaxAiPerMonth },
        };
    }

    private static PlanDto ToPlanDto(Plan p) => new()
    {
        Code = p.Code, Name = p.Name, PriceAedMonthly = p.PriceAedMonthly,
        MaxContacts = p.MaxContacts, MaxEmailsPerMonth = p.MaxEmailsPerMonth,
        MaxWhatsAppPerMonth = p.MaxWhatsAppPerMonth, MaxAiPerMonth = p.MaxAiPerMonth,
        MaxUsers = p.MaxUsers, SortOrder = p.SortOrder,
    };
}
