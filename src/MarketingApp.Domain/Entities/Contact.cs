namespace MarketingApp.Domain.Entities;

public class Contact
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? WhatsAppNumber { get; set; }
    public Guid? GroupId { get; set; }
    public string? CustomFields { get; set; } // JSON string
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
    public ContactGroup? Group { get; set; }
    public ICollection<CampaignMessage> CampaignMessages { get; set; } = new List<CampaignMessage>();
}
