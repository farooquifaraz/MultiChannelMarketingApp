using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarketingApp.Domain.Entities;

namespace MarketingApp.Infrastructure.Persistence.Configurations;

public class WebhookEventLogConfiguration : IEntityTypeConfiguration<WebhookEventLog>
{
    public void Configure(EntityTypeBuilder<WebhookEventLog> builder)
    {
        builder.ToTable("webhook_event_logs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.Provider).HasMaxLength(30).IsRequired();
        builder.Property(e => e.ProviderEventId).HasMaxLength(255).IsRequired();
        builder.Property(e => e.EventType).HasMaxLength(40).IsRequired();
        builder.Property(e => e.RawPayload).HasColumnType("jsonb");
        builder.Property(e => e.ReceivedAt).HasDefaultValueSql("NOW()");

        // Idempotency guard. Same (provider, eventId) -> single insert.
        builder.HasIndex(e => new { e.Provider, e.ProviderEventId }).IsUnique();
        builder.HasIndex(e => e.CampaignMessageId);
    }
}
