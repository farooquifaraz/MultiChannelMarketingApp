using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarketingApp.Domain.Entities;

namespace MarketingApp.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Entity).HasMaxLength(50);
        builder.Property(a => a.Details).HasColumnType("jsonb");
        builder.Property(a => a.IpAddress).HasMaxLength(45);
        builder.Property(a => a.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasOne(a => a.User).WithMany(u => u.AuditLogs).HasForeignKey(a => a.UserId);
    }
}
