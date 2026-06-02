using MarketingApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarketingApp.Infrastructure.Persistence.Configurations;

public class WhatsAppTemplateConfiguration : IEntityTypeConfiguration<WhatsAppTemplate>
{
    public void Configure(EntityTypeBuilder<WhatsAppTemplate> builder)
    {
        builder.ToTable("whatsapp_templates");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(512).IsRequired();
        builder.Property(x => x.Language).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.BodyText).HasColumnType("text");
        builder.Property(x => x.HeaderType).HasMaxLength(20);
        builder.Property(x => x.MetaTemplateId).HasMaxLength(100);

        // One row per (group, template name, language).
        builder.HasIndex(x => new { x.SmtpGroupId, x.Name, x.Language }).IsUnique();

        builder.HasOne(x => x.SmtpGroup)
               .WithMany()
               .HasForeignKey(x => x.SmtpGroupId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
