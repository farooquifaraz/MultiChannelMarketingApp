using System.Security.Claims;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Domain.Entities;
using MarketingApp.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace MarketingApp.API.Controllers;

/// <summary>
/// Self-service endpoints for the currently logged-in user.
/// Personal signature lives here — it merges with the user's assigned SmtpGroup at render time.
/// </summary>
[ApiController]
[Route("api/v1/me")]
[Authorize]
public class MeController : ControllerBase
{
    private readonly IGenericRepository<User> _userRepo;
    private readonly ISmtpGroupService _smtpGroups;
    private readonly ISystemSettingsService _systemSettings;
    private readonly ILogger<MeController> _logger;

    public MeController(
        IGenericRepository<User> userRepo,
        ISmtpGroupService smtpGroups,
        ISystemSettingsService systemSettings,
        ILogger<MeController> logger)
    {
        _userRepo = userRepo;
        _smtpGroups = smtpGroups;
        _systemSettings = systemSettings;
        _logger = logger;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public record MyProfileDto(Guid Id, string FullName, string Email, string Role, string? SmtpGroupName, DateTime CreatedAt);
    public record UpdateMyProfileDto(string FullName);
    public record ChangeMyPasswordDto(string CurrentPassword, string NewPassword);

    /// <summary>
    /// M2 — what every Send Message / Send Campaign page asks before letting the user
    /// click Send: "which provider will actually deliver this email?". Resolves the
    /// user's assigned SmtpGroup (or platform default), and exposes enough info to
    /// render a clear banner: provider, from-address, daily/hourly rate caps.
    /// Returns null group when no default is configured — the UI then surfaces a
    /// warning instead of silently falling back to the mock service.
    /// </summary>
    public record ActiveSenderDto(
        Guid? GroupId,
        string? GroupName,
        string? Provider,
        string? FromEmail,
        string? FromName,
        bool IsDefault,
        bool IsAssignedToUser,
        bool HasApiKey,
        bool HasSmtpPassword,
        int? DelayBetweenMessagesMs,
        int? MaxMessagesPerMinute);

    [HttpGet("active-sender")]
    public async Task<IActionResult> GetActiveSender(CancellationToken ct)
    {
        var userId = GetUserId();
        var user = await _userRepo.GetByIdAsync(userId, ct) ?? throw new NotFoundException("User", userId);
        var group = await _smtpGroups.ResolveForUserAsync(userId, ct);

        if (group is null)
        {
            return Ok(ApiResponse<ActiveSenderDto>.Ok(new ActiveSenderDto(
                null, null, null, null, null, false, false, false, false, null, null)));
        }

        // Whether this group is the user's assigned group (vs the platform-wide default fallback).
        var isAssigned = user.SmtpGroupId == group.Id;

        // Provider-specific credential presence — surfaced so the UI can warn
        // "Brevo selected but API key missing" before the user clicks Send.
        var provider = (group.EmailProvider ?? "smtp").ToLowerInvariant();
        var hasApiKey = provider switch
        {
            "brevo"    => !string.IsNullOrWhiteSpace(group.BrevoApiKey),
            "sendgrid" => !string.IsNullOrWhiteSpace(group.SendGridApiKey),
            "mailgun"  => !string.IsNullOrWhiteSpace(group.MailgunApiKey),
            _          => false, // SMTP doesn't use an API key
        };
        var hasSmtpPassword = provider == "smtp" && !string.IsNullOrWhiteSpace(group.SmtpPassword);

        return Ok(ApiResponse<ActiveSenderDto>.Ok(new ActiveSenderDto(
            GroupId: group.Id,
            GroupName: group.Name,
            Provider: provider,
            FromEmail: group.FromEmail,
            FromName: group.FromName,
            IsDefault: group.IsDefault,
            IsAssignedToUser: isAssigned,
            HasApiKey: hasApiKey,
            HasSmtpPassword: hasSmtpPassword,
            DelayBetweenMessagesMs: group.DelayBetweenMessagesMs,
            MaxMessagesPerMinute: group.MaxMessagesPerMinute)));
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var user = await _userRepo.GetByIdAsync(GetUserId(), ct) ?? throw new NotFoundException("User", GetUserId());
        var group = await _smtpGroups.ResolveForUserAsync(user.Id, ct);
        return Ok(ApiResponse<MyProfileDto>.Ok(new MyProfileDto(
            user.Id, user.FullName, user.Email, user.Role, group?.Name, user.CreatedAt)));
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateMyProfileDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName))
            throw new AppValidationException(new List<string> { "Full name is required." });
        var user = await _userRepo.GetByIdAsync(GetUserId(), ct) ?? throw new NotFoundException("User", GetUserId());
        user.FullName = dto.FullName.Trim();
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepo.UpdateAsync(user, ct);
        return await GetProfile(ct);
    }

    [HttpPost("password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangeMyPasswordDto dto, CancellationToken ct)
    {
        var user = await _userRepo.GetByIdAsync(GetUserId(), ct) ?? throw new NotFoundException("User", GetUserId());
        if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword ?? "", user.PasswordHash))
            return BadRequest(ApiResponse<object>.Fail("Current password is incorrect."));
        var sys = await _systemSettings.GetAsync(ct);
        var minLen = Math.Max(4, sys.PasswordMinLength);
        if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < minLen)
            return BadRequest(ApiResponse<object>.Fail($"New password must be at least {minLen} characters."));
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepo.UpdateAsync(user, ct);
        _logger.LogInformation("User {UserId} changed their password", user.Id);
        return Ok(ApiResponse<object>.Ok(null!, "Password changed."));
    }

    [HttpGet("signature")]
    public async Task<IActionResult> GetSignature(CancellationToken ct)
    {
        var userId = GetUserId();
        var user = await _userRepo.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User", userId);
        var group = await _smtpGroups.ResolveForUserAsync(userId, ct);

        var dto = new MySignatureDto
        {
            FullName = user.FullName,
            SignatureDesignation = user.SignatureDesignation,
            SignaturePhone = user.SignaturePhone,
            SignatureImageUrl = user.SignatureImageUrl,
            // Effective = personal override, falling back to org's default
            EffectiveDesignation = user.SignatureDesignation ?? group?.SignatureDesignation,
            EffectivePhone       = user.SignaturePhone       ?? group?.SignaturePhone,
            EffectiveImageUrl    = user.SignatureImageUrl    ?? group?.SignatureImageUrl,
            // Org-level (admin-managed)
            OrgFromEmail       = group?.FromEmail,
            OrgCompanyName     = group?.FromName,
            OrgCompanyWebsite  = group?.CompanyWebsite,
            OrgSmtpGroupName   = group?.Name,
        };
        return Ok(ApiResponse<MySignatureDto>.Ok(dto));
    }

    [HttpPut("signature")]
    public async Task<IActionResult> UpdateSignature([FromBody] UpdateMySignatureDto dto, CancellationToken ct)
    {
        var userId = GetUserId();
        var user = await _userRepo.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User", userId);

        user.SignatureDesignation = dto.SignatureDesignation;
        user.SignaturePhone = dto.SignaturePhone;
        user.SignatureImageUrl = dto.SignatureImageUrl;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepo.UpdateAsync(user, ct);
        _logger.LogInformation("User {UserId} updated personal signature", userId);

        // Return the freshly resolved view
        return await GetSignature(ct);
    }

    /// <summary>Upload a signature image — available to any logged-in user (not just admin).</summary>
    [HttpPost("signature/image/upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadImage(IFormFile file, [FromServices] IWebHostEnvironment env)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("No file uploaded."));
        if (file.Length > 2 * 1024 * 1024)
            return BadRequest(ApiResponse<object>.Fail("Max size is 2 MB."));
        var allowed = new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowed.Contains(ext))
            return BadRequest(ApiResponse<object>.Fail($"Unsupported file type '{ext}'."));

        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var dir = Path.Combine(webRoot, "uploads", "signatures");
        Directory.CreateDirectory(dir);
        var fileName = $"{GetUserId()}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{ext}";
        var path = Path.Combine(dir, fileName);
        await using (var stream = new FileStream(path, FileMode.Create))
            await file.CopyToAsync(stream);
        var url = $"{Request.Scheme}://{Request.Host}/uploads/signatures/{fileName}";
        return Ok(ApiResponse<object>.Ok(new { url, sizeBytes = file.Length }, "Image uploaded."));
    }

    /// <summary>
    /// L1 — upload a WhatsApp media file (image / document / video). Returns a public URL plus the
    /// resolved mediaType so the compose UI can attach it to a WhatsApp template. Meta fetches the
    /// file from this URL at send time, so it must be publicly reachable (it is, via api.samdigital.ae).
    /// </summary>
    [HttpPost("whatsapp-media/upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadWhatsAppMedia(IFormFile file, [FromServices] IWebHostEnvironment env)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("No file uploaded."));
        // Meta caps: image 5MB, video 16MB, document 100MB. Cap at 16MB to cover image+video safely.
        if (file.Length > 16 * 1024 * 1024)
            return BadRequest(ApiResponse<object>.Fail("Max size is 16 MB."));

        // Map extension → Meta media type. Reject anything we don't recognize.
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        string? mediaType = ext switch
        {
            ".png" or ".jpg" or ".jpeg" or ".webp" => "image",
            ".mp4" or ".3gp" => "video",
            ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" or ".txt" => "document",
            _ => null,
        };
        if (mediaType is null)
            return BadRequest(ApiResponse<object>.Fail($"Unsupported file type '{ext}'. Allowed: images, mp4, pdf/office docs."));

        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var dir = Path.Combine(webRoot, "uploads", "whatsapp-media");
        Directory.CreateDirectory(dir);
        var storedName = $"{GetUserId()}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{ext}";
        var path = Path.Combine(dir, storedName);
        await using (var stream = new FileStream(path, FileMode.Create))
            await file.CopyToAsync(stream);

        var url = $"{Request.Scheme}://{Request.Host}/uploads/whatsapp-media/{storedName}";
        // Preserve the original filename for documents (shown to the WhatsApp recipient).
        var originalName = Path.GetFileName(file.FileName);
        _logger.LogInformation("User {UserId} uploaded WhatsApp {Type} media ({Size} bytes)", GetUserId(), mediaType, file.Length);
        return Ok(ApiResponse<object>.Ok(new { url, mediaType, fileName = originalName, sizeBytes = file.Length }, "Media uploaded."));
    }
}
