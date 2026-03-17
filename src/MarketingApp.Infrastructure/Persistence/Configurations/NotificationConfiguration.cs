using MarketingApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarketingApp.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Message).IsRequired();
        builder.Property(x => x.Type).HasMaxLength(20).HasDefaultValue("info");
        builder.Property(x => x.RelatedEntity).HasMaxLength(50);
        builder.Property(x => x.IsRead).HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(x => new { x.UserId, x.IsRead });
        builder.HasIndex(x => x.CreatedAt);

        builder.HasOne(x => x.User)
            .WithMany(u => u.Notifications)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
