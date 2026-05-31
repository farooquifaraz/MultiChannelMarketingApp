using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.API.Helpers;
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
    private bool IsAdmin => string.Equals(User.FindFirstValue(ClaimTypes.Role), "admin", StringComparison.OrdinalIgnoreCase);

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
        [FromQuery] bool viewAll = false,
        CancellationToken ct = default)
    {
        if (pageSize > 100) pageSize = 100;
        // Admins can flip viewAll=true to see ALL users' campaigns. Non-admins always scoped to themselves.
        Guid? scopeUserId = (IsAdmin && viewAll) ? null : CurrentUserId;
        var result = await _campaignService.GetAllAsync(scopeUserId, pageNumber, pageSize, status, channel, ct);
        return Ok(result);
    }

    [HttpGet("export.csv")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] string? status = null,
        [FromQuery] string? channel = null,
        [FromQuery] bool viewAll = false,
        CancellationToken ct = default)
    {
        Guid? scopeUserId = (IsAdmin && viewAll) ? null : CurrentUserId;
        // Pull everything (up to 10k rows — safety cap for a single export).
        var result = await _campaignService.GetAllAsync(scopeUserId, 1, 10000, status, channel, ct);
        var bytes = CsvExporter.BuildCsv<CampaignDto>(result.Data, new[]
        {
            ("Name", (Func<CampaignDto, object?>)(c => c.Name)),
            ("Channel", (Func<CampaignDto, object?>)(c => c.Channel)),
            ("Status", (Func<CampaignDto, object?>)(c => c.Status)),
            ("Template", (Func<CampaignDto, object?>)(c => c.TemplateName)),
            ("Group", (Func<CampaignDto, object?>)(c => c.GroupName)),
            ("Owner", (Func<CampaignDto, object?>)(c => c.OwnerName)),
            ("OwnerEmail", (Func<CampaignDto, object?>)(c => c.OwnerEmail)),
            ("SmtpGroup", (Func<CampaignDto, object?>)(c => c.SmtpGroupName)),
            ("TotalContacts", (Func<CampaignDto, object?>)(c => c.TotalContacts)),
            ("Sent", (Func<CampaignDto, object?>)(c => c.SentCount)),
            ("Failed", (Func<CampaignDto, object?>)(c => c.FailedCount)),
            ("ScheduledAt", (Func<CampaignDto, object?>)(c => c.ScheduledAt)),
            ("CompletedAt", (Func<CampaignDto, object?>)(c => c.CompletedAt)),
            ("CreatedAt", (Func<CampaignDto, object?>)(c => c.CreatedAt)),
        });
        var filename = $"campaigns_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv";
        return File(bytes, "text/csv", filename);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CampaignDetailDto>), 200)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _campaignService.GetByIdAsync(id, CurrentUserId, IsAdmin, ct);
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

    [HttpPost("{id:guid}/retry-failed")]
    [ProducesResponseType(typeof(ApiResponse<object>), 202)]
    public async Task<IActionResult> RetryFailed(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("User {UserId} retrying failed messages for campaign {CampaignId}", CurrentUserId, id);
        var count = await _campaignService.RetryFailedAsync(id, CurrentUserId, IsAdmin, ct);
        return Accepted(ApiResponse<object>.Ok(new { retriedCount = count }, $"Retrying {count} failed message(s)."));
    }

    [HttpPost("{id:guid}/cancel-schedule")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> CancelSchedule(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("User {UserId} cancelling scheduled campaign {CampaignId}", CurrentUserId, id);
        await _campaignService.CancelScheduledAsync(id, CurrentUserId, IsAdmin, ct);
        return Ok(ApiResponse<object>.Ok(null!, "Scheduled send cancelled."));
    }

    [HttpGet("{id:guid}/report")]
    [ProducesResponseType(typeof(ApiResponse<CampaignReportDto>), 200)]
    public async Task<IActionResult> GetReport(Guid id, CancellationToken ct)
    {
        var result = await _campaignService.GetReportAsync(id, CurrentUserId, IsAdmin, ct);
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
        var result = await _campaignService.GetMessagesAsync(id, CurrentUserId, IsAdmin, pageNumber, pageSize, status, ct);
        return Ok(result);
    }
}
