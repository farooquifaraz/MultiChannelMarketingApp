namespace MarketingApp.Application.DTOs;

public record SmtpGroupDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsDefault { get; init; }
    public bool IsActive { get; init; }

    public string EmailProvider { get; init; } = "smtp";
    public string? SmtpHost { get; init; }
    public int SmtpPort { get; init; }
    public string? SmtpUsername { get; init; }
    // SmtpPassword is intentionally excluded from the read DTO for security.
    public bool SmtpEnableSsl { get; init; }
    public int SmtpTimeout { get; init; }

    public string? SendGridApiKeyMasked { get; init; }
    public string? BrevoApiKeyMasked { get; init; }
    public string? MailgunApiKeyMasked { get; init; }
    public string? MailgunDomain { get; init; }

    public string? FromEmail { get; init; }
    public string? FromName { get; init; }

    public string? SignatureDesignation { get; init; }
    public string? SignaturePhone { get; init; }
    public string? CompanyWebsite { get; init; }
    public string? SignatureImageUrl { get; init; }

    public string? WhatsAppPhoneNumberId { get; init; }
    public string? WhatsAppBusinessAccountId { get; init; }
    public string? SmsSenderNumber { get; init; }

    // Per-group rate limit overrides. Null = inherit global SystemSettings.
    public int? DelayBetweenMessagesMs { get; init; }
    public int? MaxMessagesPerMinute { get; init; }

    // Day 7 G2 — webhook secrets (masked for read)
    public string? SendGridWebhookSecretMasked { get; init; }
    public string? BrevoWebhookSecretMasked { get; init; }
    public string? MailgunWebhookSecretMasked { get; init; }

    // Day 7 G3 — IMAP polling
    public bool EnableInboxPolling { get; init; }
    public string? ImapHost { get; init; }
    public int ImapPort { get; init; } = 993;
    public bool ImapEnableSsl { get; init; } = true;
    public string? ImapUsername { get; init; }
    public string ImapFolder { get; init; } = "INBOX";
    public long LastImapUid { get; init; }
    public DateTime? LastInboxPolledAt { get; init; }
    public int? InboxPollingIntervalMinutes { get; init; }
    public Guid? DefaultInboxOwnerUserId { get; init; }

    public int AssignedUserCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record CreateSmtpGroupDto
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsDefault { get; init; }
    public bool IsActive { get; init; } = true;

    public string EmailProvider { get; init; } = "smtp";

    public string? SmtpHost { get; init; }
    public int SmtpPort { get; init; } = 587;
    public string? SmtpUsername { get; init; }
    public string? SmtpPassword { get; init; }
    public bool SmtpEnableSsl { get; init; } = true;
    public int SmtpTimeout { get; init; } = 30000;

    public string? SendGridApiKey { get; init; }
    public string? BrevoApiKey { get; init; }
    public string? MailgunApiKey { get; init; }
    public string? MailgunDomain { get; init; }

    public string? FromEmail { get; init; }
    public string? FromName { get; init; }

    public string? SignatureDesignation { get; init; }
    public string? SignaturePhone { get; init; }
    public string? CompanyWebsite { get; init; }
    public string? SignatureImageUrl { get; init; }

    public string? WhatsAppApiKey { get; init; }
    public string? WhatsAppPhoneNumberId { get; init; }
    public string? WhatsAppBusinessAccountId { get; init; }

    public string? SmsApiKey { get; init; }
    public string? SmsApiSecret { get; init; }
    public string? SmsSenderNumber { get; init; }

    // Per-group rate limit overrides. Null = inherit global SystemSettings.
    public int? DelayBetweenMessagesMs { get; init; }
    public int? MaxMessagesPerMinute { get; init; }

    // Day 7 G2 — webhook secrets (raw)
    public string? SendGridWebhookSecret { get; init; }
    public string? BrevoWebhookSecret { get; init; }
    public string? MailgunWebhookSecret { get; init; }

    // Day 7 G3 — IMAP polling
    public bool EnableInboxPolling { get; init; }
    public string? ImapHost { get; init; }
    public int ImapPort { get; init; } = 993;
    public bool ImapEnableSsl { get; init; } = true;
    public string? ImapUsername { get; init; }
    public string? ImapPassword { get; init; }
    public string ImapFolder { get; init; } = "INBOX";
    public int? InboxPollingIntervalMinutes { get; init; }
    public Guid? DefaultInboxOwnerUserId { get; init; }
}

public record UpdateSmtpGroupDto : CreateSmtpGroupDto;

public record AssignUsersToGroupDto
{
    public Guid GroupId { get; init; }
    public List<Guid> UserIds { get; init; } = new();
}

public record TestSmtpGroupDto
{
    public string TestEmail { get; init; } = string.Empty;
}

public record UserAssignmentDto
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public Guid? SmtpGroupId { get; init; }
    public string? SmtpGroupName { get; init; }
}
