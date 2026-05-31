using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarketingApp.Domain.Entities;

namespace MarketingApp.Infrastructure.Persistence.Configurations;

public class InboxAiChatConfiguration : IEntityTypeConfiguration<InboxAiChat>
{
    public void Configure(EntityTypeBuilder<InboxAiChat> b)
    {
        b.ToTable("inbox_ai_chats");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(c => c.Role).HasMaxLength(20).IsRequired();
        b.Property(c => c.Content).HasColumnType("text").IsRequired();
        b.Property(c => c.ProviderUsed).HasMaxLength(30);
        b.Property(c => c.Suggestions).HasColumnType("text");
        b.Property(c => c.CreatedAt).HasDefaultValueSql("NOW()");

        b.HasIndex(c => new { c.ThreadId, c.CreatedAt });
        b.HasIndex(c => c.OwnerUserId);
    }
}
