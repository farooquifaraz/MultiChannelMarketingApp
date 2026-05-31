namespace MarketingApp.Domain.Entities;

public class UserSmtpSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    // Provider: "smtp", "sendgrid", "brevo", "mailgun"
    public string EmailProvider { get; set; } = "smtp";

    // SendGrid Settings
    public string? SendGridApiKey { get; set; }

    // Brevo Settings
    public string? BrevoApiKey { get; set; }

    // Mailgun Settings
    public string? MailgunApiKey { get; set; }
    public string? MailgunDomain { get; set; }

    // Email SMTP Settings
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public string? SmtpFromEmail { get; set; }
    public string? SmtpFromName { get; set; }
    public bool SmtpEnableSsl { get; set; } = true;
    public bool SmtpUseDefaultCredentials { get; set; }
    public int SmtpTimeout { get; set; } = 30000;

    // WhatsApp Settings
    public string? WhatsAppApiKey { get; set; }
    public string? WhatsAppPhoneNumberId { get; set; }
    public string? WhatsAppBusinessAccountId { get; set; }

    // SMS Settings
    public string? SmsApiKey { get; set; }
    public string? SmsApiSecret { get; set; }
    public string? SmsSenderNumber { get; set; }

    // Notification Settings
    public bool NotifyOnCampaignComplete { get; set; } = true;
    public bool NotifyOnMessageFailed { get; set; } = true;
    public string? NotificationEmail { get; set; }

    // Email Signature — used to render {{sender_designation}}, {{sender_phone}}, {{company_website}}, {{signature_image}} in templates
    public string? SignatureDesignation { get; set; }
    public string? SignaturePhone { get; set; }
    public string? CompanyWebsite { get; set; }
    public string? SignatureImageUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
}
