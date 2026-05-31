using MarketingApp.Application.DTOs;
using MarketingApp.Domain.Entities;

namespace MarketingApp.Application.Interfaces;

public interface ISmtpGroupService
{
    Task<IEnumerable<SmtpGroupDto>> GetAllAsync(CancellationToken ct = default);
    Task<SmtpGroupDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<SmtpGroupDto> CreateAsync(Guid createdByUserId, CreateSmtpGroupDto dto, CancellationToken ct = default);

    /// <summary>
    /// Duplicates an existing SmtpGroup (all provider/IMAP/signature config INCLUDING secrets) into a new
    /// group named "Copy of {name}". The clone is never default, has no user assignments, and its IMAP
    /// dedup cursor is reset — so admins can quickly spin up a per-user mailbox group and just tweak the
    /// From email + credentials, without re-entering everything.
    /// </summary>
    Task<SmtpGroupDto> CloneAsync(Guid sourceId, Guid createdByUserId, CancellationToken ct = default);
    Task<SmtpGroupDto> UpdateAsync(Guid id, UpdateSmtpGroupDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<SmtpGroupDto> SetDefaultAsync(Guid id, CancellationToken ct = default);
    Task<int> AssignUsersAsync(Guid groupId, List<Guid> userIds, CancellationToken ct = default);
    Task<int> UnassignUsersAsync(List<Guid> userIds, CancellationToken ct = default);
    Task<bool> TestConnectionAsync(Guid groupId, string testEmail, CancellationToken ct = default);
    Task<IEnumerable<UserAssignmentDto>> GetUserAssignmentsAsync(CancellationToken ct = default);

    /// <summary>
    /// Resolves the SmtpGroup that should be used when the given user sends a campaign.
    /// Order: (1) user's explicitly assigned group → (2) the platform default group → (3) null.
    /// </summary>
    Task<SmtpGroup?> ResolveForUserAsync(Guid userId, CancellationToken ct = default);
}
