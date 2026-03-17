using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using System.Security.Claims;

namespace MarketingApp.API.Controllers;

[ApiController]
[Route("api/v1/templates")]
[Authorize]
public class TemplatesController : ControllerBase
{
    private readonly ITemplateService _templateService;
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public TemplatesController(ITemplateService templateService)
    {
        _templateService = templateService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<TemplateDto>>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] string? channel, CancellationToken ct)
    {
        var result = await _templateService.GetAllAsync(CurrentUserId, channel, ct);
        return Ok(ApiResponse<IEnumerable<TemplateDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TemplateDto>), 200)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _templateService.GetByIdAsync(id, CurrentUserId, ct);
        return Ok(ApiResponse<TemplateDto>.Ok(result));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<TemplateDto>), 201)]
    public async Task<IActionResult> Create([FromBody] CreateTemplateDto dto, CancellationToken ct)
    {
        var result = await _templateService.CreateAsync(CurrentUserId, dto, ct);
        return StatusCode(201, ApiResponse<TemplateDto>.Ok(result, "Template created"));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TemplateDto>), 200)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTemplateDto dto, CancellationToken ct)
    {
        var result = await _templateService.UpdateAsync(id, CurrentUserId, dto, ct);
        return Ok(ApiResponse<TemplateDto>.Ok(result));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _templateService.DeleteAsync(id, CurrentUserId, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/preview")]
    [ProducesResponseType(typeof(ApiResponse<string>), 200)]
    public async Task<IActionResult> Preview(Guid id, [FromBody] Dictionary<string, string> sampleData, CancellationToken ct)
    {
        var result = await _templateService.PreviewAsync(id, CurrentUserId, sampleData, ct);
        return Ok(ApiResponse<string>.Ok(result));
    }
}
