using FluentAssertions;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Application.Interfaces.Media;
using MarketingApp.Application.Services;
using MarketingApp.Domain.Entities;
using MarketingApp.Domain.Exceptions;
using MarketingApp.Infrastructure.Services.Media;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq.Expressions;
using System.Text;

namespace MarketingApp.Tests.Services;

/// <summary>
/// Phase 3 (P3.1) — AI image generation foundation. Covers the pure helpers (ParseSize, placeholder
/// SVG, response parsing), the provider factory, the mock client, and the service generate/validate
/// flow. The mock path proves the studio works keyless.
/// </summary>
public class ImageGenerationTests
{
    // ---- ParseSize (pure) ----

    [Theory]
    [InlineData("1024x1024", 1024, 1024)]
    [InlineData("1792x1024", 1792, 1024)]
    [InlineData("1200x628", 1200, 628)]
    [InlineData("  1080x1080 ", 1080, 1080)]
    public void ParseSize_returns_allowed_dimensions(string token, int w, int h)
        => ImageGenerationService.ParseSize(token).Should().Be((w, h));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("999x999")]
    [InlineData("garbage")]
    public void ParseSize_falls_back_to_square_for_unknown(string? token)
        => ImageGenerationService.ParseSize(token).Should().Be((1024, 1024));

    // ---- Mock client / placeholder SVG ----

    [Fact]
    public void BuildPlaceholderSvg_is_wellformed_and_contains_dims()
    {
        var svg = MockImageGenerationClient.BuildPlaceholderSvg("A modern villa in Dubai", 1200, 628);
        svg.Should().StartWith("<svg").And.EndWith("</svg>");
        svg.Should().Contain("1200").And.Contain("628");
        svg.Should().Contain("villa");
    }

    [Fact]
    public void BuildPlaceholderSvg_escapes_xml_special_chars()
    {
        var svg = MockImageGenerationClient.BuildPlaceholderSvg("Sale <b> & \"deal\"", 512, 512);
        svg.Should().NotContain("<b>");
        svg.Should().Contain("&amp;");
    }

    [Fact]
    public async Task MockClient_returns_svg_data_uri()
    {
        var client = new MockImageGenerationClient();
        var result = await client.GenerateAsync(
            new ImageGenerationRequest("hello", 1024, 1024, "n/a", null, null, 30), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.ImageUrl.Should().StartWith("data:image/svg+xml;base64,");
        result.Provider.Should().Be("mock");
        // Round-trips to valid SVG.
        var b64 = result.ImageUrl!.Substring("data:image/svg+xml;base64,".Length);
        Encoding.UTF8.GetString(Convert.FromBase64String(b64)).Should().StartWith("<svg");
    }

    // ---- Factory ----

    [Fact]
    public void Factory_resolves_by_provider_key_case_insensitively()
    {
        var factory = new ImageGenerationClientFactory(new IImageGenerationClient[]
        {
            new MockImageGenerationClient(),
            new DalleImageClient(Mock.Of<IHttpClientFactory>(), NullLogger<DalleImageClient>.Instance),
        });
        factory.Resolve("mock").Should().NotBeNull();
        factory.Resolve("MOCK").Should().NotBeNull();
        factory.Resolve("dalle").Should().NotBeNull();
        factory.Resolve("nope").Should().BeNull();
        factory.Resolve("").Should().BeNull();
    }

    // ---- DALL·E response parsing (pure) ----

    [Fact]
    public void ParseImageUrl_reads_url_field()
        => DalleImageClient.ParseImageUrl("{\"data\":[{\"url\":\"https://x/y.png\"}]}").Should().Be("https://x/y.png");

    [Fact]
    public void ParseImageUrl_reads_b64_as_data_uri()
        => DalleImageClient.ParseImageUrl("{\"data\":[{\"b64_json\":\"QQ==\"}]}").Should().StartWith("data:image/png;base64,");

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"data\":[]}")]
    [InlineData("not json")]
    public void ParseImageUrl_returns_null_for_bad_bodies(string body)
        => DalleImageClient.ParseImageUrl(body).Should().BeNull();

    // ---- Service ----

    private static ImageGenerationService BuildService(out Mock<IGenericRepository<GeneratedAsset>> repo, string provider = "mock")
    {
        var factory = new ImageGenerationClientFactory(new IImageGenerationClient[] { new MockImageGenerationClient() });
        var settings = new Mock<ISystemSettingsService>();
        settings.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SystemSettingsDto { ImageProvider = provider, ImageModel = "dall-e-3" });
        settings.Setup(s => s.GetRawImageApiKeyAsync(It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        repo = new Mock<IGenericRepository<GeneratedAsset>>();
        repo.Setup(r => r.AddAsync(It.IsAny<GeneratedAsset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeneratedAsset a, CancellationToken _) => a);
        var audit = new Mock<IAuditService>();
        audit.Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<Guid?>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return new ImageGenerationService(factory, settings.Object, repo.Object, audit.Object,
            NullLogger<ImageGenerationService>.Instance);
    }

    [Fact]
    public async Task GenerateAsync_mock_produces_completed_asset()
    {
        var svc = BuildService(out var repo);
        var dto = await svc.GenerateAsync(Guid.NewGuid(), new GenerateImageDto { Prompt = "Eid sale banner", Size = "1200x628" });

        dto.Status.Should().Be("completed");
        dto.Provider.Should().Be("mock");
        dto.Width.Should().Be(1200);
        dto.Height.Should().Be(628);
        dto.ImageUrl.Should().StartWith("data:image/svg+xml;base64,");
        repo.Verify(r => r.AddAsync(It.IsAny<GeneratedAsset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GenerateAsync_rejects_blank_prompt(string prompt)
    {
        var svc = BuildService(out _);
        var act = () => svc.GenerateAsync(Guid.NewGuid(), new GenerateImageDto { Prompt = prompt });
        await act.Should().ThrowAsync<AppValidationException>();
    }

    [Fact]
    public async Task GenerateAsync_rejects_overlong_prompt()
    {
        var svc = BuildService(out _);
        var act = () => svc.GenerateAsync(Guid.NewGuid(), new GenerateImageDto { Prompt = new string('x', 1001) });
        await act.Should().ThrowAsync<AppValidationException>();
    }

    [Fact]
    public async Task GenerateAsync_throws_when_disabled()
    {
        var svc = BuildService(out _, provider: "disabled");
        var act = () => svc.GenerateAsync(Guid.NewGuid(), new GenerateImageDto { Prompt = "hi" });
        await act.Should().ThrowAsync<AppValidationException>();
    }

    [Fact]
    public void SizeOptions_returns_five_options()
    {
        var svc = BuildService(out _);
        svc.SizeOptions().Should().HaveCount(5);
    }
}
