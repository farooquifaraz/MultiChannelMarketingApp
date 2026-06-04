using MarketingApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarketingApp.Infrastructure.Persistence.Configurations;

public class IntegrationCredentialConfiguration : IEntityTypeConfiguration<IntegrationCredential>
{
    public void Configure(EntityTypeBuilder<IntegrationCredential> b)
    {
        b.ToTable("integration_credentials");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.Category).HasMaxLength(20).IsRequired();
        b.Property(x => x.Provider).HasMaxLength(40).IsRequired();
        b.Property(x => x.ApiKey).HasColumnType("text");
        b.Property(x => x.Model).HasMaxLength(120);
        b.Property(x => x.BaseUrl).HasColumnType("text");
        b.Property(x => x.SecondarySecret).HasColumnType("text");
        b.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("NOW()");
        b.HasIndex(x => new { x.Category, x.Provider }).IsUnique();
    }
}
