namespace MarketingApp.Domain.Entities;

public class MessageTemplate
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty; // email, whatsapp, sms
    public string? Subject { get; set; } // email only
    public string Body { get; set; } = string.Empty; // supports {{name}}, {{offer}} etc.

    // === L1: WhatsApp media (optional) ===
    // When set, a WhatsApp message sends this media with Body used as the caption.
    // MediaType ∈ "image" | "document" | "video". MediaUrl must be a publicly reachable
    // https URL (Meta fetches it). MediaFileName is shown to the recipient for documents.
    public string? MediaUrl { get; set; }
    public string? MediaType { get; set; }
    public string? MediaFileName { get; set; }

    public bool IsActive { get; set; } = true;
    // Master switch — when false, only the owner can see this template (private).
    public bool IsShared { get; set; } = false;
    /// <summary>
    /// When IsShared = true, defines who can see this template:
    ///   "global" → all users (the original behavior)
    ///   "groups" → only users assigned to one of the linked SmtpGroups (see SharedWithGroups)
    ///   "users"  → only the specifically listed users (see SharedWithUsers)
    /// </summary>
    public string ShareScope { get; set; } = "global";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
    public ICollection<Campaign> Campaigns { get; set; } = new List<Campaign>();
    public ICollection<TemplateSharedGroup> SharedWithGroups { get; set; } = new List<TemplateSharedGroup>();
    public ICollection<TemplateSharedUser> SharedWithUsers { get; set; } = new List<TemplateSharedUser>();
}

/// <summary>Junction — template visible to users assigned to this SmtpGroup.</summary>
public class TemplateSharedGroup
{
    public Guid TemplateId { get; set; }
    public MessageTemplate Template { get; set; } = null!;
    public Guid SmtpGroupId { get; set; }
    public SmtpGroup SmtpGroup { get; set; } = null!;
}

/// <summary>Junction — template explicitly shared with this individual user.</summary>
public class TemplateSharedUser
{
    public Guid TemplateId { get; set; }
    public MessageTemplate Template { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}
