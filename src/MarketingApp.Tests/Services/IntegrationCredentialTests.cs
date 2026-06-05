using FluentAssertions;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Application.Services;
using MarketingApp.Domain.Entities;
using MarketingApp.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq.Expressions;

namespace MarketingApp.Tests.Services;

/// <summary>
/// P3.5 — per-provider credential vault. Verifies key masking, per-provider upsert (no cross-provider
/// key loss), and that activating/saving syncs the chosen credential into SystemSettings so the
/// existing AI/image/payment code paths keep working.
/// </summary>
public class IntegrationCredentialTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("short", "••••")]                              // <=12 chars fully masked
    [InlineData("AIzaSyDHpBOrd7gjGJ2I", "AIzaSy…gjGJ2I")]      // first6 … last6 for identification
    public void Mask_shows_first6_last6(string? input, string? expected)
        => IntegrationCredentialService.Mask(input).Should().Be(expected);

    private static (IntegrationCredentialService svc, List<IntegrationCredential> store, SystemSettings settings) Build()
    {
        var store = new List<IntegrationCredential>();
        var repo = new Mock<IGenericRepository<IntegrationCredential>>();
        repo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<IntegrationCredential, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<IntegrationCredential, bool>> p, CancellationToken _) => store.Where(p.Compile()).ToList());
        repo.Setup(r => r.AddAsync(It.IsAny<IntegrationCredential>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IntegrationCredential c, CancellationToken _) => { c.Id = Guid.NewGuid(); store.Add(c); return c; });
        repo.Setup(r => r.UpdateAsync(It.IsAny<IntegrationCredential>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var settings = new SystemSettings { AiProvider = "disabled", ImageProvider = "mock", PaymentProvider = "mock" };
        var settingsRepo = new Mock<IGenericRepository<SystemSettings>>();
        settingsRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { settings });
        settingsRepo.Setup(r => r.UpdateAsync(It.IsAny<SystemSettings>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var audit = new Mock<IAuditService>();
        audit.Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<Guid?>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return (new IntegrationCredentialService(repo.Object, settingsRepo.Object, audit.Object,
            NullLogger<IntegrationCredentialService>.Instance), store, settings);
    }

    [Fact]
    public async Task SaveAsync_creates_per_provider_without_touching_others()
    {
        var (svc, store, _) = Build();
        await svc.SaveAsync(Guid.NewGuid(), "ai", "openai", new SaveCredentialDto { ApiKey = "sk-openai", Model = "gpt-4o" });
        await svc.SaveAsync(Guid.NewGuid(), "ai", "gemini", new SaveCredentialDto { ApiKey = "AIza-gem" });

        store.Should().HaveCount(2);
        store.Single(c => c.Provider == "openai").ApiKey.Should().Be("sk-openai");
        store.Single(c => c.Provider == "gemini").ApiKey.Should().Be("AIza-gem");
    }

    [Fact]
    public async Task SaveAsync_blank_key_keeps_existing()
    {
        var (svc, store, _) = Build();
        await svc.SaveAsync(Guid.NewGuid(), "ai", "openai", new SaveCredentialDto { ApiKey = "sk-original" });
        await svc.SaveAsync(Guid.NewGuid(), "ai", "openai", new SaveCredentialDto { ApiKey = "  ", Model = "gpt-4o-mini" });

        var c = store.Single(x => x.Provider == "openai");
        c.ApiKey.Should().Be("sk-original");   // kept
        c.Model.Should().Be("gpt-4o-mini");    // updated
    }

    [Fact]
    public async Task SetActiveAsync_syncs_key_into_system_settings()
    {
        var (svc, _, settings) = Build();
        await svc.SaveAsync(Guid.NewGuid(), "ai", "openai", new SaveCredentialDto { ApiKey = "sk-openai", Model = "gpt-4o" });
        await svc.SetActiveAsync(Guid.NewGuid(), "ai", "openai");

        settings.AiProvider.Should().Be("openai");
        settings.AiApiKey.Should().Be("sk-openai");   // existing code reads this
        settings.AiModel.Should().Be("gpt-4o");
    }

    [Fact]
    public async Task Switching_active_provider_swaps_the_live_key()
    {
        var (svc, _, settings) = Build();
        await svc.SaveAsync(Guid.NewGuid(), "ai", "openai", new SaveCredentialDto { ApiKey = "sk-openai" });
        await svc.SaveAsync(Guid.NewGuid(), "ai", "gemini", new SaveCredentialDto { ApiKey = "AIza-gem" });

        await svc.SetActiveAsync(Guid.NewGuid(), "ai", "openai");
        settings.AiApiKey.Should().Be("sk-openai");

        await svc.SetActiveAsync(Guid.NewGuid(), "ai", "gemini");
        settings.AiApiKey.Should().Be("AIza-gem");   // swapped, neither key lost from the vault
    }

    [Fact]
    public async Task GetAsync_marks_active_and_masks_keys()
    {
        var (svc, _, _) = Build();
        await svc.SaveAsync(Guid.NewGuid(), "image", "dalle", new SaveCredentialDto { ApiKey = "sk-proj-longkey1234" });
        await svc.SetActiveAsync(Guid.NewGuid(), "image", "dalle");

        var dto = await svc.GetAsync("image");
        dto.ActiveProvider.Should().Be("dalle");
        var row = dto.Credentials.Single(c => c.Provider == "dalle");
        row.IsActive.Should().BeTrue();
        row.HasKey.Should().BeTrue();
        row.KeyMasked.Should().NotContain("longkey");
    }

    [Fact]
    public async Task Unknown_category_throws()
    {
        var (svc, _, _) = Build();
        var act = () => svc.GetAsync("bogus");
        await act.Should().ThrowAsync<AppValidationException>();
    }

    [Fact]
    public async Task Activating_keyless_provider_clears_active_secret()
    {
        var (svc, _, settings) = Build();
        await svc.SaveAsync(Guid.NewGuid(), "image", "dalle", new SaveCredentialDto { ApiKey = "sk-x" });
        await svc.SetActiveAsync(Guid.NewGuid(), "image", "dalle");
        settings.ImageApiKey.Should().Be("sk-x");

        await svc.SetActiveAsync(Guid.NewGuid(), "image", "mock");   // keyless
        settings.ImageProvider.Should().Be("mock");
        settings.ImageApiKey.Should().BeNull();
    }
}
