using MarketingApp.Domain.Entities;

namespace MarketingApp.Application.Interfaces;

public interface ISmsService
{
    Task<bool> SendAsync(string phoneNumber, string message, CancellationToken ct = default);
    Task<bool> SendWithUserSettingsAsync(string phoneNumber, string message, UserSmtpSettings settings, CancellationToken ct = default);
}
