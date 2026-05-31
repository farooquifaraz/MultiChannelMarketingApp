using System.Security.Claims;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketingApp.API.Controllers;

[ApiController]
[Route("api/v1/admin/users")]
[Authorize]
public class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _service;

    public AdminUsersController(IAdminUserService service) { _service = service; }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsAdmin() => string.Equals(User.FindFirstValue(ClaimTypes.Role), "admin", StringComparison.OrdinalIgnoreCase);
    private IActionResult? GuardAdmin() =>
        IsAdmin() ? null : StatusCode(403, ApiResponse<object>.Fail("Admin role required."));

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? role = null,
        [FromQuery] Guid? smtpGroupId = null,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        if (GuardAdmin() is { } forbidden) return forbidden;
        var users = await _service.ListAsync(role, smtpGroupId, search, isActive, ct);
        return Ok(ApiResponse<IEnumerable<AdminUserDto>>.Ok(users));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        if (GuardAdmin() is { } forbidden) return forbidden;
        var user = await _service.GetByIdAsync(id, ct);
        if (user is null) return NotFound(ApiResponse<object>.Fail("User not found."));
        return Ok(ApiResponse<AdminUserDto>.Ok(user));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserDto dto, CancellationToken ct)
    {
        if (GuardAdmin() is { } forbidden) return forbidden;
        var created = await _service.CreateAsync(GetUserId(), dto, ct);
        return StatusCode(201, ApiResponse<AdminUserDto>.Ok(created, "User created."));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserDto dto, CancellationToken ct)
    {
        if (GuardAdmin() is { } forbidden) return forbidden;
        var updated = await _service.UpdateAsync(id, dto, ct);
        return Ok(ApiResponse<AdminUserDto>.Ok(updated, "User updated."));
    }

    [HttpPut("{id:guid}/role")]
    public async Task<IActionResult> ChangeRole(Guid id, [FromBody] ChangeRoleDto dto, CancellationToken ct)
    {
        if (GuardAdmin() is { } forbidden) return forbidden;
        var updated = await _service.ChangeRoleAsync(id, dto.Role, ct);
        return Ok(ApiResponse<AdminUserDto>.Ok(updated, $"Role changed to '{updated.Role}'."));
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordDto dto, CancellationToken ct)
    {
        if (GuardAdmin() is { } forbidden) return forbidden;
        await _service.ResetPasswordAsync(id, dto.NewPassword, ct);
        return Ok(ApiResponse<object>.Ok(null!, "Password reset successfully."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (GuardAdmin() is { } forbidden) return forbidden;
        await _service.DeleteAsync(id, GetUserId(), ct);
        return Ok(ApiResponse<object>.Ok(null!, "User deactivated."));
    }
}
