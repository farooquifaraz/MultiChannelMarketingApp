using MarketingApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarketingApp.Infrastructure.Persistence.Configurations;

public class SystemSettingsConfiguration : IEntityTypeConfiguration<SystemSettings>
{
    public void Configure(EntityTypeBuilder<SystemSettings> builder)
    {
        builder.ToTable("system_settings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.BatchSize).HasDefaultValue(50);
        builder.Property(x => x.DelayBetweenBatchesMs).HasDefaultValue(500);
        builder.Property(x => x.DelayBetweenMessagesMs).HasDefaultValue(1000);
        builder.Property(x => x.MaxMessagesPerMinute).HasDefaultValue(0);
        builder.Property(x => x.AllowUsersToSeeSharedTemplates).HasDefaultValue(true);
        builder.Property(x => x.AllowUsersToSeeSharedContacts).HasDefaultValue(false);
        builder.Property(x => x.PlatformName).HasMaxLength(100).HasDefaultValue("MarketPro");
        builder.Property(x => x.LogoUrl).HasMaxLength(500);
        builder.Property(x => x.PrimaryColor).HasMaxLength(20).HasDefaultValue("#4f46e5");
        builder.Property(x => x.AvatarServiceUrl).HasMaxLength(500);
        builder.Property(x => x.DefaultLocale).HasMaxLength(20).HasDefaultValue("en-US");
        builder.Property(x => x.DefaultDateFormat).HasMaxLength(50).HasDefaultValue("MMMM dd, yyyy");
        builder.Property(x => x.PasswordMinLength).HasDefaultValue(8);
        builder.Property(x => x.MaxFileUploadSizeMb).HasDefaultValue(10);
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("NOW()");
    }
}
