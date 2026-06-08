using System.Security.Claims;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketingApp.API.Controllers;

/// <summary>
/// Admin-only management of inbox aliases (receiving address -> owning user) used by the poller to
/// route mail in a shared-mailbox setup. Purely additive — when no alias exists, routing falls back
/// to the existing chain/contact/catch-all logic, so this controller is safe to leave unused.
/// </summary>
[ApiController]
[Route("api/v1/admin/inbox-aliases")]
[Authorize]
public class AdminInboxAliasesController : ControllerBase
{
    private readonly IGenericRepository<InboxAlias> _aliasRepo;
    private readonly IGenericRepository<User> _userRepo;

    public AdminInboxAliasesController(IGenericRepository<InboxAlias> aliasRepo, IGenericRepository<User> userRepo)
    {
        _aliasRepo = aliasRepo;
        _userRepo = userRepo;
    }

    private bool IsAdmin() => string.Equals(User.FindFirstValue(ClaimTypes.Role), "admin", StringComparison.OrdinalIgnoreCase);
    private IActionResult? GuardAdmin() =>
        IsAdmin() ? null : StatusCode(403, ApiResponse<object>.Fail("Admin role required."));

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (GuardAdmin() is { } forbidden) return forbidden;
        var aliases = (await _aliasRepo.GetAllAsync(ct))
            .OrderBy(a => a.Address)
            .Select(a => new InboxAliasDto
            {
                Id = a.Id,
                Address = a.Address,
                UserId = a.UserId,
                SmtpGroupId = a.SmtpGroupId,
                IsActive = a.IsActive,
                CreatedAt = a.CreatedAt,
            });
        return Ok(ApiResponse<IEnumerable<InboxAliasDto>>.Ok(aliases));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInboxAliasDto dto, CancellationToken ct)
    {
        if (GuardAdmin() is { } forbidden) return forbidden;

        var address = dto.Address?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(address) || !address.Contains('@'))
            return BadRequest(ApiResponse<object>.Fail("A valid email address is required."));

        var user = await _userRepo.GetByIdAsync(dto.UserId, ct);
        if (user is null) return BadRequest(ApiResponse<object>.Fail("User not found."));

        if (await _aliasRepo.AnyAsync(a => a.Address.ToLower() == address, ct))
            return Conflict(ApiResponse<object>.Fail($"Alias '{address}' already exists."));

        var alias = await _aliasRepo.AddAsync(new InboxAlias
        {
            Address = address,
            UserId = dto.UserId,
            SmtpGroupId = dto.SmtpGroupId,
            IsActive = true,
        }, ct);

        return StatusCode(201, ApiResponse<InboxAliasDto>.Ok(new InboxAliasDto
        {
            Id = alias.Id,
            Address = alias.Address,
            UserId = alias.UserId,
            SmtpGroupId = alias.SmtpGroupId,
            IsActive = alias.IsActive,
            CreatedAt = alias.CreatedAt,
        }, "Alias created."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (GuardAdmin() is { } forbidden) return forbidden;
        var alias = await _aliasRepo.GetByIdAsync(id, ct);
        if (alias is null) return NotFound(ApiResponse<object>.Fail("Alias not found."));
        await _aliasRepo.DeleteAsync(alias, ct);
        return Ok(ApiResponse<object>.Ok(null!, "Alias deleted."));
    }
}
