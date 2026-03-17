using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using System.Security.Claims;

namespace MarketingApp.API.Controllers;

[ApiController]
[Route("api/v1/campaigns")]
[Authorize]
public class CampaignsController : ControllerBase
{
    private readonly ICampaignService _campaignService;
    private readonly ILogger<CampaignsController> _logger;
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public CampaignsController(ICampaignService campaignService, ILogger<CampaignsController> logger)
    {
        _campaignService = campaignService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<CampaignDto>), 200)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? channel = null,
        CancellationToken ct = default)
    {
        if (pageSize > 100) pageSize = 100;
        var result = await _campaignService.GetAllAsync(CurrentUserId, pageNumber, pageSize, status, channel, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CampaignDetailDto>), 200)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _campaignService.GetByIdAsync(id, CurrentUserId, ct);
        return Ok(ApiResponse<CampaignDetailDto>.Ok(result));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CampaignDto>), 201)]
    public async Task<IActionResult> Create([FromBody] CreateCampaignDto dto, CancellationToken ct)
    {
        _logger.LogInformation("User {UserId} creating campaign: {Name}", CurrentUserId, dto.Name);
        var result = await _campaignService.CreateAsync(CurrentUserId, dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<CampaignDto>.Ok(result, "Campaign created"));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CampaignDto>), 200)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCampaignDto dto, CancellationToken ct)
    {
        var result = await _campaignService.UpdateAsync(id, CurrentUserId, dto, ct);
        return Ok(ApiResponse<CampaignDto>.Ok(result));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _campaignService.DeleteAsync(id, CurrentUserId, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/send")]
    [EnableRateLimiting("send")]
    [ProducesResponseType(typeof(ApiResponse), 202)]
    public async Task<IActionResult> Send(Guid id, [FromBody] SendCampaignDto? dto, CancellationToken ct)
    {
        _logger.LogInformation("User {UserId} sending campaign {CampaignId}", CurrentUserId, id);
        await _campaignService.SendAsync(id, CurrentUserId, dto?.ScheduledAt, ct);
        return Accepted(ApiResponse<object>.Ok(null!, "Campaign queued successfully"));
    }

    [HttpGet("{id:guid}/report")]
    [ProducesResponseType(typeof(ApiResponse<CampaignReportDto>), 200)]
    public async Task<IActionResult> GetReport(Guid id, CancellationToken ct)
    {
        var result = await _campaignService.GetReportAsync(id, CurrentUserId, ct);
        return Ok(ApiResponse<CampaignReportDto>.Ok(result));
    }

    [HttpGet("{id:guid}/messages")]
    [ProducesResponseType(typeof(PagedResponse<CampaignMessageDto>), 200)]
    public async Task<IActionResult> GetMessages(Guid id,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        if (pageSize > 100) pageSize = 100;
        var result = await _campaignService.GetMessagesAsync(id, CurrentUserId, pageNumber, pageSize, status, ct);
        return Ok(result);
    }
}
