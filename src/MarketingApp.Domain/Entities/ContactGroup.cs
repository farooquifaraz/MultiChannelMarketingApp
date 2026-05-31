namespace MarketingApp.Domain.Entities;

public class ContactGroup
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>
    /// When set, all users assigned to this SmtpGroup can see the contacts in this ContactGroup.
    /// Null = private (only the owner sees it).
    /// </summary>
    public Guid? SmtpGroupId { get; set; }
    public SmtpGroup? SmtpGroup { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
    public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
}
