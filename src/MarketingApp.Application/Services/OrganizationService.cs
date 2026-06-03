using System.Text;
using System.Text.RegularExpressions;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Domain.Entities;
using MarketingApp.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Application.Services;

/// <summary>
/// Organization (tenant) management — P2.4 foundation. Additive + zero-regression: creating orgs and
/// assigning users only writes the new organizations table / users.organization_id column. No
/// existing read path filters on org yet (that is gated by SystemSettings.EnableMultiTenancy), so
/// today's behaviour is unchanged.
/// </summary>
public class OrganizationService : IOrganizationService
{
    private readonly IGenericRepository<Organization> _orgRepo;
    private readonly IGenericRepository<User> _userRepo;
    private readonly IAuditService _audit;
    private readonly ILogger<OrganizationService> _logger;

    public OrganizationService(
        IGenericRepository<Organization> orgRepo,
        IGenericRepository<User> userRepo,
        IAuditService audit,
        ILogger<OrganizationService> logger)
    {
        _orgRepo = orgRepo;
        _userRepo = userRepo;
        _audit = audit;
        _logger = logger;
    }

    public async Task<IEnumerable<OrganizationDto>> ListAsync(CancellationToken ct = default)
    {
        var orgs = await _orgRepo.GetAllAsync(ct);
        var users = await _userRepo.GetAllAsync(ct);
        var counts = users
            .Where(u => u.OrganizationId.HasValue)
            .GroupBy(u => u.OrganizationId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        return orgs
            .OrderByDescending(o => o.Id == Organization.LegacyOrgId)
            .ThenBy(o => o.Name)
            .Select(o => ToDto(o, counts.GetValueOrDefault(o.Id)))
            .ToList();
    }

    public async Task<OrganizationDto?> GetForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userRepo.GetByIdAsync(userId, ct);
        if (user?.OrganizationId is not Guid orgId) return null;
        var org = await _orgRepo.GetByIdAsync(orgId, ct);
        if (org is null) return null;
        var count = await _userRepo.CountAsync(u => u.OrganizationId == orgId, ct);
        return ToDto(org, count);
    }

    public async Task<OrganizationDto> CreateAsync(Guid actorId, CreateOrganizationDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new AppValidationException("Organization name is required.");

        var slug = string.IsNullOrWhiteSpace(dto.Slug) ? Slugify(dto.Name) : Slugify(dto.Slug);
        if (string.IsNullOrEmpty(slug))
            throw new AppValidationException("Could not derive a valid slug from the name.");

        if (await _orgRepo.AnyAsync(o => o.Slug == slug, ct))
            throw new ConflictException($"An organization with slug '{slug}' already exists.");

        var org = new Organization
        {
            Name = dto.Name.Trim(),
            Slug = slug,
            OwnerUserId = actorId,
            PlanCode = string.IsNullOrWhiteSpace(dto.PlanCode) ? "free" : dto.PlanCode!.Trim().ToLowerInvariant(),
        };
        await _orgRepo.AddAsync(org, ct);
        await _audit.LogAsync(actorId, "OrganizationCreated", "Organization", org.Id, ct: ct);
        _logger.LogInformation("Organization {OrgId} '{Name}' created by {Actor}", org.Id, org.Name, actorId);
        return ToDto(org, 0);
    }

    public async Task<OrganizationDto> AssignUserAsync(Guid actorId, Guid userId, Guid organizationId, CancellationToken ct = default)
    {
        var org = await _orgRepo.GetByIdAsync(organizationId, ct)
            ?? throw new NotFoundException("Organization", organizationId);
        var user = await _userRepo.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User", userId);

        user.OrganizationId = organizationId;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepo.UpdateAsync(user, ct);
        await _audit.LogAsync(actorId, "OrganizationUserAssigned", "Organization", organizationId, ct: ct);

        var count = await _userRepo.CountAsync(u => u.OrganizationId == organizationId, ct);
        return ToDto(org, count);
    }

    private static OrganizationDto ToDto(Organization o, int userCount) => new()
    {
        Id = o.Id,
        Name = o.Name,
        Slug = o.Slug,
        OwnerUserId = o.OwnerUserId,
        PlanCode = o.PlanCode,
        IsActive = o.IsActive,
        UserCount = userCount,
        IsLegacy = o.Id == Organization.LegacyOrgId,
        CreatedAt = o.CreatedAt,
    };

    /// <summary>
    /// URL-safe slug: lowercased, non-alphanumerics → single hyphens, trimmed, max 80 chars.
    /// Pure + deterministic so it's unit-testable.
    /// </summary>
    internal static string Slugify(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var lower = input.Trim().ToLowerInvariant();
        var sb = new StringBuilder(lower.Length);
        foreach (var ch in lower)
            // ASCII alphanumerics only (already lowercased) → everything else becomes a separator,
            // so the slug is guaranteed URL-safe even for accented / non-Latin input.
            sb.Append(ch is (>= 'a' and <= 'z') or (>= '0' and <= '9') ? ch : '-');
        var collapsed = Regex.Replace(sb.ToString(), "-+", "-").Trim('-');
        return collapsed.Length > 80 ? collapsed[..80].Trim('-') : collapsed;
    }
}
