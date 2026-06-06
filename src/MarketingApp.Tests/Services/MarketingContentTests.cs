using FluentAssertions;
using MarketingApp.Application.Services;

namespace MarketingApp.Tests.Services;

/// <summary>
/// P3.6 — AI marketing-copy parser. Verifies the structured JSON (with/without ``` fences or
/// surrounding prose) is parsed into channel copy, and that bad output degrades gracefully.
/// </summary>
public class MarketingContentTests
{
    private const string Sample = """
    {
      "whatsapp": { "broadcast": "🏆 EXCLUSIVE villa", "status_text": "New listing ✨" },
      "instagram": { "caption": "Dream home", "reels_hook": "Wait for it", "story_cta": "DM INFO" },
      "email": { "subject": "Private viewing", "preview": "Exclusive", "body": "<b>Dear investor</b>" },
      "image_prompt": "a luxury villa at golden hour"
    }
    """;

    [Fact]
    public void Parses_clean_json()
    {
        var dto = MarketingContentService.ParseContent(Sample);
        dto.WhatsApp.Broadcast.Should().Contain("EXCLUSIVE");
        dto.WhatsApp.StatusText.Should().Contain("New listing");
        dto.Instagram.Caption.Should().Be("Dream home");
        dto.Instagram.ReelsHook.Should().Be("Wait for it");
        dto.Email.Subject.Should().Be("Private viewing");
        dto.Email.Body.Should().Contain("Dear investor");
        dto.ImagePrompt.Should().Contain("golden hour");
    }

    [Fact]
    public void Parses_json_inside_markdown_fences()
    {
        var fenced = "```json\n" + Sample + "\n```";
        MarketingContentService.ParseContent(fenced).WhatsApp.Broadcast.Should().Contain("EXCLUSIVE");
    }

    [Fact]
    public void Parses_json_with_surrounding_prose()
    {
        var noisy = "Sure! Here is your content:\n" + Sample + "\nHope this helps!";
        MarketingContentService.ParseContent(noisy).Instagram.StoryCta.Should().Be("DM INFO");
    }

    [Fact]
    public void Falls_back_to_raw_text_when_not_json()
    {
        var dto = MarketingContentService.ParseContent("just some plain text, not json");
        dto.WhatsApp.Broadcast.Should().Be("just some plain text, not json");
    }

    [Fact]
    public void Missing_sections_default_to_empty()
    {
        var dto = MarketingContentService.ParseContent("{\"whatsapp\":{\"broadcast\":\"hi\"}}");
        dto.WhatsApp.Broadcast.Should().Be("hi");
        dto.Instagram.Caption.Should().BeEmpty();
        dto.Email.Subject.Should().BeEmpty();
    }

    [Fact]
    public void Parses_facebook_section()
    {
        var json = "{\"facebook\":{\"post\":\"🏡 Luxury villa\",\"headline\":\"5BR Oasis Villa\",\"cta\":\"Book a viewing\"}}";
        var dto = MarketingContentService.ParseContent(json);
        dto.Facebook.Post.Should().Contain("Luxury villa");
        dto.Facebook.Headline.Should().Be("5BR Oasis Villa");
        dto.Facebook.Cta.Should().Be("Book a viewing");
    }

    [Theory]
    [InlineData(null, null, 4)]               // nothing → all 4
    [InlineData(new[] { "all" }, null, 4)]    // explicit all → all 4
    [InlineData(new[] { "facebook", "whatsapp" }, null, 2)]
    [InlineData(new[] { "instagram", "bogus" }, null, 1)] // unknown ignored
    public void NormalizeChannels_resolves_requested_set(string[]? channels, string? legacy, int expected)
    {
        var list = channels?.ToList();
        MarketingContentService.NormalizeChannels(list, legacy).Count.Should().Be(expected);
    }

    [Fact]
    public void NormalizeChannels_keeps_canonical_order()
    {
        var r = MarketingContentService.NormalizeChannels(new() { "email", "whatsapp" }, null);
        r.Should().ContainInOrder("whatsapp", "email"); // canonical order, not input order
    }

    [Fact]
    public void BuildSystemPrompt_includes_only_requested_channels_plus_image()
    {
        var prompt = MarketingContentService.BuildSystemPrompt(new[] { "facebook" });
        prompt.Should().Contain("\"facebook\"");
        prompt.Should().Contain("\"image_prompt\"");
        prompt.Should().NotContain("\"whatsapp\"");
        prompt.Should().NotContain("\"instagram\"");
        prompt.Should().NotContain("\"email\"");
    }
}
