namespace MarketingApp.Application.DTOs;

/// <summary>Read model for an inbox alias (receiving address -> owning user).</summary>
public sealed class InboxAliasDto
{
    public Guid Id { get; set; }
    public string Address { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public Guid? SmtpGroupId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Create payload for a new inbox alias.</summary>
public sealed class CreateInboxAliasDto
{
    public string Address { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    /// <summary>Optional — scope the alias to a single SmtpGroup. Null = matches any group.</summary>
    public Guid? SmtpGroupId { get; set; }
}
