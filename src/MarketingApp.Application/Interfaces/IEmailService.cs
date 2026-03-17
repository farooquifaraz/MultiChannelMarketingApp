using MarketingApp.Domain.Entities;

namespace MarketingApp.Application.Interfaces;

public interface IEmailService
{
    Task<bool> SendAsync(string toEmail, string subject, string body, CancellationToken ct = default);
    Task<bool> SendWithUserSettingsAsync(string toEmail, string subject, string body, UserSmtpSettings settings, CancellationToken ct = default);
}
