using MarketingApp.Application.Interfaces;
using MarketingApp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketingApp.API.Controllers;

/// <summary>
/// Anonymous endpoints for tracking pixel hits in outbound emails.
/// Returns a 1x1 transparent GIF regardless of whether the lookup succeeded —
/// we never want to leak whether a message ID is valid through HTTP status codes.
/// </summary>
[ApiController]
[AllowAnonymous]
public class TrackingController : ControllerBase
{
    // 1x1 transparent GIF (43 bytes, base64 → bytes)
    private static readonly byte[] PixelGif = Convert.FromBase64String(
        "R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");

    private readonly IGenericRepository<CampaignMessage> _messageRepo;
    private readonly ILogger<TrackingController> _logger;

    public TrackingController(IGenericRepository<CampaignMessage> messageRepo, ILogger<TrackingController> logger)
    {
        _messageRepo = messageRepo;
        _logger = logger;
    }

    [HttpGet("/track/open/{messageId:guid}.gif")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Open(Guid messageId, CancellationToken ct)
    {
        try
        {
            var msg = await _messageRepo.GetByIdAsync(messageId, ct);
            if (msg is not null && msg.OpenedAt is null)
            {
                msg.OpenedAt = DateTime.UtcNow;
                // Don't downgrade explicit "delivered" → "opened" wins as the most-progressed state
                if (msg.Status != "delivered") msg.Status = "opened";
                await _messageRepo.UpdateAsync(msg, ct);
                _logger.LogInformation("Email opened: message {MessageId}", messageId);
            }
        }
        catch (Exception ex)
        {
            // Tracking is best-effort. Never break the pixel image.
            _logger.LogWarning(ex, "Failed to record open for message {MessageId}", messageId);
        }

        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";
        return File(PixelGif, "image/gif");
    }

    /// <summary>
    /// Click-tracking redirect. Records the click on the CampaignMessage, then 302s to the original URL.
    /// Best-effort — even if tracking fails, the user still gets redirected so the click isn't lost.
    /// </summary>
    [HttpGet("/track/click/{messageId:guid}")]
    public async Task<IActionResult> Click(Guid messageId, [FromQuery] string u, CancellationToken ct)
    {
        // Defensive: refuse to redirect to non-absolute URLs (mitigates open-redirect attacks).
        if (string.IsNullOrWhiteSpace(u) || !Uri.TryCreate(u, UriKind.Absolute, out var target)
            || (target.Scheme != Uri.UriSchemeHttp && target.Scheme != Uri.UriSchemeHttps))
        {
            return BadRequest(new { error = "Missing or invalid 'u' parameter." });
        }

        try
        {
            var msg = await _messageRepo.GetByIdAsync(messageId, ct);
            if (msg is not null)
            {
                msg.ClickCount += 1;
                if (msg.ClickedAt is null) msg.ClickedAt = DateTime.UtcNow;
                // First click implies the recipient also opened — record that if it wasn't already
                if (msg.OpenedAt is null) msg.OpenedAt = DateTime.UtcNow;
                if (msg.Status != "delivered") msg.Status = "clicked";
                await _messageRepo.UpdateAsync(msg, ct);
                _logger.LogInformation("Email link clicked: message {MessageId} → {Url}", messageId, target);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record click for message {MessageId}", messageId);
        }

        return Redirect(target.ToString());
    }
}
