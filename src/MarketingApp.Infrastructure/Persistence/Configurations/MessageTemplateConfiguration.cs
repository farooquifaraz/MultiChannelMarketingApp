using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarketingApp.Domain.Entities;

namespace MarketingApp.Infrastructure.Persistence.Configurations;

public class MessageTemplateConfiguration : IEntityTypeConfiguration<MessageTemplate>
{
    public void Configure(EntityTypeBuilder<MessageTemplate> builder)
    {
        builder.ToTable("message_templates");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Channel).HasMaxLength(20).IsRequired();
        builder.Property(t => t.Subject).HasMaxLength(200);
        builder.Property(t => t.Body).IsRequired();
        builder.Property(t => t.IsActive).HasDefaultValue(true);
        builder.Property(t => t.IsShared).HasDefaultValue(false);
        builder.Property(t => t.ShareScope).HasMaxLength(20).HasDefaultValue("global");
        builder.Property(t => t.CreatedAt).HasDefaultValueSql("NOW()");
        builder.Property(t => t.UpdatedAt).HasDefaultValueSql("NOW()");

        builder.HasOne(t => t.User).WithMany(u => u.Templates).HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
