using Microsoft.EntityFrameworkCore;
using MarketingApp.Domain.Entities;

namespace MarketingApp.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<ContactGroup> ContactGroups => Set<ContactGroup>();
    public DbSet<MessageTemplate> MessageTemplates => Set<MessageTemplate>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CampaignMessage> CampaignMessages => Set<CampaignMessage>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserSmtpSettings> UserSmtpSettings => Set<UserSmtpSettings>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
