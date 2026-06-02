namespace MarketingApp.Application.DTOs;

public class TemplateDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    // L1 — WhatsApp media (image/document/video). MediaType ∈ image|document|video.
    public string? MediaUrl { get; set; }
    public string? MediaType { get; set; }
    public string? MediaFileName { get; set; }
    public bool IsActive { get; set; }
    public bool IsShared { get; set; }
    /// <summary>"global" | "groups" | "users" — only meaningful when IsShared = true.</summary>
    public string ShareScope { get; set; } = "global";
    public List<Guid> SharedWithGroupIds { get; set; } = new();
    public List<Guid> SharedWithUserIds { get; set; } = new();
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UpdateTemplateShareDto
{
    public bool IsShared { get; set; }
    /// <summary>"global" | "groups" | "users". Required when IsShared = true.</summary>
    public string ShareScope { get; set; } = "global";
    public List<Guid> SharedWithGroupIds { get; set; } = new();
    public List<Guid> SharedWithUserIds { get; set; } = new();
}

public class CreateTemplateDto
{
    public string Name { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    // L1 — optional WhatsApp media attachment.
    public string? MediaUrl { get; set; }
    public string? MediaType { get; set; }
    public string? MediaFileName { get; set; }
}

public class UpdateTemplateDto
{
    public string Name { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    // L1 — optional WhatsApp media attachment.
    public string? MediaUrl { get; set; }
    public string? MediaType { get; set; }
    public string? MediaFileName { get; set; }
}
