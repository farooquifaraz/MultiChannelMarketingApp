using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarketingApp.Domain.Entities;

namespace MarketingApp.Infrastructure.Persistence.Configurations;

public class ContactGroupConfiguration : IEntityTypeConfiguration<ContactGroup>
{
    public void Configure(EntityTypeBuilder<ContactGroup> builder)
    {
        builder.ToTable("contact_groups");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(g => g.Name).HasMaxLength(100).IsRequired();
        builder.Property(g => g.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasOne(g => g.User).WithMany(u => u.ContactGroups).HasForeignKey(g => g.UserId).OnDelete(DeleteBehavior.Cascade);

        // Optional link to SmtpGroup — when set, contacts in this group are visible to all users in that SmtpGroup.
        builder.HasOne(g => g.SmtpGroup)
            .WithMany()
            .HasForeignKey(g => g.SmtpGroupId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(g => g.SmtpGroupId);
    }
}
