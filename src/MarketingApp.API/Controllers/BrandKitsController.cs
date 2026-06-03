using System.Security.Claims;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketingApp.API.Controllers;

/// <summary>Per-user brand kits (P3.2) — logo + colors + font applied to AI banner generation.</summary>
[ApiController]
[Route("api/v1/brand-kits")]
[Authorize]
public class BrandKitsController : ControllerBase
{
    private readonly IBrandKitService _service;

    public BrandKitsController(IBrandKitService service) { _service = service; }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<IEnumerable<BrandKitDto>>.Ok(await _service.ListMineAsync(GetUserId(), ct)));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBrandKitDto dto, CancellationToken ct)
        => Ok(ApiResponse<BrandKitDto>.Ok(await _service.CreateAsync(GetUserId(), dto, ct)));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBrandKitDto dto, CancellationToken ct)
        => Ok(ApiResponse<BrandKitDto>.Ok(await _service.UpdateAsync(GetUserId(), id, dto, ct)));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(GetUserId(), id, ct);
        return Ok(ApiResponse<object>.Ok(new { deleted = true }));
    }
}
