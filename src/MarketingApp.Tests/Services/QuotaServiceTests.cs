using FluentAssertions;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Application.Services;
using Moq;

namespace MarketingApp.Tests.Services;

/// <summary>
/// P2.3 — plan quota enforcement. Verifies the gate (enable_quotas off ⇒ always allowed, so existing
/// sends never break), the over/under-limit math, and unlimited tiers.
/// </summary>
public class QuotaServiceTests
{
    private static SubscriptionDto Sub(bool enforced, int emailUsed, int emailLimit) => new()
    {
        PlanCode = "starter", PlanName = "Starter", QuotasEnforced = enforced,
        Emails = new UsageMetricDto { Used = emailUsed, Limit = emailLimit },
        WhatsApp = new UsageMetricDto { Used = 0, Limit = 1000 },
        Ai = new UsageMetricDto { Used = 0, Limit = 500 },
        Contacts = new UsageMetricDto { Used = 0, Limit = 2500 },
    };

    private static QuotaService NewSvc(SubscriptionDto sub)
    {
        var billing = new Mock<IBillingService>();
        billing.Setup(b => b.GetMySubscriptionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(sub);
        return new QuotaService(billing.Object);
    }

    [Fact(DisplayName = "Quotas OFF → always allowed (zero-regression gate)")]
    public async Task Disabled_AlwaysAllowed()
    {
        var svc = NewSvc(Sub(enforced: false, emailUsed: 9999, emailLimit: 500)); // way over, but not enforced
        var r = await svc.CheckAsync(Guid.NewGuid(), QuotaKind.Email, 1000);
        r.Allowed.Should().BeTrue();
        r.Enforced.Should().BeFalse();
        r.Reason.Should().BeNull();
    }

    [Fact(DisplayName = "Enforced + under limit → allowed")]
    public async Task Enforced_UnderLimit_Allowed()
    {
        var svc = NewSvc(Sub(enforced: true, emailUsed: 100, emailLimit: 10000));
        var r = await svc.CheckAsync(Guid.NewGuid(), QuotaKind.Email, 50);
        r.Allowed.Should().BeTrue();
        r.Enforced.Should().BeTrue();
    }

    [Fact(DisplayName = "Enforced + request would exceed limit → denied with reason")]
    public async Task Enforced_OverLimit_Denied()
    {
        var svc = NewSvc(Sub(enforced: true, emailUsed: 9990, emailLimit: 10000));
        var r = await svc.CheckAsync(Guid.NewGuid(), QuotaKind.Email, 50); // 9990+50 > 10000
        r.Allowed.Should().BeFalse();
        r.Reason.Should().NotBeNullOrEmpty();
        r.Reason.Should().Contain("upgrade");
    }

    [Fact(DisplayName = "Enforced + exactly at limit → allowed (boundary)")]
    public async Task Enforced_ExactLimit_Allowed()
    {
        var svc = NewSvc(Sub(enforced: true, emailUsed: 9950, emailLimit: 10000));
        var r = await svc.CheckAsync(Guid.NewGuid(), QuotaKind.Email, 50); // 9950+50 == 10000
        r.Allowed.Should().BeTrue();
    }

    [Fact(DisplayName = "Unlimited tier (-1) → always allowed even when enforced")]
    public async Task Unlimited_Allowed()
    {
        var sub = Sub(enforced: true, emailUsed: 999999, emailLimit: -1);
        var svc = NewSvc(sub);
        var r = await svc.CheckAsync(Guid.NewGuid(), QuotaKind.Email, 100000);
        r.Allowed.Should().BeTrue();
        r.Unlimited.Should().BeTrue();
    }

    [Fact(DisplayName = "Checks the correct metric per kind (WhatsApp)")]
    public async Task PicksWhatsAppMetric()
    {
        var sub = Sub(enforced: true, emailUsed: 0, emailLimit: 10000);
        sub.WhatsApp = new UsageMetricDto { Used = 1000, Limit = 1000 };
        var svc = NewSvc(sub);
        var r = await svc.CheckAsync(Guid.NewGuid(), QuotaKind.WhatsApp, 1); // 1000+1 > 1000
        r.Allowed.Should().BeFalse();
        r.Reason.Should().Contain("WhatsApp");
    }
}
