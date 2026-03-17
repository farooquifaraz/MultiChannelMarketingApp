using MarketingApp.Application.DTOs;

namespace MarketingApp.Application.Interfaces;

public interface ISmtpSettingsService
{
    Task<SmtpSettingsDto?> GetSettingsAsync(Guid userId);
    Task<SmtpSettingsDto> CreateOrUpdateSettingsAsync(Guid userId, CreateSmtpSettingsDto dto);
    Task<bool> TestSmtpConnectionAsync(Guid userId, string testEmail);
}
