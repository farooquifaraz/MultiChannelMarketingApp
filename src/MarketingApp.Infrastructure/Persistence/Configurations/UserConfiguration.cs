using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarketingApp.Domain.Entities;

namespace MarketingApp.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(u => u.FullName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(150).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.Role).HasMaxLength(20).HasDefaultValue("user");
        builder.Property(u => u.IsActive).HasDefaultValue(true);
        builder.Property(u => u.CreatedAt).HasDefaultValueSql("NOW()");
        builder.Property(u => u.UpdatedAt).HasDefaultValueSql("NOW()");
    }
}
