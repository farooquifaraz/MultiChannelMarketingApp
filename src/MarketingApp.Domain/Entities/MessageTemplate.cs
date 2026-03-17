namespace MarketingApp.Domain.Entities;

public class MessageTemplate
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty; // email, whatsapp, sms
    public string? Subject { get; set; } // email only
    public string Body { get; set; } = string.Empty; // supports {{name}}, {{offer}} etc.
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
    public ICollection<Campaign> Campaigns { get; set; } = new List<Campaign>();
}
