using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using System.Security.Claims;

namespace MarketingApp.API.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<DashboardStatsDto>), 200)]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var result = await _dashboardService.GetStatsAsync(CurrentUserId, ct);
        return Ok(ApiResponse<DashboardStatsDto>.Ok(result));
    }

    [HttpGet("recent-campaigns")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<CampaignDto>>), 200)]
    public async Task<IActionResult> GetRecentCampaigns([FromQuery] int count = 5, CancellationToken ct = default)
    {
        var result = await _dashboardService.GetRecentCampaignsAsync(CurrentUserId, count, ct);
        return Ok(ApiResponse<IEnumerable<CampaignDto>>.Ok(result));
    }

    [HttpGet("channel-breakdown")]
    [ProducesResponseType(typeof(ApiResponse<ChannelBreakdownDto>), 200)]
    public async Task<IActionResult> GetChannelBreakdown(CancellationToken ct)
    {
        var result = await _dashboardService.GetChannelBreakdownAsync(CurrentUserId, ct);
        return Ok(ApiResponse<ChannelBreakdownDto>.Ok(result));
    }
}
