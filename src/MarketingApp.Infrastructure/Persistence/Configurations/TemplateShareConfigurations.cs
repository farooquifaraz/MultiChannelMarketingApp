using MarketingApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarketingApp.Infrastructure.Persistence.Configurations;

public class TemplateSharedGroupConfiguration : IEntityTypeConfiguration<TemplateSharedGroup>
{
    public void Configure(EntityTypeBuilder<TemplateSharedGroup> builder)
    {
        builder.ToTable("template_shared_groups");
        builder.HasKey(x => new { x.TemplateId, x.SmtpGroupId });
        builder.HasOne(x => x.Template)
            .WithMany(t => t.SharedWithGroups)
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.SmtpGroup)
            .WithMany()
            .HasForeignKey(x => x.SmtpGroupId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.SmtpGroupId);
    }
}

public class TemplateSharedUserConfiguration : IEntityTypeConfiguration<TemplateSharedUser>
{
    public void Configure(EntityTypeBuilder<TemplateSharedUser> builder)
    {
        builder.ToTable("template_shared_users");
        builder.HasKey(x => new { x.TemplateId, x.UserId });
        builder.HasOne(x => x.Template)
            .WithMany(t => t.SharedWithUsers)
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.UserId);
    }
}
