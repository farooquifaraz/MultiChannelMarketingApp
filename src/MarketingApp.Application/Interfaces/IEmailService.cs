using MarketingApp.Application.DTOs;
using MarketingApp.Domain.Entities;

namespace MarketingApp.Application.Interfaces;

public interface IEmailService
{
    Task<bool> SendAsync(string toEmail, string subject, string body, CancellationToken ct = default);
    Task<bool> SendWithUserSettingsAsync(string toEmail, string subject, string body, UserSmtpSettings settings, CancellationToken ct = default);

    /// <summary>
    /// Send with custom headers attached (Message-Id + X-Campaign-Message-Id + In-Reply-To / References for threading).
    /// New code paths (CampaignJobService, InboxService send-reply) should prefer this overload so we can correlate
    /// provider webhooks and inbox replies back to the originating CampaignMessage.
    /// </summary>
    Task<bool> SendWithUserSettingsAsync(string toEmail, string subject, string body, UserSmtpSettings settings, EmailHeaders headers, CancellationToken ct = default);
}
