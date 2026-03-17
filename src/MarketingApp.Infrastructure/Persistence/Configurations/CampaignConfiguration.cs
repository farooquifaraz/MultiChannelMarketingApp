using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarketingApp.Domain.Entities;

namespace MarketingApp.Infrastructure.Persistence.Configurations;

public class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("campaigns");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(c => c.Name).HasMaxLength(150).IsRequired();
        builder.Property(c => c.Channel).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Status).HasMaxLength(20).HasDefaultValue("draft");
        builder.Property(c => c.TotalContacts).HasDefaultValue(0);
        builder.Property(c => c.SentCount).HasDefaultValue(0);
        builder.Property(c => c.FailedCount).HasDefaultValue(0);
        builder.Property(c => c.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(c => c.UserId);
        builder.HasIndex(c => c.Status);

        builder.HasOne(c => c.User).WithMany(u => u.Campaigns).HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(c => c.Template).WithMany(t => t.Campaigns).HasForeignKey(c => c.TemplateId);
        builder.HasOne(c => c.Group).WithMany().HasForeignKey(c => c.GroupId);
    }
}
