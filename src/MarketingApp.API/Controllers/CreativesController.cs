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
    private readonly IMarketingContentService _content;

    public CreativesController(IImageGenerationService service, IMarketingContentService content)
    {
        _service = service;
        _content = content;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Allowed image sizes for the picker.</summary>
    [HttpGet("sizes")]
    public IActionResult Sizes() =>
        Ok(ApiResponse<IEnumerable<ImageSizeOptionDto>>.Ok(_service.SizeOptions()));

    public record PromptPreset(string Title, string Prompt);

    /// <summary>Starter prompt presets to seed the composer (P3.2).</summary>
    [HttpGet("presets")]
    public IActionResult Presets() => Ok(ApiResponse<IEnumerable<PromptPreset>>.Ok(new[]
    {
        new PromptPreset("Property listing", "A clean real-estate listing flyer for a modern 3-bedroom apartment, bright photography style, price and key features prominent"),
        new PromptPreset("Festival greeting", "An elegant festive greeting banner with warm celebratory colors, subtle decorative motifs, and space for a short message"),
        new PromptPreset("Cold-lead follow-up", "A professional, minimal follow-up banner for a B2B outreach email, trustworthy corporate look, single clear call-to-action"),
        new PromptPreset("Product launch", "A bold product-launch announcement banner, high-energy gradient background, large headline space, social-media ready"),
        new PromptPreset("Discount / sale", "An eye-catching limited-time sale banner with a big percentage-off badge and urgency, vibrant contrasting colors"),
    }));

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

    /// <summary>Generate multi-channel marketing copy (WhatsApp/Instagram/Email) from a brief.</summary>
    [HttpPost("content")]
    public async Task<IActionResult> Content([FromBody] GenerateContentDto dto, CancellationToken ct)
    {
        var result = await _content.GenerateAsync(dto, ct);
        return Ok(ApiResponse<MarketingContentDto>.Ok(result));
    }

    /// <summary>Regenerate ONLY the hero-image prompt for a brief (cheap — for "don't like the prompt").</summary>
    [HttpPost("image-prompt")]
    public async Task<IActionResult> ImagePrompt([FromBody] GenerateContentDto dto, CancellationToken ct)
    {
        var prompt = await _content.GenerateImagePromptAsync(dto.Brief, ct);
        return Ok(ApiResponse<object>.Ok(new { imagePrompt = prompt }));
    }

    /// <summary>Delete one of the current user's generated banners.</summary>
    [HttpDelete("assets/{id:guid}")]
    public async Task<IActionResult> DeleteAsset(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(GetUserId(), id, ct);
        return Ok(ApiResponse<object>.Ok(new { deleted = true }));
    }
}
