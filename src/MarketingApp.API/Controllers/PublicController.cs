using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace MarketingApp.API.Controllers;

/// <summary>
/// Public (anonymous) endpoints — safe-to-share branding for the login/register screens.
/// Never expose security-sensitive settings here.
/// </summary>
[ApiController]
[Route("api/v1/public")]
public class PublicController : ControllerBase
{
    private readonly ISystemSettingsService _settings;

    public PublicController(ISystemSettingsService settings) { _settings = settings; }

    public record BrandInfoDto(string PlatformName, string? LogoUrl, string PrimaryColor);

    [HttpGet("brand")]
    public async Task<IActionResult> GetBrand(CancellationToken ct)
    {
        var s = await _settings.GetAsync(ct);
        var brand = new BrandInfoDto(s.PlatformName, s.LogoUrl, s.PrimaryColor);
        return Ok(ApiResponse<BrandInfoDto>.Ok(brand));
    }
}
