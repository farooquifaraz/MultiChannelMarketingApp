using FluentAssertions;
using MarketingApp.Application.Jobs;

namespace MarketingApp.Tests.Services;

/// <summary>
/// L3 — parsing of Meta WhatsApp Cloud API inbound webhook payloads. Pure function, fully testable
/// without Meta credentials. Covers the real Meta payload shape, non-text types, profile names,
/// timestamps, and malformed/empty input (must never throw).
/// </summary>
public class WhatsAppInboundParseTests
{
    [Fact(DisplayName = "Parses a real text-message webhook (from, id, text, phone_number_id, name)")]
    public void ParsesTextMessage()
    {
        var json = """
        {
          "object": "whatsapp_business_account",
          "entry": [{
            "id": "WABA123",
            "changes": [{
              "field": "messages",
              "value": {
                "messaging_product": "whatsapp",
                "metadata": { "display_phone_number": "15550001111", "phone_number_id": "PHONE_999" },
                "contacts": [{ "profile": { "name": "Ahsan Yaqoob" }, "wa_id": "971501234567" }],
                "messages": [{
                  "from": "971501234567",
                  "id": "wamid.ABC123",
                  "timestamp": "1780000000",
                  "type": "text",
                  "text": { "body": "Hi, is the villa still available?" }
                }]
              }
            }]
          }]
        }
        """;
        var list = WhatsAppInboundService.ParseInbound(json);
        list.Should().HaveCount(1);
        var m = list[0];
        m.PhoneNumberId.Should().Be("PHONE_999");
        m.From.Should().Be("971501234567");
        m.FromName.Should().Be("Ahsan Yaqoob");
        m.MessageId.Should().Be("wamid.ABC123");
        m.Type.Should().Be("text");
        m.Text.Should().Be("Hi, is the villa still available?");
        m.ReceivedAt.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1780000000).UtcDateTime);
    }

    [Theory(DisplayName = "Non-text message types yield a placeholder body")]
    [InlineData("image", "[image]")]
    [InlineData("document", "[document]")]
    [InlineData("video", "[video]")]
    [InlineData("audio", "[voice message]")]
    [InlineData("location", "[location]")]
    public void NonTextPlaceholders(string type, string expected)
    {
        var json = $$"""
        { "entry": [{ "changes": [{ "value": {
          "metadata": { "phone_number_id": "P1" },
          "messages": [{ "from": "971500000000", "id": "wamid.X", "timestamp": "1780000000", "type": "{{type}}" }]
        } }] }] }
        """;
        var list = WhatsAppInboundService.ParseInbound(json);
        list.Should().HaveCount(1);
        list[0].Type.Should().Be(type);
        list[0].Text.Should().Be(expected);
    }

    [Fact(DisplayName = "Multiple messages in one webhook all parsed")]
    public void MultipleMessages()
    {
        var json = """
        { "entry": [{ "changes": [{ "value": {
          "metadata": { "phone_number_id": "P1" },
          "messages": [
            { "from": "9715001", "id": "wamid.1", "timestamp": "1780000000", "type": "text", "text": { "body": "one" } },
            { "from": "9715002", "id": "wamid.2", "timestamp": "1780000001", "type": "text", "text": { "body": "two" } }
          ]
        } }] }] }
        """;
        var list = WhatsAppInboundService.ParseInbound(json);
        list.Should().HaveCount(2);
        list.Select(x => x.MessageId).Should().BeEquivalentTo(new[] { "wamid.1", "wamid.2" });
    }

    [Fact(DisplayName = "Status-only webhook (no messages[]) yields nothing")]
    public void StatusOnlyWebhook()
    {
        // Meta also sends delivery/read status callbacks with no inbound messages.
        var json = """
        { "entry": [{ "changes": [{ "value": {
          "metadata": { "phone_number_id": "P1" },
          "statuses": [{ "id": "wamid.X", "status": "delivered" }]
        } }] }] }
        """;
        WhatsAppInboundService.ParseInbound(json).Should().BeEmpty();
    }

    [Theory(DisplayName = "Malformed / empty input never throws → empty list")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"entry":[]}""")]
    [InlineData("""{"entry":[{"changes":[]}]}""")]
    public void MalformedInput(string json)
    {
        WhatsAppInboundService.ParseInbound(json).Should().BeEmpty();
    }

    [Fact(DisplayName = "Missing profile name → FromName null (still parses)")]
    public void NoProfileName()
    {
        var json = """
        { "entry": [{ "changes": [{ "value": {
          "metadata": { "phone_number_id": "P1" },
          "messages": [{ "from": "9715001", "id": "wamid.1", "timestamp": "1780000000", "type": "text", "text": { "body": "hi" } }]
        } }] }] }
        """;
        var list = WhatsAppInboundService.ParseInbound(json);
        list[0].FromName.Should().BeNull();
        list[0].Text.Should().Be("hi");
    }
}
