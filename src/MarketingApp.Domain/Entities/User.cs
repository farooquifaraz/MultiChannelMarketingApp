namespace MarketingApp.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
    public ICollection<ContactGroup> ContactGroups { get; set; } = new List<ContactGroup>();
    public ICollection<MessageTemplate> Templates { get; set; } = new List<MessageTemplate>();
    public ICollection<Campaign> Campaigns { get; set; } = new List<Campaign>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public UserSmtpSettings? SmtpSettings { get; set; }
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
