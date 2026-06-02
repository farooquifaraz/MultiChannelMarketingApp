using FluentAssertions;
using MarketingApp.Application.DTOs;
using MarketingApp.Infrastructure.Services.Webhooks;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarketingApp.Tests.Services;

/// <summary>
/// Brevo webhook correlation — guards the fix for the 2026-06-02 incident where Brevo
/// reported "delivered/opened" but MarketPro messages stayed stuck on "sent".
///
/// Root cause: Brevo does NOT echo arbitrary custom headers (X-Campaign-Message-Id) in
/// its webhooks. It only round-trips "X-Mailin-custom" and "tags". The handler must read
/// the CampaignMessage.Id from those channels, and always surface the recipient email so
/// the processor can fall back to email-based correlation for un-tagged sends.
/// </summary>
public class BrevoWebhookHandlerTests
{
    private static BrevoWebhookHandler NewHandler() => new(NullLogger<BrevoWebhookHandler>.Instance);

    private static WebhookRequest Req(string json) => new()
    {
        BodyText = json,
        RawBody = System.Text.Encoding.UTF8.GetBytes(json),
    };

    private static readonly Guid MsgId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact(DisplayName = "Resolves CampaignMessageId from X-Mailin-custom")]
    public async Task ResolvesFromMailinCustom()
    {
        var json = $$"""
        { "event": "delivered", "email": "a@b.com", "message-id": "<x@brevo>",
          "date": "2026-06-02 15:19:00", "X-Mailin-custom": "{{MsgId}}" }
        """;
        var events = await NewHandler().ParseAsync(Req(json), default);
        events.Should().HaveCount(1);
        events[0].CampaignMessageId.Should().Be(MsgId);
        events[0].EventType.Should().Be("delivered");
        events[0].RecipientEmail.Should().Be("a@b.com");
    }

    [Fact(DisplayName = "Resolves CampaignMessageId from tags array")]
    public async Task ResolvesFromTagsArray()
    {
        var json = $$"""
        { "event": "opened", "email": "a@b.com", "tags": ["{{MsgId}}"] }
        """;
        var events = await NewHandler().ParseAsync(Req(json), default);
        events[0].CampaignMessageId.Should().Be(MsgId);
        events[0].EventType.Should().Be("opened");
    }

    [Fact(DisplayName = "Resolves CampaignMessageId from tag string (JSON-encoded array)")]
    public async Task ResolvesFromTagJsonString()
    {
        var json = $$"""
        { "event": "click", "email": "a@b.com", "tag": "[\"{{MsgId}}\"]" }
        """;
        var events = await NewHandler().ParseAsync(Req(json), default);
        events[0].CampaignMessageId.Should().Be(MsgId);
        events[0].EventType.Should().Be("clicked");
    }

    [Fact(DisplayName = "Resolves CampaignMessageId from bare tag string")]
    public async Task ResolvesFromBareTag()
    {
        var json = $$"""
        { "event": "delivered", "email": "a@b.com", "tag": "{{MsgId}}" }
        """;
        var events = await NewHandler().ParseAsync(Req(json), default);
        events[0].CampaignMessageId.Should().Be(MsgId);
    }

    [Fact(DisplayName = "Still honours legacy X-Campaign-Message-Id header if present")]
    public async Task ResolvesFromLegacyHeader()
    {
        var json = $$"""
        { "event": "delivered", "email": "a@b.com", "X-Campaign-Message-Id": "{{MsgId}}" }
        """;
        var events = await NewHandler().ParseAsync(Req(json), default);
        events[0].CampaignMessageId.Should().Be(MsgId);
    }

    [Fact(DisplayName = "No correlation id → null id but recipient email still captured (enables processor fallback)")]
    public async Task NoIdButEmailCaptured()
    {
        // This is the shape of every email sent BEFORE the tag fix — Brevo gives us only the address.
        var json = """
        { "event": "opened", "email": "old-send@b.com", "message-id": "<x@brevo>", "date": "2026-06-02 15:19:00" }
        """;
        var events = await NewHandler().ParseAsync(Req(json), default);
        events[0].CampaignMessageId.Should().BeNull();
        events[0].RecipientEmail.Should().Be("old-send@b.com");
    }

    [Theory(DisplayName = "Brevo event names normalize to internal status vocab")]
    [InlineData("delivered", "delivered")]
    [InlineData("opened", "opened")]
    [InlineData("unique_opened", "opened")]
    [InlineData("click", "clicked")]
    [InlineData("hard_bounce", "bounced")]
    [InlineData("soft_bounce", "deferred")]
    [InlineData("blocked", "dropped")]
    [InlineData("spam", "spam_report")]
    [InlineData("unsubscribed", "unsubscribed")]
    public async Task NormalizesEventTypes(string brevoEvent, string expected)
    {
        var json = $$"""
        { "event": "{{brevoEvent}}", "email": "a@b.com", "X-Mailin-custom": "{{MsgId}}" }
        """;
        var events = await NewHandler().ParseAsync(Req(json), default);
        events[0].EventType.Should().Be(expected);
    }

    [Fact(DisplayName = "hard_bounce flagged as hard; soft_bounce is not")]
    public async Task HardBounceFlag()
    {
        var hard = await NewHandler().ParseAsync(Req("""{ "event":"hard_bounce", "email":"a@b.com", "reason":"no such user" }"""), default);
        hard[0].IsHardBounce.Should().BeTrue();

        var soft = await NewHandler().ParseAsync(Req("""{ "event":"soft_bounce", "email":"a@b.com" }"""), default);
        soft[0].IsHardBounce.Should().BeFalse();
    }

    [Fact(DisplayName = "Distinct timestamps produce distinct dedup keys")]
    public async Task DedupKeyVariesByTimestamp()
    {
        var e1 = await NewHandler().ParseAsync(Req("""{ "event":"opened", "email":"a@b.com", "message-id":"<m1>", "date":"2026-06-02 15:19:00" }"""), default);
        var e2 = await NewHandler().ParseAsync(Req("""{ "event":"opened", "email":"a@b.com", "message-id":"<m1>", "date":"2026-06-02 16:20:00" }"""), default);
        e1[0].ProviderEventId.Should().NotBe(e2[0].ProviderEventId);
    }
}
