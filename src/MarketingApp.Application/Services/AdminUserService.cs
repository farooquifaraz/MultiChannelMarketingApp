using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Domain.Entities;
using MarketingApp.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Application.Services;

public class AdminUserService : IAdminUserService
{
    private readonly IGenericRepository<User> _userRepo;
    private readonly IGenericRepository<SmtpGroup> _smtpGroupRepo;
    private readonly ISystemSettingsService _systemSettings;
    private readonly IAuditService _audit;
    private readonly ILogger<AdminUserService> _logger;

    public AdminUserService(
        IGenericRepository<User> userRepo,
        IGenericRepository<SmtpGroup> smtpGroupRepo,
        ISystemSettingsService systemSettings,
        IAuditService audit,
        ILogger<AdminUserService> logger)
    {
        _userRepo = userRepo;
        _smtpGroupRepo = smtpGroupRepo;
        _systemSettings = systemSettings;
        _audit = audit;
        _logger = logger;
    }

    private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase) { "user", "admin" };

    public async Task<IEnumerable<AdminUserDto>> ListAsync(string? role, Guid? smtpGroupId, string? search, bool? isActive, CancellationToken ct = default)
    {
        var users = await _userRepo.FindAsync(u => true, ct);
        var groups = (await _smtpGroupRepo.FindAsync(g => true, ct))
            .ToDictionary(g => g.Id, g => g.Name);

        var filtered = users.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(role))
            filtered = filtered.Where(u => string.Equals(u.Role, role, StringComparison.OrdinalIgnoreCase));
        if (smtpGroupId.HasValue)
            filtered = filtered.Where(u => u.SmtpGroupId == smtpGroupId.Value);
        if (isActive.HasValue)
            filtered = filtered.Where(u => u.IsActive == isActive.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            filtered = filtered.Where(u =>
                u.FullName.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                u.Email.Contains(s, StringComparison.OrdinalIgnoreCase));
        }

        return filtered
            .OrderByDescending(u => u.Role == "admin")
            .ThenBy(u => u.Email)
            .Select(u => ToDto(u, groups));
    }

    public async Task<AdminUserDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _userRepo.GetByIdAsync(id, ct);
        if (user is null) return null;
        var groups = (await _smtpGroupRepo.FindAsync(g => true, ct)).ToDictionary(g => g.Id, g => g.Name);
        return ToDto(user, groups);
    }

    public async Task<AdminUserDto> CreateAsync(Guid actorId, CreateUserDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName))
            throw new AppValidationException(new List<string> { "Full name is required." });
        if (string.IsNullOrWhiteSpace(dto.Email) || !dto.Email.Contains('@'))
            throw new AppValidationException(new List<string> { "Valid email is required." });

        var sys = await _systemSettings.GetAsync(ct);
        var minLen = Math.Max(4, sys.PasswordMinLength);
        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < minLen)
            throw new AppValidationException(new List<string> { $"Password must be at least {minLen} characters." });

        var role = AllowedRoles.Contains(dto.Role) ? dto.Role.ToLowerInvariant() : "user";

        // Email must be unique (case-insensitive)
        var existing = (await _userRepo.FindAsync(
            u => u.Email.ToLower() == dto.Email.ToLower(), ct)).FirstOrDefault();
        if (existing is not null)
            throw new ConflictException($"A user with email '{dto.Email}' already exists.");

        var user = new User
        {
            FullName = dto.FullName.Trim(),
            Email = dto.Email.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = role,
            IsActive = true,
            SmtpGroupId = dto.SmtpGroupId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await _userRepo.AddAsync(user, ct);
        await _audit.LogAsync(actorId, "UserCreated", "User", user.Id, new { user.Email, user.Role }, ct: ct);
        _logger.LogInformation("Admin {ActorId} created user {Email} role={Role}", actorId, user.Email, user.Role);

        var groups = (await _smtpGroupRepo.FindAsync(g => true, ct)).ToDictionary(g => g.Id, g => g.Name);
        return ToDto(user, groups);
    }

    public async Task<AdminUserDto> UpdateAsync(Guid id, UpdateUserDto dto, CancellationToken ct = default)
    {
        var user = await _userRepo.GetByIdAsync(id, ct) ?? throw new NotFoundException("User", id);
        if (!string.IsNullOrWhiteSpace(dto.FullName)) user.FullName = dto.FullName.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            // Email uniqueness
            var conflict = (await _userRepo.FindAsync(
                u => u.Email.ToLower() == dto.Email.ToLower() && u.Id != id, ct)).FirstOrDefault();
            if (conflict is not null)
                throw new ConflictException($"Another user already has email '{dto.Email}'.");
            user.Email = dto.Email.Trim();
        }
        user.SmtpGroupId = dto.SmtpGroupId;
        user.IsActive = dto.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepo.UpdateAsync(user, ct);

        var groups = (await _smtpGroupRepo.FindAsync(g => true, ct)).ToDictionary(g => g.Id, g => g.Name);
        return ToDto(user, groups);
    }

    public async Task<AdminUserDto> ChangeRoleAsync(Guid id, string role, CancellationToken ct = default)
    {
        var user = await _userRepo.GetByIdAsync(id, ct) ?? throw new NotFoundException("User", id);
        if (!AllowedRoles.Contains(role))
            throw new AppValidationException(new List<string> { "Role must be 'user' or 'admin'." });

        // If demoting last admin, block to avoid lockout
        if (string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            var adminCount = (await _userRepo.FindAsync(
                u => u.Role == "admin" && u.IsActive && u.Id != id, ct)).Count();
            if (adminCount == 0)
                throw new ConflictException("Cannot demote the last active admin. Promote another user first.");
        }

        user.Role = role.ToLowerInvariant();
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepo.UpdateAsync(user, ct);

        var groups = (await _smtpGroupRepo.FindAsync(g => true, ct)).ToDictionary(g => g.Id, g => g.Name);
        return ToDto(user, groups);
    }

    public async Task<AdminUserDto> ResetPasswordAsync(Guid id, string newPassword, CancellationToken ct = default)
    {
        var user = await _userRepo.GetByIdAsync(id, ct) ?? throw new NotFoundException("User", id);
        var sys = await _systemSettings.GetAsync(ct);
        var minLen = Math.Max(4, sys.PasswordMinLength);
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < minLen)
            throw new AppValidationException(new List<string> { $"Password must be at least {minLen} characters." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepo.UpdateAsync(user, ct);
        _logger.LogInformation("Password reset for user {UserId}", id);

        var groups = (await _smtpGroupRepo.FindAsync(g => true, ct)).ToDictionary(g => g.Id, g => g.Name);
        return ToDto(user, groups);
    }

    public async Task DeleteAsync(Guid id, Guid actorId, CancellationToken ct = default)
    {
        var user = await _userRepo.GetByIdAsync(id, ct) ?? throw new NotFoundException("User", id);
        if (user.Id == actorId)
            throw new ConflictException("You cannot delete your own account.");

        // If user is the LAST active admin, block
        if (string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase) && user.IsActive)
        {
            var otherAdmins = (await _userRepo.FindAsync(
                u => u.Role == "admin" && u.IsActive && u.Id != id, ct)).Count();
            if (otherAdmins == 0)
                throw new ConflictException("Cannot delete the last active admin.");
        }

        // Soft delete — keep the row for referential integrity (campaigns, audit, etc.)
        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepo.UpdateAsync(user, ct);
        await _audit.LogAsync(actorId, "UserDeactivated", "User", id, new { user.Email }, ct: ct);
        _logger.LogInformation("User {UserId} deactivated by {ActorId}", id, actorId);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static AdminUserDto ToDto(User u, IDictionary<Guid, string> groupNames) => new()
    {
        Id = u.Id,
        FullName = u.FullName,
        Email = u.Email,
        Role = u.Role,
        IsActive = u.IsActive,
        SmtpGroupId = u.SmtpGroupId,
        SmtpGroupName = u.SmtpGroupId.HasValue && groupNames.TryGetValue(u.SmtpGroupId.Value, out var n) ? n : null,
        CreatedAt = u.CreatedAt,
        UpdatedAt = u.UpdatedAt,
        HasPersonalSignature = !string.IsNullOrWhiteSpace(u.SignatureDesignation)
                                || !string.IsNullOrWhiteSpace(u.SignaturePhone)
                                || !string.IsNullOrWhiteSpace(u.SignatureImageUrl),
    };
}
