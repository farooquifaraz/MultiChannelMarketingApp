namespace MarketingApp.Domain.Entities;

public class Campaign
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid TemplateId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Status { get; set; } = "draft";
    public Guid GroupId { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalContacts { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
    public MessageTemplate Template { get; set; } = null!;
    public ContactGroup Group { get; set; } = null!;
    public ICollection<CampaignMessage> Messages { get; set; } = new List<CampaignMessage>();
}
