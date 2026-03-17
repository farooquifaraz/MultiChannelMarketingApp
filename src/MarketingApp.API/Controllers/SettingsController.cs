using System.Security.Claims;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketingApp.API.Controllers;

[ApiController]
[Route("api/v1/settings")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly ISmtpSettingsService _smtpSettingsService;

    public SettingsController(ISmtpSettingsService smtpSettingsService)
    {
        _smtpSettingsService = smtpSettingsService;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("smtp")]
    public async Task<ActionResult<ApiResponse<SmtpSettingsDto>>> GetSmtpSettings()
    {
        var settings = await _smtpSettingsService.GetSettingsAsync(GetUserId());
        return Ok(ApiResponse<SmtpSettingsDto?>.Ok(settings, settings is null ? "No SMTP settings configured" : "Settings retrieved"));
    }

    [HttpPost("smtp")]
    public async Task<ActionResult<ApiResponse<SmtpSettingsDto>>> SaveSmtpSettings([FromBody] CreateSmtpSettingsDto dto)
    {
        var result = await _smtpSettingsService.CreateOrUpdateSettingsAsync(GetUserId(), dto);
        return Ok(ApiResponse<SmtpSettingsDto>.Ok(result, "SMTP settings saved successfully"));
    }

    [HttpPost("smtp/test")]
    public async Task<ActionResult<ApiResponse<bool>>> TestSmtpConnection([FromBody] TestSmtpDto dto)
    {
        try
        {
            var success = await _smtpSettingsService.TestSmtpConnectionAsync(GetUserId(), dto.TestEmail);
            return Ok(ApiResponse<bool>.Ok(success, "Test email sent successfully! Check your inbox."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<bool>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<bool>.Fail($"Email test failed: {ex.Message}"));
        }
    }
}
