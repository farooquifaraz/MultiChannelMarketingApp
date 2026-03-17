using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarketingApp.Domain.Entities;

namespace MarketingApp.Infrastructure.Persistence.Configurations;

public class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable("contacts");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(c => c.FullName).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(150);
        builder.Property(c => c.Phone).HasMaxLength(20);
        builder.Property(c => c.WhatsAppNumber).HasMaxLength(20);
        builder.Property(c => c.CustomFields).HasColumnType("jsonb");
        builder.Property(c => c.IsActive).HasDefaultValue(true);
        builder.Property(c => c.CreatedAt).HasDefaultValueSql("NOW()");
        builder.Property(c => c.UpdatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(c => c.UserId);
        builder.HasIndex(c => c.GroupId);
        builder.HasIndex(c => c.Email);
        builder.HasIndex(c => c.Phone);

        builder.HasOne(c => c.User).WithMany(u => u.Contacts).HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(c => c.Group).WithMany(g => g.Contacts).HasForeignKey(c => c.GroupId).OnDelete(DeleteBehavior.SetNull);
    }
}
