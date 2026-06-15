namespace MarketingApp.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "user";

    // === Self-service password reset (forgot password) ===
    // A short-lived random token emailed to the user; cleared once the password is reset.
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // SMTP group assignment — every user (except admin who never sends) should belong to a group.
    // If null, the platform's default SmtpGroup (IsDefault=true) is used.
    public Guid? SmtpGroupId { get; set; }
    public SmtpGroup? SmtpGroup { get; set; }

    // P2.4 — tenant boundary. Nullable + backfilled to the seeded "Legacy Organization" so existing
    // data and queries are unaffected. Query-level isolation is deferred (gated by EnableMultiTenancy).
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    // === Personal email signature (per-user override over SmtpGroup defaults) ===
    // sender_name comes from FullName above.
    public string? SignatureDesignation { get; set; }
    public string? SignaturePhone { get; set; }
    public string? SignatureImageUrl { get; set; }

    // Navigation
    public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
    public ICollection<ContactGroup> ContactGroups { get; set; } = new List<ContactGroup>();
    public ICollection<MessageTemplate> Templates { get; set; } = new List<MessageTemplate>();
    public ICollection<Campaign> Campaigns { get; set; } = new List<Campaign>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public UserSmtpSettings? SmtpSettings { get; set; }
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
