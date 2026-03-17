namespace MarketingApp.Domain.Entities;

public class CampaignMessage
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid ContactId { get; set; }
    public string Status { get; set; } = "pending";
    public string? ErrorMessage { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? OpenedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Campaign Campaign { get; set; } = null!;
    public Contact Contact { get; set; } = null!;
}
