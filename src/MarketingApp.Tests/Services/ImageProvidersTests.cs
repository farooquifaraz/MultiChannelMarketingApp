using FluentAssertions;
using MarketingApp.Application.Interfaces.Media;
using MarketingApp.Infrastructure.Services.Media;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace MarketingApp.Tests.Services;

/// <summary>
/// P3.3 — additional image providers. Covers the Pollinations URL builder (free, no key), Gemini +
/// Stability response parsers, Stability aspect-ratio mapping, key-required guards, and that the
/// factory now resolves every registered provider.
/// </summary>
public class ImageProvidersTests
{
    // ---- Pollinations (free, no key) ----

    [Fact]
    public void Pollinations_builds_encoded_url_with_dimensions()
    {
        var url = PollinationsImageClient.BuildUrl("a red car", 1200, 628);
        url.Should().StartWith("https://image.pollinations.ai/prompt/");
        url.Should().Contain("a%20red%20car");
        url.Should().Contain("width=1200").And.Contain("height=628").And.Contain("nologo=true");
    }

    [Fact]
    public async Task Pollinations_returns_url_without_key()
    {
        var r = await new PollinationsImageClient().GenerateAsync(
            new ImageGenerationRequest("hi", 512, 512, "", null, null, 30), CancellationToken.None);
        r.IsSuccess.Should().BeTrue();
        r.ImageUrl.Should().Contain("pollinations.ai");
        r.Provider.Should().Be("pollinations");
    }

    // ---- Gemini parser ----

    [Fact]
    public void Gemini_parses_inline_image_to_data_uri()
    {
        var json = """
        {"candidates":[{"content":{"parts":[
          {"text":"here"},
          {"inlineData":{"mimeType":"image/png","data":"QUJD"}}
        ]}}]}
        """;
        GeminiImageClient.ParseInlineImage(json).Should().Be("data:image/png;base64,QUJD");
    }

    [Fact]
    public void Gemini_supports_snake_case_inline_data()
    {
        var json = "{\"candidates\":[{\"content\":{\"parts\":[{\"inline_data\":{\"mime_type\":\"image/jpeg\",\"data\":\"XYZ\"}}]}}]}";
        GeminiImageClient.ParseInlineImage(json).Should().Be("data:image/jpeg;base64,XYZ");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"candidates\":[]}")]
    [InlineData("not json")]
    public void Gemini_returns_null_for_bad_bodies(string body)
        => GeminiImageClient.ParseInlineImage(body).Should().BeNull();

    // ---- Stability parser + aspect ratio ----

    [Fact]
    public void Stability_parses_base64_image()
        => StabilityImageClient.ParseImage("{\"image\":\"QUJD\"}").Should().Be("data:image/png;base64,QUJD");

    [Theory]
    [InlineData("{}")]
    [InlineData("bad")]
    public void Stability_returns_null_for_bad_bodies(string body)
        => StabilityImageClient.ParseImage(body).Should().BeNull();

    [Theory]
    [InlineData(1920, 1080, "16:9")]
    [InlineData(1200, 628, "16:9")]
    [InlineData(1024, 1024, "1:1")]
    [InlineData(1024, 1792, "9:16")]
    [InlineData(0, 0, "1:1")]
    public void Stability_maps_aspect_ratio(int w, int h, string expected)
        => StabilityImageClient.AspectRatio(w, h).Should().Be(expected);

    // ---- key guards ----

    [Fact]
    public async Task Gemini_without_key_fails_cleanly()
    {
        var c = new GeminiImageClient(Mock.Of<IHttpClientFactory>(), NullLogger<GeminiImageClient>.Instance);
        var r = await c.GenerateAsync(new ImageGenerationRequest("x", 512, 512, "", null, null, 30), CancellationToken.None);
        r.IsSuccess.Should().BeFalse();
        r.Error.Should().Contain("key");
    }

    [Fact]
    public async Task HuggingFace_without_token_fails_cleanly()
    {
        var c = new HuggingFaceImageClient(Mock.Of<IHttpClientFactory>(), NullLogger<HuggingFaceImageClient>.Instance);
        var r = await c.GenerateAsync(new ImageGenerationRequest("x", 512, 512, "", null, null, 30), CancellationToken.None);
        r.IsSuccess.Should().BeFalse();
        r.Error.Should().Contain("token");
    }

    // ---- factory resolves all providers ----

    [Fact]
    public void Factory_resolves_all_registered_image_providers()
    {
        var http = Mock.Of<IHttpClientFactory>();
        var factory = new ImageGenerationClientFactory(new IImageGenerationClient[]
        {
            new MockImageGenerationClient(),
            new DalleImageClient(http, NullLogger<DalleImageClient>.Instance),
            new PollinationsImageClient(),
            new GeminiImageClient(http, NullLogger<GeminiImageClient>.Instance),
            new StabilityImageClient(http, NullLogger<StabilityImageClient>.Instance),
            new HuggingFaceImageClient(http, NullLogger<HuggingFaceImageClient>.Instance),
        });
        foreach (var key in new[] { "mock", "dalle", "pollinations", "gemini", "stability", "huggingface" })
            factory.Resolve(key).Should().NotBeNull($"provider '{key}' should resolve");
        factory.Resolve("unknown").Should().BeNull();
    }
}
