namespace MarketingApp.Application.DTOs;

/// <summary>A cached WhatsApp approved template, surfaced to the admin UI + compose picker (L2).</summary>
public class WhatsAppTemplateDto
{
    public Guid Id { get; set; }
    public Guid SmtpGroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Language { get; set; } = "en_US";
    public string Category { get; set; } = "MARKETING";
    public string Status { get; set; } = "PENDING";
    public string BodyText { get; set; } = string.Empty;
    public int VariableCount { get; set; }
    public string? HeaderType { get; set; }
    public DateTime SyncedAt { get; set; }
}

/// <summary>Result of a Meta template sync run.</summary>
public class WhatsAppTemplateSyncResultDto
{
    public int Synced { get; set; }
    public int Approved { get; set; }
    public string? Message { get; set; }
}
