using System.Security.Claims;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketingApp.API.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize] // role check is enforced per-endpoint
public class AdminController : ControllerBase
{
    private readonly ISystemSettingsService _settings;

    public AdminController(ISystemSettingsService settings)
    {
        _settings = settings;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsAdmin() => string.Equals(User.FindFirstValue(ClaimTypes.Role), "admin", StringComparison.OrdinalIgnoreCase);

    /// <summary>Read platform settings — visible to everyone (so users can see send rate too).</summary>
    [HttpGet("system-settings")]
    public async Task<IActionResult> GetSystemSettings(CancellationToken ct)
    {
        var dto = await _settings.GetAsync(ct);
        return Ok(ApiResponse<SystemSettingsDto>.Ok(dto));
    }

    /// <summary>Update platform settings — admin only.</summary>
    [HttpPut("system-settings")]
    public async Task<IActionResult> UpdateSystemSettings([FromBody] UpdateSystemSettingsDto dto, CancellationToken ct)
    {
        if (!IsAdmin())
            return StatusCode(403, ApiResponse<object>.Fail("Only admins can change platform settings."));

        var updated = await _settings.UpdateAsync(GetUserId(), dto, ct);
        return Ok(ApiResponse<SystemSettingsDto>.Ok(updated, "Settings updated."));
    }
}
