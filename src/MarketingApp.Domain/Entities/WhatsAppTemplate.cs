namespace MarketingApp.Domain.Entities;

/// <summary>
/// A WhatsApp message template as approved (or pending) in Meta Business Manager, cached locally
/// per SmtpGroup (each group maps to one WhatsApp Business Account). Business-initiated WhatsApp
/// messages outside the 24-hour customer-service window MUST use one of these approved templates —
/// you cannot send free-form marketing text. We sync them from the Meta Graph API and let users
/// pick one + fill its {{1}}, {{2}} … variables when composing a campaign.
/// </summary>
public class WhatsAppTemplate
{
    public Guid Id { get; set; }

    /// <summary>The SmtpGroup (WhatsApp Business Account) this template belongs to.</summary>
    public Guid SmtpGroupId { get; set; }

    /// <summary>Meta template name (unique per WABA + language), e.g. "property_listing".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Language/locale code, e.g. "en_US", "ar".</summary>
    public string Language { get; set; } = "en_US";

    /// <summary>MARKETING | UTILITY | AUTHENTICATION.</summary>
    public string Category { get; set; } = "MARKETING";

    /// <summary>APPROVED | PENDING | REJECTED | DISABLED — only APPROVED can be sent.</summary>
    public string Status { get; set; } = "PENDING";

    /// <summary>The BODY component text, with {{1}}, {{2}} … positional placeholders.</summary>
    public string BodyText { get; set; } = string.Empty;

    /// <summary>How many {{n}} variables the body expects (drives the variable-mapping UI).</summary>
    public int VariableCount { get; set; }

    /// <summary>Header type if the template has one: TEXT | IMAGE | DOCUMENT | VIDEO | null.</summary>
    public string? HeaderType { get; set; }

    /// <summary>Meta's own template id, for traceability.</summary>
    public string? MetaTemplateId { get; set; }

    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public SmtpGroup SmtpGroup { get; set; } = null!;
}
