using MarketingApp.Application.DTOs;

namespace MarketingApp.Application.Interfaces;

/// <summary>
/// Organization (tenant) management — P2.4 foundation slice. Provides the data model + admin CRUD
/// for orgs and user-to-org assignment. Query-level tenant isolation is NOT performed here; it is a
/// later slice gated by SystemSettings.EnableMultiTenancy. Until then this is a pure grouping layer.
/// </summary>
public interface IOrganizationService
{
    /// <summary>All organizations with their user counts (admin), Legacy first.</summary>
    Task<IEnumerable<OrganizationDto>> ListAsync(CancellationToken ct = default);

    /// <summary>The organization the given user belongs to (or null if unassigned).</summary>
    Task<OrganizationDto?> GetForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Create a new organization. Throws on duplicate slug. Slug auto-derived from name if blank.</summary>
    Task<OrganizationDto> CreateAsync(Guid actorId, CreateOrganizationDto dto, CancellationToken ct = default);

    /// <summary>Move a user into an organization. Throws if user or org not found.</summary>
    Task<OrganizationDto> AssignUserAsync(Guid actorId, Guid userId, Guid organizationId, CancellationToken ct = default);

    /// <summary>Rename / re-plan an organization. Cannot edit the Legacy org.</summary>
    Task<OrganizationDto> UpdateAsync(Guid actorId, Guid orgId, UpdateOrganizationDto dto, CancellationToken ct = default);

    /// <summary>Delete an organization. Cannot delete Legacy; any members are moved back to Legacy first.</summary>
    Task DeleteAsync(Guid actorId, Guid orgId, CancellationToken ct = default);
}
