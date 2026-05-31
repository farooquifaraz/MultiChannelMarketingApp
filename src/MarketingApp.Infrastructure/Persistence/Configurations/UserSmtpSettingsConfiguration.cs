using MarketingApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarketingApp.Infrastructure.Persistence.Configurations;

public class UserSmtpSettingsConfiguration : IEntityTypeConfiguration<UserSmtpSettings>
{
    public void Configure(EntityTypeBuilder<UserSmtpSettings> builder)
    {
        builder.ToTable("user_smtp_settings");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.SmtpHost).HasMaxLength(200);
        builder.Property(x => x.SmtpUsername).HasMaxLength(200);
        builder.Property(x => x.SmtpPassword).HasMaxLength(500);
        builder.Property(x => x.SmtpFromEmail).HasMaxLength(200);
        builder.Property(x => x.SmtpFromName).HasMaxLength(100);
        builder.Property(x => x.SmtpPort).HasDefaultValue(587);
        builder.Property(x => x.SmtpEnableSsl).HasDefaultValue(true);
        builder.Property(x => x.SmtpUseDefaultCredentials).HasDefaultValue(false);
        builder.Property(x => x.SmtpTimeout).HasDefaultValue(30000);

        builder.Property(x => x.EmailProvider).HasMaxLength(20).HasDefaultValue("smtp");
        builder.Property(x => x.SendGridApiKey).HasMaxLength(500);
        builder.Property(x => x.BrevoApiKey).HasMaxLength(500);
        builder.Property(x => x.MailgunApiKey).HasMaxLength(500);
        builder.Property(x => x.MailgunDomain).HasMaxLength(200);

        builder.Property(x => x.WhatsAppApiKey).HasMaxLength(500);
        builder.Property(x => x.WhatsAppPhoneNumberId).HasMaxLength(100);
        builder.Property(x => x.WhatsAppBusinessAccountId).HasMaxLength(100);

        builder.Property(x => x.SmsApiKey).HasMaxLength(500);
        builder.Property(x => x.SmsApiSecret).HasMaxLength(500);
        builder.Property(x => x.SmsSenderNumber).HasMaxLength(20);

        builder.Property(x => x.NotifyOnCampaignComplete).HasDefaultValue(true);
        builder.Property(x => x.NotifyOnMessageFailed).HasDefaultValue(true);
        builder.Property(x => x.NotificationEmail).HasMaxLength(200);

        // Signature fields
        builder.Property(x => x.SignatureDesignation).HasMaxLength(100);
        builder.Property(x => x.SignaturePhone).HasMaxLength(30);
        builder.Property(x => x.CompanyWebsite).HasMaxLength(200);
        builder.Property(x => x.SignatureImageUrl).HasMaxLength(500);

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(x => x.UserId).IsUnique();

        builder.HasOne(x => x.User)
            .WithOne(u => u.SmtpSettings)
            .HasForeignKey<UserSmtpSettings>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
