namespace MarketingApp.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Entity { get; set; }
    public Guid? EntityId { get; set; }
    public string? Details { get; set; } // JSON
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User? User { get; set; }
}
