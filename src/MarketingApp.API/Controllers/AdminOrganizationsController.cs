using System.Security.Claims;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketingApp.API.Controllers;

/// <summary>
/// Organization (tenant) administration — P2.4 foundation. List orgs, create orgs, and assign users
/// to an org. This is the data-model + management layer only; per-org data isolation is gated behind
/// SystemSettings.EnableMultiTenancy and not enforced by these endpoints.
/// </summary>
[ApiController]
[Route("api/v1/admin/organizations")]
[Authorize]
public class AdminOrganizationsController : ControllerBase
{
    private readonly IOrganizationService _service;

    public AdminOrganizationsController(IOrganizationService service) { _service = service; }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsAdmin() => string.Equals(User.FindFirstValue(ClaimTypes.Role), "admin", StringComparison.OrdinalIgnoreCase);
    private IActionResult? GuardAdmin() =>
        IsAdmin() ? null : StatusCode(403, ApiResponse<object>.Fail("Admin role required."));

    /// <summary>All organizations with user counts (Legacy first).</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (GuardAdmin() is { } forbidden) return forbidden;
        var orgs = await _service.ListAsync(ct);
        return Ok(ApiResponse<IEnumerable<OrganizationDto>>.Ok(orgs));
    }

    /// <summary>Create a new organization.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrganizationDto dto, CancellationToken ct)
    {
        if (GuardAdmin() is { } forbidden) return forbidden;
        var org = await _service.CreateAsync(GetUserId(), dto, ct);
        return Ok(ApiResponse<OrganizationDto>.Ok(org));
    }

    /// <summary>Move a user into an organization.</summary>
    [HttpPost("assign-user")]
    public async Task<IActionResult> AssignUser([FromBody] AssignUserToOrgDto dto, CancellationToken ct)
    {
        if (GuardAdmin() is { } forbidden) return forbidden;
        var org = await _service.AssignUserAsync(GetUserId(), dto.UserId, dto.OrganizationId, ct);
        return Ok(ApiResponse<OrganizationDto>.Ok(org));
    }
}
