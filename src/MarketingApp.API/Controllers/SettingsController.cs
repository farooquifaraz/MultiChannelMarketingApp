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
    private bool IsAdmin() => string.Equals(User.FindFirstValue(ClaimTypes.Role), "admin", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Get the user's SMTP settings. For non-admins, this returns a safe view (no credentials).
    /// SMTP infrastructure is centrally managed by the admin — regular users only see read-only signature info.
    /// </summary>
    [HttpGet("smtp")]
    public async Task<ActionResult<ApiResponse<SmtpSettingsDto>>> GetSmtpSettings()
    {
        var settings = await _smtpSettingsService.GetSettingsAsync(GetUserId());
        return Ok(ApiResponse<SmtpSettingsDto?>.Ok(settings, settings is null ? "No SMTP settings configured" : "Settings retrieved"));
    }

    /// <summary>Save SMTP settings — admin only. Regular users get 403.</summary>
    [HttpPost("smtp")]
    public async Task<ActionResult<ApiResponse<SmtpSettingsDto>>> SaveSmtpSettings([FromBody] CreateSmtpSettingsDto dto)
    {
        if (!IsAdmin())
            return StatusCode(403, ApiResponse<object>.Fail("Email settings are managed by your admin. Please contact them to configure SMTP."));
        var result = await _smtpSettingsService.CreateOrUpdateSettingsAsync(GetUserId(), dto);
        return Ok(ApiResponse<SmtpSettingsDto>.Ok(result, "SMTP settings saved successfully"));
    }

    /// <summary>Test SMTP connection — admin only.</summary>
    [HttpPost("smtp/test")]
    public async Task<ActionResult<ApiResponse<bool>>> TestSmtpConnection([FromBody] TestSmtpDto dto)
    {
        if (!IsAdmin())
            return StatusCode(403, ApiResponse<bool>.Fail("Only admins can test the SMTP connection."));
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

    /// <summary>
    /// Upload signature image. Saves to wwwroot/uploads/signatures/ and returns public URL.
    /// Accepts: PNG, JPG, JPEG, GIF, WEBP. Max size: 2 MB.
    /// </summary>
    [HttpPost("signature-image/upload")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<object>>> UploadSignatureImage(
        IFormFile file,
        [FromServices] IWebHostEnvironment env)
    {
        if (!IsAdmin())
            return StatusCode(403, ApiResponse<object>.Fail("Only admins can manage the signature image."));
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("No file uploaded."));

        const long maxBytes = 2 * 1024 * 1024; // 2 MB
        if (file.Length > maxBytes)
            return BadRequest(ApiResponse<object>.Fail("File too large. Maximum size is 2 MB."));

        var allowedExtensions = new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
            return BadRequest(ApiResponse<object>.Fail($"Unsupported file type '{ext}'. Allowed: PNG, JPG, JPEG, GIF, WEBP."));

        // Save under wwwroot/uploads/signatures/{userId}-{timestamp}{ext}
        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var uploadsDir = Path.Combine(webRoot, "uploads", "signatures");
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"{GetUserId()}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // Build public URL (absolute so it works in emails sent to external recipients)
        var publicUrl = $"{Request.Scheme}://{Request.Host}/uploads/signatures/{fileName}";

        return Ok(ApiResponse<object>.Ok(new { url = publicUrl, sizeBytes = file.Length }, "Image uploaded successfully"));
    }
}
