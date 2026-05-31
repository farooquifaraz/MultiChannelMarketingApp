using System.Security.Claims;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.API.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketingApp.API.Controllers;

[ApiController]
[Route("api/v1/admin/audit-logs")]
[Authorize]
public class AdminAuditLogsController : ControllerBase
{
    private readonly IAuditService _audit;

    public AdminAuditLogsController(IAuditService audit) { _audit = audit; }

    private bool IsAdmin() => string.Equals(User.FindFirstValue(ClaimTypes.Role), "admin", StringComparison.OrdinalIgnoreCase);
    private IActionResult? GuardAdmin() =>
        IsAdmin() ? null : StatusCode(403, ApiResponse<object>.Fail("Admin role required."));

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? action = null,
        [FromQuery] string? entity = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        if (GuardAdmin() is { } forbidden) return forbidden;

        var result = await _audit.QueryAsync(new AuditLogQuery
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            UserId = userId,
            Action = action,
            Entity = entity,
            FromDate = fromDate,
            ToDate = toDate,
            Search = search,
        }, ct);

        return Ok(result);
    }

    [HttpGet("filters")]
    public async Task<IActionResult> Filters(CancellationToken ct)
    {
        if (GuardAdmin() is { } forbidden) return forbidden;
        var actions = await _audit.GetDistinctActionsAsync(ct);
        var entities = await _audit.GetDistinctEntitiesAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { actions, entities }));
    }

    [HttpGet("export.csv")]
    public async Task<IActionResult> Export(
        [FromQuery] Guid? userId = null,
        [FromQuery] string? action = null,
        [FromQuery] string? entity = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        if (GuardAdmin() is { } forbidden) return forbidden;

        // Cap export to 10k rows.
        var result = await _audit.QueryAsync(new AuditLogQuery
        {
            PageNumber = 1,
            PageSize = 10000,
            UserId = userId,
            Action = action,
            Entity = entity,
            FromDate = fromDate,
            ToDate = toDate,
            Search = search,
        }, ct);

        var bytes = CsvExporter.BuildCsv<AuditLogDto>(result.Data, new[]
        {
            ("CreatedAt", (Func<AuditLogDto, object?>)(a => a.CreatedAt)),
            ("User", (Func<AuditLogDto, object?>)(a => a.UserFullName)),
            ("Email", (Func<AuditLogDto, object?>)(a => a.UserEmail)),
            ("Action", (Func<AuditLogDto, object?>)(a => a.Action)),
            ("Entity", (Func<AuditLogDto, object?>)(a => a.Entity)),
            ("EntityId", (Func<AuditLogDto, object?>)(a => a.EntityId)),
            ("IpAddress", (Func<AuditLogDto, object?>)(a => a.IpAddress)),
            ("Details", (Func<AuditLogDto, object?>)(a => a.Details)),
        });
        var filename = $"audit_logs_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv";
        return File(bytes, "text/csv", filename);
    }
}
