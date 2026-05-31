using System.Security.Claims;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketingApp.API.Controllers;

[ApiController]
[Route("api/v1/admin/smtp-groups")]
[Authorize]
public class AdminSmtpGroupsController : ControllerBase
{
    private readonly ISmtpGroupService _service;

    public AdminSmtpGroupsController(ISmtpGroupService service) { _service = service; }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsAdmin() => string.Equals(User.FindFirstValue(ClaimTypes.Role), "admin", StringComparison.OrdinalIgnoreCase);
    private IActionResult? GuardAdmin() =>
        IsAdmin() ? null : StatusCode(403, ApiResponse<object>.Fail("Admin role required."));

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (GuardAdmin() is { } forbidden) return forbidden;
        var groups = await _service.GetAllAsync(ct);
        return Ok(ApiResponse<IEnumerable<SmtpGroupDto>>.Ok(groups));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        if (GuardAdmin() is { } forbidden) return forbidden;
        var group = await _service.GetByIdAsync(id, ct);
        if (group is null) return NotFound(ApiResponse<object>.Fail("Group not found."));
        return Ok(ApiResponse<SmtpGroupDto>.Ok(group));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSmtpGroupDto dto, CancellationToken ct)
    {
        if (GuardAdmin() is { } forbidden) return forbidden;
        var created = await _service.CreateAsync(GetUserId(), dto, ct);
        return StatusCode(201, ApiResponse<SmtpGroupDto>.Ok(created, "Group created."));
    }

    [HttpPost("{id:guid}/clone")]
    public async Task<IActionResult> Clone(Guid id, CancellationToken ct)
    {
        if (GuardAdmin() is { } forbidden) return forbidden;
        var clone = await _service.CloneAsync(id, GetUserId(), ct);
        return StatusCode(201, ApiResponse<SmtpGroupDto>.Ok(clone, $"Cloned to \"{clone.Name}\". Edit it to set the new From email & credentials."));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSmtpGroupDto dto, CancellationToken ct)
    {
        if (GuardAdmin() is { } forbidden) return forbidden;
        var updated = await _service.UpdateAsync(id, dto, ct);
        return Ok(ApiResponse<SmtpGroupDto>.Ok(updated, "Group updated."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (GuardAdmin() is { } forbidden) return forbidden;
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/set-default")]
    public async Task<IActionResult> SetDefault(Guid id, CancellationToken ct)
    {
        if (GuardAdmin() is { } forbidden) return forbidden;
        var updated = await _service.SetDefaultAsync(id, ct);
        return Ok(ApiResponse<SmtpGroupDto>.Ok(updated, $"\"{updated.Name}\" is now the default group."));
    }

    [HttpPost("assign-users")]
    public async Task<IActionResult> AssignUsers([FromBody] AssignUsersToGroupDto dto, CancellationToken ct)
    {
        if (GuardAdmin() is { } forbidden) return forbidden;
        var count = await _service.AssignUsersAsync(dto.GroupId, dto.UserIds, ct);
        return Ok(ApiResponse<object>.Ok(new { assignedCount = count }, $"{count} user(s) assigned."));
    }

    [HttpPost("unassign-users")]
    public async Task<IActionResult> UnassignUsers([FromBody] List<Guid> userIds, CancellationToken ct)
    {
        if (GuardAdmin() is { } forbidden) return forbidden;
        var count = await _service.UnassignUsersAsync(userIds, ct);
        return Ok(ApiResponse<object>.Ok(new { unassignedCount = count }, $"{count} user(s) unassigned."));
    }

    [HttpPost("{id:guid}/test")]
    public async Task<IActionResult> Test(Guid id, [FromBody] TestSmtpGroupDto dto, CancellationToken ct)
    {
        if (GuardAdmin() is { } forbidden) return forbidden;
        try
        {
            var ok = await _service.TestConnectionAsync(id, dto.TestEmail, ct);
            return Ok(ApiResponse<bool>.Ok(ok, "Test email sent."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<bool>.Fail(ex.Message));
        }
    }

    [HttpGet("user-assignments")]
    public async Task<IActionResult> UserAssignments(CancellationToken ct)
    {
        if (GuardAdmin() is { } forbidden) return forbidden;
        var users = await _service.GetUserAssignmentsAsync(ct);
        return Ok(ApiResponse<IEnumerable<UserAssignmentDto>>.Ok(users));
    }
}
