namespace MarketingApp.Application.DTOs;

public record SmtpSettingsDto
{
    public Guid Id { get; init; }
    public string EmailProvider { get; init; } = "smtp";
    public string? SendGridApiKey { get; init; }
    public string? BrevoApiKey { get; init; }
    public string? MailgunApiKey { get; init; }
    public string? MailgunDomain { get; init; }
    public string? SmtpHost { get; init; }
    public int SmtpPort { get; init; }
    public string? SmtpUsername { get; init; }
    public string? SmtpFromEmail { get; init; }
    public string? SmtpFromName { get; init; }
    public bool SmtpEnableSsl { get; init; }
    public bool SmtpUseDefaultCredentials { get; init; }
    public int SmtpTimeout { get; init; }
    public string? WhatsAppApiKey { get; init; }
    public string? WhatsAppPhoneNumberId { get; init; }
    public string? WhatsAppBusinessAccountId { get; init; }
    public string? SmsApiKey { get; init; }
    public string? SmsSenderNumber { get; init; }
    public bool NotifyOnCampaignComplete { get; init; }
    public bool NotifyOnMessageFailed { get; init; }
    public string? NotificationEmail { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record CreateSmtpSettingsDto
{
    public string SmtpHost { get; init; } = string.Empty;
    public int SmtpPort { get; init; } = 587;
    public string SmtpUsername { get; init; } = string.Empty;
    public string SmtpPassword { get; init; } = string.Empty;
    public string SmtpFromEmail { get; init; } = string.Empty;
    public string? SmtpFromName { get; init; }
    public bool SmtpEnableSsl { get; init; } = true;
    public bool SmtpUseDefaultCredentials { get; init; }
    public int SmtpTimeout { get; init; } = 30000;
    public string EmailProvider { get; init; } = "smtp";
    public string? SendGridApiKey { get; init; }
    public string? BrevoApiKey { get; init; }
    public string? MailgunApiKey { get; init; }
    public string? MailgunDomain { get; init; }
    public string? WhatsAppApiKey { get; init; }
    public string? WhatsAppPhoneNumberId { get; init; }
    public string? WhatsAppBusinessAccountId { get; init; }
    public string? SmsApiKey { get; init; }
    public string? SmsApiSecret { get; init; }
    public string? SmsSenderNumber { get; init; }
    public bool NotifyOnCampaignComplete { get; init; } = true;
    public bool NotifyOnMessageFailed { get; init; } = true;
    public string? NotificationEmail { get; init; }
}

public record UpdateSmtpSettingsDto
{
    public string? SmtpHost { get; init; }
    public int? SmtpPort { get; init; }
    public string? SmtpUsername { get; init; }
    public string? SmtpPassword { get; init; }
    public string? SmtpFromEmail { get; init; }
    public string? SmtpFromName { get; init; }
    public bool? SmtpEnableSsl { get; init; }
    public bool? SmtpUseDefaultCredentials { get; init; }
    public int? SmtpTimeout { get; init; }
    public string? EmailProvider { get; init; }
    public string? SendGridApiKey { get; init; }
    public string? BrevoApiKey { get; init; }
    public string? MailgunApiKey { get; init; }
    public string? MailgunDomain { get; init; }
    public string? WhatsAppApiKey { get; init; }
    public string? WhatsAppPhoneNumberId { get; init; }
    public string? WhatsAppBusinessAccountId { get; init; }
    public string? SmsApiKey { get; init; }
    public string? SmsApiSecret { get; init; }
    public string? SmsSenderNumber { get; init; }
    public bool? NotifyOnCampaignComplete { get; init; }
    public bool? NotifyOnMessageFailed { get; init; }
    public string? NotificationEmail { get; init; }
}

public record TestSmtpDto
{
    public string TestEmail { get; init; } = string.Empty;
}
