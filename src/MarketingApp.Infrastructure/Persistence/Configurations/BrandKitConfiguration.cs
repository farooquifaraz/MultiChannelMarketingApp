using MarketingApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarketingApp.Infrastructure.Persistence.Configurations;

public class BrandKitConfiguration : IEntityTypeConfiguration<BrandKit>
{
    public void Configure(EntityTypeBuilder<BrandKit> b)
    {
        b.ToTable("brand_kits");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.LogoUrl).HasColumnType("text");
        b.Property(x => x.PrimaryColor).HasMaxLength(9).IsRequired();
        b.Property(x => x.SecondaryColor).HasMaxLength(9);
        b.Property(x => x.AccentColor).HasMaxLength(9);
        b.Property(x => x.FontFamily).HasMaxLength(60).HasDefaultValue("Inter");
        b.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("NOW()");

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.UserId);
    }
}
