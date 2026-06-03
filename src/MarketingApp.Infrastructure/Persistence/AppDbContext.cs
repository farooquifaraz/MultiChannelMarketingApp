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
    public DbSet<SystemSettings> SystemSettings => Set<SystemSettings>();
    public DbSet<SmtpGroup> SmtpGroups => Set<SmtpGroup>();
    public DbSet<TemplateSharedGroup> TemplateSharedGroups => Set<TemplateSharedGroup>();
    public DbSet<TemplateSharedUser> TemplateSharedUsers => Set<TemplateSharedUser>();
    // Day 7 G2
    public DbSet<WebhookEventLog> WebhookEventLogs => Set<WebhookEventLog>();
    // Day 7 G3
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<OutboundReply> OutboundReplies => Set<OutboundReply>();
    // Day 9
    public DbSet<InboxAiChat> InboxAiChats => Set<InboxAiChat>();
    // L2 — cached WhatsApp approved templates
    public DbSet<WhatsAppTemplate> WhatsAppTemplates => Set<WhatsAppTemplate>();
    // Phase 2 — billing
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    // P2.4 — multi-tenancy
    public DbSet<Organization> Organizations => Set<Organization>();
    // Phase 3 — AI image generation
    public DbSet<GeneratedAsset> GeneratedAssets => Set<GeneratedAsset>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
