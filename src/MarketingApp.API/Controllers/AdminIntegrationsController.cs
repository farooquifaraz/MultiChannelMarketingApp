using System.Security.Claims;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketingApp.API.Controllers;

/// <summary>
/// Per-provider integration credential vault (P3.5). One simple place to save each provider's key and
/// pick which one is active per category (ai | image | payment).
/// </summary>
[ApiController]
[Route("api/v1/admin/integrations")]
[Authorize]
public class AdminIntegrationsController : ControllerBase
{
    private readonly IIntegrationCredentialService _service;

    public AdminIntegrationsController(IIntegrationCredentialService service) { _service = service; }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsAdmin() => string.Equals(User.FindFirstValue(ClaimTypes.Role), "admin", StringComparison.OrdinalIgnoreCase);
    private IActionResult? GuardAdmin() => IsAdmin() ? null : StatusCode(403, ApiResponse<object>.Fail("Admin role required."));

    /// <summary>All saved credentials for a category + which provider is active.</summary>
    [HttpGet("{category}")]
    public async Task<IActionResult> Get(string category, CancellationToken ct)
    {
        if (GuardAdmin() is { } f) return f;
        return Ok(ApiResponse<IntegrationCategoryDto>.Ok(await _service.GetAsync(category, ct)));
    }

    /// <summary>Save/update one provider's key (blank key = keep existing).</summary>
    [HttpPut("{category}/{provider}")]
    public async Task<IActionResult> Save(string category, string provider, [FromBody] SaveCredentialDto dto, CancellationToken ct)
    {
        if (GuardAdmin() is { } f) return f;
        return Ok(ApiResponse<IntegrationCategoryDto>.Ok(await _service.SaveAsync(GetUserId(), category, provider, dto, ct)));
    }

    /// <summary>Make a provider the active/enabled one for its category.</summary>
    [HttpPost("{category}/{provider}/activate")]
    public async Task<IActionResult> Activate(string category, string provider, CancellationToken ct)
    {
        if (GuardAdmin() is { } f) return f;
        return Ok(ApiResponse<IntegrationCategoryDto>.Ok(await _service.SetActiveAsync(GetUserId(), category, provider, ct)));
    }
}
