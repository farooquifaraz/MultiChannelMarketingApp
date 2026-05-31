using MarketingApp.Application.DTOs;

namespace MarketingApp.Application.Interfaces;

public interface IAdminUserService
{
    Task<IEnumerable<AdminUserDto>> ListAsync(string? role, Guid? smtpGroupId, string? search, bool? isActive, CancellationToken ct = default);
    Task<AdminUserDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AdminUserDto> CreateAsync(Guid actorId, CreateUserDto dto, CancellationToken ct = default);
    Task<AdminUserDto> UpdateAsync(Guid id, UpdateUserDto dto, CancellationToken ct = default);
    Task<AdminUserDto> ChangeRoleAsync(Guid id, string role, CancellationToken ct = default);
    Task<AdminUserDto> ResetPasswordAsync(Guid id, string newPassword, CancellationToken ct = default);
    Task DeleteAsync(Guid id, Guid actorId, CancellationToken ct = default);
}
