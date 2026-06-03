using MarketingApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarketingApp.Infrastructure.Persistence.Configurations;

public class GeneratedAssetConfiguration : IEntityTypeConfiguration<GeneratedAsset>
{
    public void Configure(EntityTypeBuilder<GeneratedAsset> b)
    {
        b.ToTable("generated_assets");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.Prompt).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Provider).HasMaxLength(40).HasDefaultValue("mock");
        b.Property(x => x.Size).HasMaxLength(20).IsRequired();
        b.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("pending");
        // ImageUrl can be a long data-URI for the mock provider → unbounded text.
        b.Property(x => x.ImageUrl).HasColumnType("text");
        b.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
    }
}
