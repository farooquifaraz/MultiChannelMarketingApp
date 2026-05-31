using AutoMapper;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Domain.Entities;
using MarketingApp.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Application.Services;

public class SmtpSettingsService : ISmtpSettingsService
{
    private readonly IGenericRepository<UserSmtpSettings> _settingsRepo;
    private readonly IEmailService _emailService;
    private readonly IMapper _mapper;
    private readonly ILogger<SmtpSettingsService> _logger;

    public SmtpSettingsService(
        IGenericRepository<UserSmtpSettings> settingsRepo,
        IEmailService emailService,
        IMapper mapper,
        ILogger<SmtpSettingsService> logger)
    {
        _settingsRepo = settingsRepo;
        _emailService = emailService;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<SmtpSettingsDto?> GetSettingsAsync(Guid userId)
    {
        var settings = (await _settingsRepo.FindAsync(s => s.UserId == userId)).FirstOrDefault();
        return settings is null ? null : _mapper.Map<SmtpSettingsDto>(settings);
    }

    public async Task<SmtpSettingsDto> CreateOrUpdateSettingsAsync(Guid userId, CreateSmtpSettingsDto dto)
    {
        var existing = (await _settingsRepo.FindAsync(s => s.UserId == userId)).FirstOrDefault();

        if (existing is null)
        {
            var settings = new UserSmtpSettings
            {
                UserId = userId,
                EmailProvider = dto.EmailProvider,
                SendGridApiKey = dto.SendGridApiKey,
                BrevoApiKey = dto.BrevoApiKey,
                MailgunApiKey = dto.MailgunApiKey,
                MailgunDomain = dto.MailgunDomain,
                SmtpHost = dto.SmtpHost,
                SmtpPort = dto.SmtpPort,
                SmtpUsername = dto.SmtpUsername,
                SmtpPassword = dto.SmtpPassword,
                SmtpFromEmail = dto.SmtpFromEmail,
                SmtpFromName = dto.SmtpFromName,
                SmtpEnableSsl = dto.SmtpEnableSsl,
                SmtpUseDefaultCredentials = dto.SmtpUseDefaultCredentials,
                SmtpTimeout = dto.SmtpTimeout,
                WhatsAppApiKey = dto.WhatsAppApiKey,
                WhatsAppPhoneNumberId = dto.WhatsAppPhoneNumberId,
                WhatsAppBusinessAccountId = dto.WhatsAppBusinessAccountId,
                SmsApiKey = dto.SmsApiKey,
                SmsApiSecret = dto.SmsApiSecret,
                SmsSenderNumber = dto.SmsSenderNumber,
                NotifyOnCampaignComplete = dto.NotifyOnCampaignComplete,
                NotifyOnMessageFailed = dto.NotifyOnMessageFailed,
                NotificationEmail = dto.NotificationEmail,
                SignatureDesignation = dto.SignatureDesignation,
                SignaturePhone = dto.SignaturePhone,
                CompanyWebsite = dto.CompanyWebsite,
                SignatureImageUrl = dto.SignatureImageUrl
            };

            await _settingsRepo.AddAsync(settings);
            _logger.LogInformation("Created SMTP settings for user {UserId}", userId);
            return _mapper.Map<SmtpSettingsDto>(settings);
        }
        else
        {
            existing.SmtpHost = dto.SmtpHost;
            existing.SmtpPort = dto.SmtpPort;
            existing.SmtpUsername = dto.SmtpUsername;
            existing.SmtpPassword = dto.SmtpPassword;
            existing.SmtpFromEmail = dto.SmtpFromEmail;
            existing.SmtpFromName = dto.SmtpFromName;
            existing.SmtpEnableSsl = dto.SmtpEnableSsl;
            existing.SmtpUseDefaultCredentials = dto.SmtpUseDefaultCredentials;
            existing.SmtpTimeout = dto.SmtpTimeout;
            existing.EmailProvider = dto.EmailProvider;
            existing.SendGridApiKey = dto.SendGridApiKey;
            existing.BrevoApiKey = dto.BrevoApiKey;
            existing.MailgunApiKey = dto.MailgunApiKey;
            existing.MailgunDomain = dto.MailgunDomain;
            existing.WhatsAppApiKey = dto.WhatsAppApiKey;
            existing.WhatsAppPhoneNumberId = dto.WhatsAppPhoneNumberId;
            existing.WhatsAppBusinessAccountId = dto.WhatsAppBusinessAccountId;
            existing.SmsApiKey = dto.SmsApiKey;
            existing.SmsApiSecret = dto.SmsApiSecret;
            existing.SmsSenderNumber = dto.SmsSenderNumber;
            existing.NotifyOnCampaignComplete = dto.NotifyOnCampaignComplete;
            existing.NotifyOnMessageFailed = dto.NotifyOnMessageFailed;
            existing.NotificationEmail = dto.NotificationEmail;
            existing.SignatureDesignation = dto.SignatureDesignation;
            existing.SignaturePhone = dto.SignaturePhone;
            existing.CompanyWebsite = dto.CompanyWebsite;
            existing.SignatureImageUrl = dto.SignatureImageUrl;
            existing.UpdatedAt = DateTime.UtcNow;

            await _settingsRepo.UpdateAsync(existing);
            _logger.LogInformation("Updated SMTP settings for user {UserId}", userId);
            return _mapper.Map<SmtpSettingsDto>(existing);
        }
    }

    public async Task<bool> TestSmtpConnectionAsync(Guid userId, string testEmail)
    {
        var settings = (await _settingsRepo.FindAsync(s => s.UserId == userId)).FirstOrDefault()
            ?? throw new NotFoundException("SMTP settings not configured. Please configure your email settings first.");

        _logger.LogInformation("Testing SMTP for user {UserId} | Provider: {Provider} | Host: {Host} | Username: {Username} | FromEmail: {From}",
            userId, settings.EmailProvider, settings.SmtpHost, settings.SmtpUsername, settings.SmtpFromEmail);

        var provider = (settings.EmailProvider ?? "smtp").ToLower();

        // Validate settings before testing
        if (provider == "smtp")
        {
            if (string.IsNullOrWhiteSpace(settings.SmtpHost))
                throw new NotFoundException("SMTP Host is not configured. Please enter your SMTP host (e.g., smtp.hostinger.com).");
            if (string.IsNullOrWhiteSpace(settings.SmtpUsername))
                throw new NotFoundException("SMTP Username is not configured. Please enter your email username.");
            if (string.IsNullOrWhiteSpace(settings.SmtpPassword))
                throw new NotFoundException("SMTP Password is not configured. Please enter your email password.");
        }
        else if (provider == "sendgrid" && string.IsNullOrWhiteSpace(settings.SendGridApiKey))
            throw new NotFoundException("SendGrid API Key is not configured.");
        else if (provider == "brevo" && string.IsNullOrWhiteSpace(settings.BrevoApiKey))
            throw new NotFoundException("Brevo API Key is not configured.");
        else if (provider == "mailgun" && (string.IsNullOrWhiteSpace(settings.MailgunApiKey) || string.IsNullOrWhiteSpace(settings.MailgunDomain)))
            throw new NotFoundException("Mailgun API Key or Domain is not configured.");

        var result = await _emailService.SendWithUserSettingsAsync(
            testEmail,
            "MarketPro - SMTP Test Email",
            @"<div style='font-family:Arial,sans-serif;max-width:600px;margin:0 auto;padding:20px;'>
                <div style='background:linear-gradient(135deg,#4F46E5,#7C3AED);padding:30px;border-radius:12px 12px 0 0;text-align:center;'>
                    <h1 style='color:white;margin:0;'>✅ MarketPro</h1>
                </div>
                <div style='background:#f9fafb;padding:30px;border:1px solid #e5e7eb;border-radius:0 0 12px 12px;'>
                    <h2 style='color:#1f2937;'>SMTP Test Successful!</h2>
                    <p style='color:#4b5563;font-size:16px;'>Your email settings are configured correctly. You can now send marketing campaigns.</p>
                    <p style='color:#6b7280;font-size:14px;'>Provider: <strong>" + provider.ToUpper() + @"</strong></p>
                    <p style='color:#6b7280;font-size:14px;'>From: <strong>" + (settings.SmtpFromEmail ?? settings.SmtpUsername) + @"</strong></p>
                    <hr style='border:1px solid #e5e7eb;margin:20px 0;'/>
                    <p style='color:#9ca3af;font-size:12px;'>Sent from MarketPro Marketing Platform</p>
                </div>
            </div>",
            settings);

        if (!result)
            throw new InvalidOperationException("Failed to send test email. Please check your SMTP credentials and try again.");

        return result;
    }
}
