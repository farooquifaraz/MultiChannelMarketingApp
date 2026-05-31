namespace MarketingApp.Domain.Entities;

/// <summary>
/// An admin-managed bundle of email/whatsapp/sms provider credentials + signature info.
/// Users get assigned to ONE SmtpGroup. Campaigns sent by that user route through this group's
/// configuration (provider credentials, From details, signature).
/// One group is marked IsDefault — it's used when a user has no explicit assignment.
/// </summary>
public class SmtpGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Identity & lifecycle
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;

    // === Provider selection ===
    // "smtp" | "sendgrid" | "brevo" | "mailgun"
    public string EmailProvider { get; set; } = "smtp";

    // === SMTP provider ===
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public bool SmtpEnableSsl { get; set; } = true;
    public int SmtpTimeout { get; set; } = 30000;

    // === API providers ===
    public string? SendGridApiKey { get; set; }
    public string? BrevoApiKey { get; set; }
    public string? MailgunApiKey { get; set; }
    public string? MailgunDomain { get; set; }

    // === From address (mandatory for any provider) ===
    public string? FromEmail { get; set; }
    public string? FromName { get; set; }

    // === Signature info (rendered into templates via {{sender_*}}, {{company_*}} placeholders) ===
    public string? SignatureDesignation { get; set; }
    public string? SignaturePhone { get; set; }
    public string? CompanyWebsite { get; set; }
    public string? SignatureImageUrl { get; set; }

    // === WhatsApp Cloud API (optional, per group) ===
    public string? WhatsAppApiKey { get; set; }
    public string? WhatsAppPhoneNumberId { get; set; }
    public string? WhatsAppBusinessAccountId { get; set; }

    // === SMS gateway (optional, per group) ===
    public string? SmsApiKey { get; set; }
    public string? SmsApiSecret { get; set; }
    public string? SmsSenderNumber { get; set; }

    // === Per-group rate limits ===
    // When set, OVERRIDE the global SystemSettings values during campaign send. Null = inherit global.
    // Useful when one group sends via Gmail (slow, capped) and another via SendGrid (fast).
    public int? DelayBetweenMessagesMs { get; set; }
    public int? MaxMessagesPerMinute { get; set; }

    // === Day 7 G2: Per-provider webhook signing secrets ===
    // Each API provider has its own webhook signature scheme. Secrets are stored per-group so
    // multiple groups using the same provider can have isolated webhook endpoints.
    public string? SendGridWebhookSecret { get; set; }
    public string? BrevoWebhookSecret { get; set; }
    public string? MailgunWebhookSecret { get; set; }

    // === Day 7 G3: IMAP inbox polling config ===
    // When enabled, the inbox-poll background job connects via IMAP and pulls new messages.
    public bool EnableInboxPolling { get; set; }
    public string? ImapHost { get; set; }              // null -> fall back to SmtpHost
    public int ImapPort { get; set; } = 993;
    public bool ImapEnableSsl { get; set; } = true;
    public string? ImapUsername { get; set; }          // null -> reuse SmtpUsername
    public string? ImapPassword { get; set; }          // null -> reuse SmtpPassword
    public string ImapFolder { get; set; } = "INBOX";
    public uint LastImapUid { get; set; }              // dedup cursor (max UID seen)
    public DateTime? LastInboxPolledAt { get; set; }
    public int? InboxPollingIntervalMinutes { get; set; } // null -> inherit SystemSettings cron
    public Guid? DefaultInboxOwnerUserId { get; set; } // catch-all for replies that don't match any campaign

    // === Audit ===
    public Guid CreatedByUserId { get; set; }
    public User? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<User> AssignedUsers { get; set; } = new List<User>();
}
