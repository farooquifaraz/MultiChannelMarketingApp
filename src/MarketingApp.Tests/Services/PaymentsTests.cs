using FluentAssertions;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Application.Interfaces.Billing;
using MarketingApp.Application.Services;
using MarketingApp.Domain.Exceptions;
using MarketingApp.Infrastructure.Services.Billing;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace MarketingApp.Tests.Services;

/// <summary>
/// Phase 2 (P2.2) — payment provider foundation. Covers the mock provider (keyless immediate
/// activation), the Stripe provider's pure helpers (form builder, signature, event parser), the
/// factory, and the CheckoutService orchestration (validation + activate loop + webhook).
/// </summary>
public class PaymentsTests
{
    private static CheckoutRequest Req(decimal price = 179m) =>
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "pro", "Pro", price,
            "https://app/success", "https://app/cancel", "sk_test_x", null);

    // ---- Mock provider ----

    [Fact]
    public async Task MockProvider_activates_immediately()
    {
        var r = await new MockBillingProvider().CreateCheckoutAsync(Req(), CancellationToken.None);
        r.IsSuccess.Should().BeTrue();
        r.ActivatedImmediately.Should().BeTrue();
        r.CheckoutUrl.Should().Be("https://app/success");
        r.ExternalSessionId.Should().StartWith("mock_sess_");
    }

    // ---- Factory ----

    [Fact]
    public void Factory_resolves_providers()
    {
        var f = new BillingProviderFactory(new IBillingProvider[]
        {
            new MockBillingProvider(),
            new StripeBillingProvider(Mock.Of<IHttpClientFactory>(), NullLogger<StripeBillingProvider>.Instance),
        });
        f.Resolve("mock").Should().NotBeNull();
        f.Resolve("STRIPE").Should().NotBeNull();
        f.Resolve("paypal").Should().BeNull();
        f.Resolve("").Should().BeNull();
    }

    // ---- Stripe pure helpers ----

    [Fact]
    public void BuildCheckoutForm_has_subscription_fields_and_fils_amount()
    {
        var form = StripeBillingProvider.BuildCheckoutForm(Req(179m)).ToDictionary(k => k.Key, v => v.Value);
        form["mode"].Should().Be("subscription");
        form["client_reference_id"].Should().Be("11111111-1111-1111-1111-111111111111");
        form["metadata[plan_code]"].Should().Be("pro");
        form["line_items[0][price_data][currency]"].Should().Be("aed");
        form["line_items[0][price_data][unit_amount]"].Should().Be("17900");   // AED 179 → fils
        form["line_items[0][price_data][recurring][interval]"].Should().Be("month");
        form["line_items[0][price_data][product_data][name]"].Should().Be("Pro");
    }

    [Fact]
    public void Signature_roundtrips_and_rejects_tampering()
    {
        const string secret = "whsec_test";
        const string payload = "{\"id\":\"evt_1\"}";
        var sig = StripeBillingProvider.ComputeSignature("1700000000", payload, secret);
        var goodHeader = $"t=1700000000,v1={sig}";

        StripeBillingProvider.VerifySignature(payload, goodHeader, secret, out _).Should().BeTrue();
        StripeBillingProvider.VerifySignature(payload, "t=1700000000,v1=deadbeef", secret, out _).Should().BeFalse();
        StripeBillingProvider.VerifySignature(payload, null, secret, out _).Should().BeFalse();
        StripeBillingProvider.VerifySignature(payload, "garbage", secret, out _).Should().BeFalse();
        StripeBillingProvider.VerifySignature("tampered" + payload, goodHeader, secret, out _).Should().BeFalse();
    }

    [Fact]
    public void ComputeSignature_is_deterministic_lowercase_hex()
    {
        var a = StripeBillingProvider.ComputeSignature("1", "x", "s");
        var b = StripeBillingProvider.ComputeSignature("1", "x", "s");
        a.Should().Be(b);
        a.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void ParseEvent_activates_on_checkout_completed()
    {
        var json = """
        {"type":"checkout.session.completed","data":{"object":{
          "client_reference_id":"22222222-2222-2222-2222-222222222222",
          "metadata":{"plan_code":"business"},
          "customer":"cus_1","subscription":"sub_1"}}}
        """;
        var r = StripeBillingProvider.ParseEvent(json);
        r.Handled.Should().BeTrue();
        r.ShouldActivate.Should().BeTrue();
        r.UserId.Should().Be(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        r.PlanCode.Should().Be("business");
        r.ExternalCustomerId.Should().Be("cus_1");
        r.ExternalSubscriptionId.Should().Be("sub_1");
    }

    [Fact]
    public void ParseEvent_ignores_other_event_types()
    {
        var r = StripeBillingProvider.ParseEvent("{\"type\":\"invoice.paid\",\"data\":{\"object\":{}}}");
        r.Handled.Should().BeTrue();
        r.ShouldActivate.Should().BeFalse();
    }

    [Fact]
    public void ParseEvent_completed_without_refs_does_not_activate()
    {
        var r = StripeBillingProvider.ParseEvent("{\"type\":\"checkout.session.completed\",\"data\":{\"object\":{}}}");
        r.Handled.Should().BeTrue();
        r.ShouldActivate.Should().BeFalse();
    }

    [Fact]
    public async Task StripeProvider_without_key_returns_error()
    {
        var p = new StripeBillingProvider(Mock.Of<IHttpClientFactory>(), NullLogger<StripeBillingProvider>.Instance);
        var r = await p.CreateCheckoutAsync(Req() with { ApiKey = null }, CancellationToken.None);
        r.IsSuccess.Should().BeFalse();
        r.Error.Should().Contain("key");
    }

    // ---- CheckoutService ----

    private static CheckoutService BuildCheckout(out Mock<IBillingService> billing, string provider = "mock")
    {
        var factory = new BillingProviderFactory(new IBillingProvider[] { new MockBillingProvider() });
        billing = new Mock<IBillingService>();
        billing.Setup(b => b.GetPlanAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string code, CancellationToken _) =>
                code == "pro" ? new PlanDto { Code = "pro", Name = "Pro", PriceAedMonthly = 179m } : null);
        billing.Setup(b => b.ActivatePaidPlanAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionDto { PlanCode = "pro" });
        var settings = new Mock<ISystemSettingsService>();
        settings.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SystemSettingsDto { PaymentProvider = provider });
        settings.Setup(s => s.GetRawPaymentSecretsAsync(It.IsAny<CancellationToken>())).ReturnsAsync((null, null));
        var audit = new Mock<IAuditService>();
        audit.Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<Guid?>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return new CheckoutService(factory, billing.Object, settings.Object, audit.Object, NullLogger<CheckoutService>.Instance);
    }

    [Fact]
    public async Task StartCheckout_mock_activates_plan()
    {
        var svc = BuildCheckout(out var billing);
        var r = await svc.StartCheckoutAsync(Guid.NewGuid(), new StartCheckoutDto { PlanCode = "pro" });
        r.Activated.Should().BeTrue();
        r.PlanCode.Should().Be("pro");
        billing.Verify(b => b.ActivatePaidPlanAsync(It.IsAny<Guid>(), "pro", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("free")]
    [InlineData("nonexistent")]
    public async Task StartCheckout_rejects_invalid_plan(string plan)
    {
        var svc = BuildCheckout(out _);
        var act = () => svc.StartCheckoutAsync(Guid.NewGuid(), new StartCheckoutDto { PlanCode = plan });
        await act.Should().ThrowAsync<AppValidationException>();
    }

    [Fact]
    public async Task StartCheckout_throws_when_disabled()
    {
        var svc = BuildCheckout(out _, provider: "disabled");
        var act = () => svc.StartCheckoutAsync(Guid.NewGuid(), new StartCheckoutDto { PlanCode = "pro" });
        await act.Should().ThrowAsync<AppValidationException>();
    }

    [Fact]
    public async Task HandleWebhook_unknown_provider_returns_false()
    {
        var svc = BuildCheckout(out var billing);
        (await svc.HandleWebhookAsync("paypal", "{}", null)).Should().BeFalse();
        billing.Verify(b => b.ActivatePaidPlanAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
