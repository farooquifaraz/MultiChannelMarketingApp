namespace MarketingApp.Domain.Entities;

public class CampaignMessage
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid ContactId { get; set; }
    public string Status { get; set; } = "pending";
    public string? ErrorMessage { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? OpenedAt { get; set; }
    public DateTime? ClickedAt { get; set; }
    /// <summary>Total click count across all links in this message — bumped each time the redirect endpoint fires.</summary>
    public int ClickCount { get; set; }

    /// <summary>
    /// RFC 5322 Message-Id assigned at send time, format "&lt;{guidN}@{fromDomain}&gt;".
    /// Used (a) by provider webhooks to map a delivery event back to the CampaignMessage and
    /// (b) by inbox polling to thread incoming replies via their In-Reply-To header.
    /// Indexed unique when not null.
    /// </summary>
    public string? SmtpMessageId { get; set; }

    /// <summary>
    /// How DeliveredAt was determined.
    ///   "webhook"      = provider (SendGrid/Brevo/Mailgun) confirmed delivery via webhook.
    ///   "best-effort"  = SMTP send returned success; treated as delivered.
    ///   null           = no delivery confirmation yet.
    /// Surfaces in the UI as a small badge so users know how reliable the signal is.
    /// </summary>
    public string? DeliveryConfirmationKind { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Campaign Campaign { get; set; } = null!;
    public Contact Contact { get; set; } = null!;
}
