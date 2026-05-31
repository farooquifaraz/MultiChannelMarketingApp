namespace MarketingApp.Application.DTOs;

public class ContactDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? WhatsAppNumber { get; set; }
    public Guid? GroupId { get; set; }
    public string? GroupName { get; set; }
    public Dictionary<string, string>? CustomFields { get; set; }
    public bool IsActive { get; set; }
    public bool IsBounced { get; set; }
    public DateTime? BouncedAt { get; set; }
    public string? BounceReason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateContactDto
{
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? WhatsAppNumber { get; set; }
    public Guid? GroupId { get; set; }
    public Dictionary<string, string>? CustomFields { get; set; }
}

public class UpdateContactDto
{
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? WhatsAppNumber { get; set; }
    public Guid? GroupId { get; set; }
    public Dictionary<string, string>? CustomFields { get; set; }
}

public class ContactGroupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ContactCount { get; set; }
    public Guid? SmtpGroupId { get; set; }
    public string? SmtpGroupName { get; set; }
    public Guid OwnerUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateContactGroupDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>
    /// When true (default), the group is automatically linked to the creator's own SmtpGroup
    /// so teammates in the same SmtpGroup see the contacts. Set false to keep it private.
    /// </summary>
    public bool ShareWithTeam { get; set; } = true;
    /// <summary>
    /// Admin-only override — explicitly target a different SmtpGroup. Ignored for non-admins.
    /// If null and ShareWithTeam is true, the creator's own SmtpGroup is used.
    /// </summary>
    public Guid? SmtpGroupId { get; set; }
}

public class ImportResultDto
{
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> Errors { get; set; } = new();
}
