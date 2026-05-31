using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.API.Helpers;
using System.Security.Claims;

namespace MarketingApp.API.Controllers;

[ApiController]
[Route("api/v1/contacts")]
[Authorize]
public class ContactsController : ControllerBase
{
    private readonly IContactService _contactService;
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public ContactsController(IContactService contactService)
    {
        _contactService = contactService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ContactDto>), 200)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? groupId = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        // BUG-003 defensive clamp — prevents Skip(-N) / Take(huge) crashes.
        Helpers.PagingHelper.Clamp(ref pageNumber, ref pageSize);
        var result = await _contactService.GetAllAsync(CurrentUserId, pageNumber, pageSize, groupId, search, ct);
        return Ok(result);
    }

    [HttpGet("export.csv")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] Guid? groupId = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        // Cap export to 10k rows for safety.
        var result = await _contactService.GetAllAsync(CurrentUserId, 1, 10000, groupId, search, ct);
        var bytes = CsvExporter.BuildCsv<ContactDto>(result.Data, new[]
        {
            ("Name", (Func<ContactDto, object?>)(c => c.FullName)),
            ("Email", (Func<ContactDto, object?>)(c => c.Email)),
            ("Phone", (Func<ContactDto, object?>)(c => c.Phone)),
            ("WhatsApp", (Func<ContactDto, object?>)(c => c.WhatsAppNumber)),
            ("Group", (Func<ContactDto, object?>)(c => c.GroupName)),
            ("Active", (Func<ContactDto, object?>)(c => c.IsActive)),
            ("CreatedAt", (Func<ContactDto, object?>)(c => c.CreatedAt)),
        });
        var filename = $"contacts_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv";
        return File(bytes, "text/csv", filename);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ContactDto>), 200)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _contactService.GetByIdAsync(id, CurrentUserId, ct);
        return Ok(ApiResponse<ContactDto>.Ok(result));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ContactDto>), 201)]
    public async Task<IActionResult> Create([FromBody] CreateContactDto dto, CancellationToken ct)
    {
        var result = await _contactService.CreateAsync(CurrentUserId, dto, ct);
        return StatusCode(201, ApiResponse<ContactDto>.Ok(result, "Contact created"));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ContactDto>), 200)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateContactDto dto, CancellationToken ct)
    {
        var result = await _contactService.UpdateAsync(id, CurrentUserId, dto, ct);
        return Ok(ApiResponse<ContactDto>.Ok(result));
    }

    [HttpPost("{id:guid}/clear-bounce")]
    [ProducesResponseType(typeof(ApiResponse<ContactDto>), 200)]
    public async Task<IActionResult> ClearBounce(Guid id, CancellationToken ct)
    {
        var result = await _contactService.ClearBounceAsync(id, CurrentUserId, ct);
        return Ok(ApiResponse<ContactDto>.Ok(result, "Bounce flag cleared. Contact will receive future campaigns."));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _contactService.DeleteAsync(id, CurrentUserId, ct);
        return NoContent();
    }

    [HttpPost("import")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<ImportResultDto>), 200)]
    public async Task<IActionResult> Import(IFormFile file, [FromQuery] Guid? groupId, CancellationToken ct)
    {
        if (file.Length == 0) return BadRequest(ApiResponse.Fail("File is empty"));
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".csv" && ext != ".xlsx")
            return BadRequest(ApiResponse.Fail("Only CSV and XLSX files are allowed"));

        using var stream = file.OpenReadStream();
        var result = await _contactService.ImportContactsAsync(CurrentUserId, stream, file.FileName, groupId, ct);
        return Ok(ApiResponse<ImportResultDto>.Ok(result));
    }

    [HttpGet("groups")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ContactGroupDto>>), 200)]
    public async Task<IActionResult> GetGroups(CancellationToken ct)
    {
        var result = await _contactService.GetGroupsAsync(CurrentUserId, ct);
        return Ok(ApiResponse<IEnumerable<ContactGroupDto>>.Ok(result));
    }

    [HttpPost("groups")]
    [ProducesResponseType(typeof(ApiResponse<ContactGroupDto>), 201)]
    public async Task<IActionResult> CreateGroup([FromBody] CreateContactGroupDto dto, CancellationToken ct)
    {
        var result = await _contactService.CreateGroupAsync(CurrentUserId, dto, ct);
        return StatusCode(201, ApiResponse<ContactGroupDto>.Ok(result, "Group created"));
    }

    [HttpPut("groups/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ContactGroupDto>), 200)]
    public async Task<IActionResult> UpdateGroup(Guid id, [FromBody] CreateContactGroupDto dto, CancellationToken ct)
    {
        var result = await _contactService.UpdateGroupAsync(id, CurrentUserId, dto, ct);
        return Ok(ApiResponse<ContactGroupDto>.Ok(result, "Group updated"));
    }

    [HttpDelete("groups/{id:guid}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeleteGroup(Guid id, CancellationToken ct)
    {
        await _contactService.DeleteGroupAsync(id, CurrentUserId, ct);
        return NoContent();
    }

    [HttpPost("assign-group")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> AssignToGroup([FromBody] AssignToGroupRequest request, CancellationToken ct)
    {
        var count = await _contactService.AssignToGroupAsync(CurrentUserId, request.ContactIds, request.GroupId, ct);
        return Ok(ApiResponse<object>.Ok(new { assignedCount = count }, $"{count} contacts assigned to group"));
    }
}

public class AssignToGroupRequest
{
    public List<Guid> ContactIds { get; set; } = new();
    public Guid? GroupId { get; set; }
}
