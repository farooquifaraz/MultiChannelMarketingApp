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
/// P2.4 — Organization (tenant) foundation. Covers the pure Slugify helper and the service's
/// create / assign / list behaviour. These verify the additive grouping layer only — no test here
/// asserts query-level isolation, because that is deliberately NOT enabled in this slice.
/// </summary>
public class OrganizationServiceTests
{
    // ---- Slugify (pure) ----

    [Theory]
    [InlineData("Acme Corp", "acme-corp")]
    [InlineData("  Hello   World  ", "hello-world")]
    [InlineData("ACME!!!", "acme")]
    [InlineData("a/b\\c.d", "a-b-c-d")]
    [InlineData("Dubai Real-Estate", "dubai-real-estate")]
    [InlineData("---weird---", "weird")]
    [InlineData("Café 123", "caf-123")]
    public void Slugify_produces_url_safe_slug(string input, string expected)
        => OrganizationService.Slugify(input).Should().Be(expected);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    public void Slugify_returns_empty_for_no_alphanumerics(string input)
        => OrganizationService.Slugify(input).Should().BeEmpty();

    [Fact]
    public void Slugify_truncates_to_80_chars()
        => OrganizationService.Slugify(new string('a', 200)).Length.Should().BeLessThanOrEqualTo(80);

    [Fact]
    public void LegacyOrgId_is_stable()
        => Organization.LegacyOrgId.Should().Be(Guid.Parse("00000000-0000-0000-0000-00000000ace0"));

    // ---- Service ----

    private static OrganizationService Build(
        Mock<IGenericRepository<Organization>> orgRepo,
        Mock<IGenericRepository<User>> userRepo)
    {
        var audit = new Mock<IAuditService>();
        audit.Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<Guid?>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return new OrganizationService(orgRepo.Object, userRepo.Object, audit.Object,
            NullLogger<OrganizationService>.Instance);
    }

    [Fact]
    public async Task CreateAsync_derives_slug_and_persists()
    {
        var orgRepo = new Mock<IGenericRepository<Organization>>();
        orgRepo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<Organization, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        Organization? saved = null;
        orgRepo.Setup(r => r.AddAsync(It.IsAny<Organization>(), It.IsAny<CancellationToken>()))
            .Callback<Organization, CancellationToken>((o, _) => saved = o)
            .ReturnsAsync((Organization o, CancellationToken _) => o);
        var userRepo = new Mock<IGenericRepository<User>>();
        var svc = Build(orgRepo, userRepo);

        var actor = Guid.NewGuid();
        var dto = await svc.CreateAsync(actor, new CreateOrganizationDto { Name = "Acme Corp" });

        dto.Slug.Should().Be("acme-corp");
        dto.Name.Should().Be("Acme Corp");
        dto.PlanCode.Should().Be("free");
        saved.Should().NotBeNull();
        saved!.OwnerUserId.Should().Be(actor);
    }

    [Fact]
    public async Task CreateAsync_rejects_blank_name()
    {
        var svc = Build(new Mock<IGenericRepository<Organization>>(), new Mock<IGenericRepository<User>>());
        var act = () => svc.CreateAsync(Guid.NewGuid(), new CreateOrganizationDto { Name = "   " });
        await act.Should().ThrowAsync<AppValidationException>();
    }

    [Fact]
    public async Task CreateAsync_rejects_duplicate_slug()
    {
        var orgRepo = new Mock<IGenericRepository<Organization>>();
        orgRepo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<Organization, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var svc = Build(orgRepo, new Mock<IGenericRepository<User>>());
        var act = () => svc.CreateAsync(Guid.NewGuid(), new CreateOrganizationDto { Name = "Acme" });
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task AssignUserAsync_sets_organization_id()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var org = new Organization { Id = orgId, Name = "Acme", Slug = "acme" };
        var user = new User { Id = userId, Email = "u@x.com" };

        var orgRepo = new Mock<IGenericRepository<Organization>>();
        orgRepo.Setup(r => r.GetByIdAsync(orgId, It.IsAny<CancellationToken>())).ReturnsAsync(org);
        var userRepo = new Mock<IGenericRepository<User>>();
        userRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        userRepo.Setup(r => r.CountAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var svc = Build(orgRepo, userRepo);

        var dto = await svc.AssignUserAsync(Guid.NewGuid(), userId, orgId);

        user.OrganizationId.Should().Be(orgId);
        dto.UserCount.Should().Be(1);
        userRepo.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssignUserAsync_throws_when_org_missing()
    {
        var orgRepo = new Mock<IGenericRepository<Organization>>();
        orgRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Organization?)null);
        var svc = Build(orgRepo, new Mock<IGenericRepository<User>>());
        var act = () => svc.AssignUserAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ListAsync_orders_legacy_first_with_counts()
    {
        var legacy = new Organization { Id = Organization.LegacyOrgId, Name = "Legacy Organization", Slug = "legacy" };
        var other = new Organization { Id = Guid.NewGuid(), Name = "Acme", Slug = "acme" };
        var orgRepo = new Mock<IGenericRepository<Organization>>();
        orgRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { other, legacy });
        var userRepo = new Mock<IGenericRepository<User>>();
        userRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new User { Id = Guid.NewGuid(), OrganizationId = Organization.LegacyOrgId },
                new User { Id = Guid.NewGuid(), OrganizationId = Organization.LegacyOrgId },
                new User { Id = Guid.NewGuid(), OrganizationId = other.Id },
            });
        var svc = Build(orgRepo, userRepo);

        var list = (await svc.ListAsync()).ToList();

        list[0].IsLegacy.Should().BeTrue();
        list[0].UserCount.Should().Be(2);
        list[1].Name.Should().Be("Acme");
        list[1].UserCount.Should().Be(1);
    }
}
