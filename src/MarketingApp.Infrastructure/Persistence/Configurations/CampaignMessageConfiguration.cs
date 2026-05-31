using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarketingApp.Domain.Entities;

namespace MarketingApp.Infrastructure.Persistence.Configurations;

public class CampaignMessageConfiguration : IEntityTypeConfiguration<CampaignMessage>
{
    public void Configure(EntityTypeBuilder<CampaignMessage> builder)
    {
        builder.ToTable("campaign_messages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(m => m.Status).HasMaxLength(20).HasDefaultValue("pending");
        builder.Property(m => m.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(m => m.CampaignId);
        builder.HasIndex(m => m.Status);
        builder.HasIndex(m => new { m.CampaignId, m.Status });
        // Day 7 G1: SmtpMessageId is the correlation key used by both webhooks (Feature 1) and
        // inbox reply matching (Feature 2). Unique when set so dedup is guaranteed.
        builder.Property(m => m.SmtpMessageId).HasMaxLength(255);
        builder.HasIndex(m => m.SmtpMessageId).IsUnique().HasFilter("smtp_message_id IS NOT NULL");
        builder.Property(m => m.DeliveryConfirmationKind).HasMaxLength(20);

        builder.HasOne(m => m.Campaign).WithMany(c => c.Messages).HasForeignKey(m => m.CampaignId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(m => m.Contact).WithMany(c => c.CampaignMessages).HasForeignKey(m => m.ContactId).OnDelete(DeleteBehavior.Cascade);
    }
}
