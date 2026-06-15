using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarketingApp.Domain.Entities;

namespace MarketingApp.Infrastructure.Persistence.Configurations;

public class InboxAliasConfiguration : IEntityTypeConfiguration<InboxAlias>
{
    public void Configure(EntityTypeBuilder<InboxAlias> b)
    {
        b.ToTable("inbox_aliases");
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(a => a.Address).HasMaxLength(255).IsRequired();
        b.Property(a => a.IsActive).HasDefaultValue(true);
        b.Property(a => a.CreatedAt).HasDefaultValueSql("NOW()");

        // One address routes to exactly one owner — the matching predicate relies on this.
        b.HasIndex(a => a.Address).IsUnique();
        b.HasIndex(a => a.UserId);

        b.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(a => a.SmtpGroup)
            .WithMany()
            .HasForeignKey(a => a.SmtpGroupId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
