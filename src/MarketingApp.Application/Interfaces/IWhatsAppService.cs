using MarketingApp.Application.DTOs;
using MarketingApp.Domain.Entities;

namespace MarketingApp.Application.Interfaces;

public interface IWhatsAppService
{
    Task<bool> SendAsync(string phoneNumber, string message, CancellationToken ct = default);

    /// <summary>
    /// Sends a WhatsApp message via the Meta Cloud API. When <paramref name="media"/> is supplied
    /// the message is sent as image/document/video with the text as caption; otherwise plain text.
    /// The optional media param keeps existing text-only callers source-compatible.
    /// </summary>
    Task<bool> SendWithUserSettingsAsync(string phoneNumber, string message, UserSmtpSettings settings, WhatsAppMedia? media = null, CancellationToken ct = default);
}
