using FluentAssertions;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Application.Services;
using MarketingApp.Domain.Entities;
using MarketingApp.Domain.Exceptions;
using MarketingApp.Infrastructure.Services.Media;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq.Expressions;

namespace MarketingApp.Tests.Services;

/// <summary>
/// P3.2 — Brand Kits. Covers hex validation, the single-default invariant, ownership checks, the
/// brand-aware SVG, and brand-folding into the effective prompt.
/// </summary>
public class BrandKitTests
{
    // ---- hex validation (pure) ----

    [Theory]
    [InlineData("#fff", true)]
    [InlineData("#4f46e5", true)]
    [InlineData("#ABCDEF", true)]
    [InlineData("4f46e5", false)]
    [InlineData("#12", false)]
    [InlineData("#12345", false)]
    [InlineData("#xyzxyz", false)]
    [InlineData("", false)]
    public void IsValidHexColor(string value, bool expected)
        => BrandKitService.IsValidHexColor(value).Should().Be(expected);

    // ---- mock SVG honours brand colors safely ----

    [Fact]
    public void Svg_uses_brand_primary_and_name()
    {
        var svg = MockImageGenerationClient.BuildPlaceholderSvg("hello", 600, 600, "#ff0000", "#00ff00", "Acme Co");
        svg.Should().Contain("#ff0000").And.Contain("#00ff00").And.Contain("Acme Co");
    }

    [Fact]
    public void Svg_rejects_non_hex_brand_color()
    {
        // An attempt to inject markup via the color must be dropped (falls back to default).
        var svg = MockImageGenerationClient.BuildPlaceholderSvg("hi", 400, 400, "\"/><script>x", null, null);
        svg.Should().NotContain("<script>");
    }

    [Fact]
    public void SafeColor_validates()
    {
        MockImageGenerationClient.SafeColor("#abc", "#000").Should().Be("#abc");
        MockImageGenerationClient.SafeColor("nope", "#000").Should().Be("#000");
    }

    // ---- effective prompt folding ----

    [Fact]
    public void BuildEffectivePrompt_appends_brand_context()
    {
        var kit = new BrandKit { Name = "Acme", PrimaryColor = "#111111", SecondaryColor = "#222222", FontFamily = "Inter" };
        var p = ImageGenerationService.BuildEffectivePrompt("a flyer", kit);
        p.Should().StartWith("a flyer").And.Contain("Acme").And.Contain("#111111").And.Contain("Inter");
    }

    [Fact]
    public void BuildEffectivePrompt_unchanged_without_kit()
        => ImageGenerationService.BuildEffectivePrompt("a flyer", null).Should().Be("a flyer");

    // ---- service ----

    private static BrandKitService Build(out Mock<IGenericRepository<BrandKit>> repo, List<BrandKit>? store = null)
    {
        store ??= new List<BrandKit>();
        repo = new Mock<IGenericRepository<BrandKit>>();
        repo.Setup(r => r.AddAsync(It.IsAny<BrandKit>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BrandKit k, CancellationToken _) => { if (k.Id == Guid.Empty) k.Id = Guid.NewGuid(); store!.Add(k); return k; });
        repo.Setup(r => r.UpdateAsync(It.IsAny<BrandKit>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => store!.FirstOrDefault(k => k.Id == id));
        repo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<BrandKit, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<BrandKit, bool>> pred, CancellationToken _) => store!.Where(pred.Compile()).ToList());
        repo.Setup(r => r.DeleteAsync(It.IsAny<BrandKit>(), It.IsAny<CancellationToken>()))
            .Returns((BrandKit k, CancellationToken _) => { store!.Remove(k); return Task.CompletedTask; });
        return new BrandKitService(repo.Object, NullLogger<BrandKitService>.Instance);
    }

    [Fact]
    public async Task Create_persists_and_validates_color()
    {
        var svc = Build(out _);
        var dto = await svc.CreateAsync(Guid.NewGuid(), new CreateBrandKitDto { Name = "Acme", PrimaryColor = "#123456" });
        dto.Name.Should().Be("Acme");
        dto.PrimaryColor.Should().Be("#123456");
    }

    [Fact]
    public async Task Create_rejects_bad_color()
    {
        var svc = Build(out _);
        var act = () => svc.CreateAsync(Guid.NewGuid(), new CreateBrandKitDto { Name = "Acme", PrimaryColor = "red" });
        await act.Should().ThrowAsync<AppValidationException>();
    }

    [Fact]
    public async Task Create_rejects_blank_name()
    {
        var svc = Build(out _);
        var act = () => svc.CreateAsync(Guid.NewGuid(), new CreateBrandKitDto { Name = "  ", PrimaryColor = "#fff" });
        await act.Should().ThrowAsync<AppValidationException>();
    }

    [Fact]
    public async Task Setting_default_clears_other_defaults()
    {
        var userId = Guid.NewGuid();
        var store = new List<BrandKit>();
        var svc = Build(out _, store);
        await svc.CreateAsync(userId, new CreateBrandKitDto { Name = "A", PrimaryColor = "#111", IsDefault = true });
        await svc.CreateAsync(userId, new CreateBrandKitDto { Name = "B", PrimaryColor = "#222", IsDefault = true });

        store.Count(k => k.IsDefault).Should().Be(1);
        store.Single(k => k.IsDefault).Name.Should().Be("B");
    }

    [Fact]
    public async Task Update_rejects_other_users_kit()
    {
        var store = new List<BrandKit> { new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Name = "X", PrimaryColor = "#fff" } };
        var svc = Build(out _, store);
        var act = () => svc.UpdateAsync(Guid.NewGuid(), store[0].Id, new UpdateBrandKitDto { Name = "Y", PrimaryColor = "#000" });
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Delete_rejects_other_users_kit()
    {
        var store = new List<BrandKit> { new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Name = "X", PrimaryColor = "#fff" } };
        var svc = Build(out _, store);
        var act = () => svc.DeleteAsync(Guid.NewGuid(), store[0].Id);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task List_orders_default_first()
    {
        var userId = Guid.NewGuid();
        var store = new List<BrandKit>();
        var svc = Build(out _, store);
        await svc.CreateAsync(userId, new CreateBrandKitDto { Name = "Zeta", PrimaryColor = "#111" });
        await svc.CreateAsync(userId, new CreateBrandKitDto { Name = "Alpha", PrimaryColor = "#222", IsDefault = true });

        var list = (await svc.ListMineAsync(userId)).ToList();
        list[0].Name.Should().Be("Alpha");
        list[0].IsDefault.Should().BeTrue();
    }
}
