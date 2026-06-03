using System.Security.Claims;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketingApp.API.Controllers;

/// <summary>
/// AI Banner / Flyer studio (Phase 3). Generate images from a prompt and list your generated assets.
/// Works keyless via the mock provider; real providers (DALL·E) activate when configured in admin
/// Image settings.
/// </summary>
[ApiController]
[Route("api/v1/creatives")]
[Authorize]
public class CreativesController : ControllerBase
{
    private readonly IImageGenerationService _service;

    public CreativesController(IImageGenerationService service) { _service = service; }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Allowed image sizes for the picker.</summary>
    [HttpGet("sizes")]
    public IActionResult Sizes() =>
        Ok(ApiResponse<IEnumerable<ImageSizeOptionDto>>.Ok(_service.SizeOptions()));

    /// <summary>Generate an image from a prompt + size.</summary>
    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateImageDto dto, CancellationToken ct)
    {
        var asset = await _service.GenerateAsync(GetUserId(), dto, ct);
        return Ok(ApiResponse<GeneratedAssetDto>.Ok(asset));
    }

    /// <summary>The current user's recent generated assets (newest first).</summary>
    [HttpGet("assets")]
    public async Task<IActionResult> Assets([FromQuery] int take = 50, CancellationToken ct = default)
    {
        var assets = await _service.ListMineAsync(GetUserId(), take, ct);
        return Ok(ApiResponse<IEnumerable<GeneratedAssetDto>>.Ok(assets));
    }
}
