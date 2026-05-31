using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarketingApp.Domain.Entities;

namespace MarketingApp.Infrastructure.Persistence.Configurations;

public class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> b)
    {
        b.ToTable("inbox_messages");
        b.HasKey(m => m.Id);
        b.Property(m => m.Id).HasDefaultValueSql("gen_random_uuid()");

        b.Property(m => m.FromEmail).HasMaxLength(255).IsRequired();
        b.Property(m => m.FromName).HasMaxLength(200);
        b.Property(m => m.ToEmail).HasMaxLength(255).IsRequired();
        b.Property(m => m.Subject).HasMaxLength(500).IsRequired();
        b.Property(m => m.HtmlBody).HasColumnType("text");
        b.Property(m => m.TextBody).HasColumnType("text");
        b.Property(m => m.ImapFolder).HasMaxLength(100).HasDefaultValue("INBOX");
        b.Property(m => m.MessageId).HasMaxLength(255);
        b.Property(m => m.InReplyToMessageId).HasMaxLength(255);
        b.Property(m => m.ReferencesHeader).HasMaxLength(2000);
        b.Property(m => m.NormalizedSubject).HasMaxLength(500);

        b.Property(m => m.AiCategory).HasMaxLength(40);
        b.Property(m => m.AiSummary).HasColumnType("text");
        b.Property(m => m.AiSuggestedReply).HasColumnType("text");
        b.Property(m => m.UserEditedReply).HasColumnType("text");
        b.Property(m => m.AiGenerationError).HasColumnType("text");
        b.Property(m => m.AiProviderUsed).HasMaxLength(30);
        b.Property(m => m.AiSuggestedQuestions).HasColumnType("text");

        b.Property(m => m.CreatedAt).HasDefaultValueSql("NOW()");
        b.Property(m => m.UpdatedAt).HasDefaultValueSql("NOW()");

        // === Indexes ===
        // Per-user list scoping is THE most common query — must be indexed.
        b.HasIndex(m => m.OwnerUserId);
        // Unread badge query: WHERE OwnerUserId AND NOT IsRead.
        b.HasIndex(m => new { m.OwnerUserId, m.IsRead });
        // IMAP dedup: same UID in the same folder must collide.
        b.HasIndex(m => new { m.SmtpGroupId, m.ImapFolder, m.ImapUid }).IsUnique();
        // Reply lookup by thread.
        b.HasIndex(m => m.MatchedCampaignMessageId);
        b.HasIndex(m => m.InReplyToMessageId);
        // Day 8 threading indexes
        b.HasIndex(m => m.MessageId);              // match a later reply's In-Reply-To to this message
        b.HasIndex(m => m.ThreadId);               // conversation assembly
        b.HasIndex(m => new { m.OwnerUserId, m.ThreadId });  // per-user thread list
        b.HasIndex(m => m.NormalizedSubject);      // subject-fallback grouping

        // === Foreign keys ===
        // Owner is critical — Restrict so we never accidentally delete a user with unread inbox.
        b.HasOne(m => m.OwnerUser).WithMany().HasForeignKey(m => m.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(m => m.SmtpGroup).WithMany().HasForeignKey(m => m.SmtpGroupId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(m => m.MatchedCampaignMessage).WithMany().HasForeignKey(m => m.MatchedCampaignMessageId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(m => m.MatchedContact).WithMany().HasForeignKey(m => m.MatchedContactId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class OutboundReplyConfiguration : IEntityTypeConfiguration<OutboundReply>
{
    public void Configure(EntityTypeBuilder<OutboundReply> b)
    {
        b.ToTable("outbound_replies");
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(r => r.Subject).HasMaxLength(500).IsRequired();
        b.Property(r => r.ErrorMessage).HasMaxLength(2000);
        b.Property(r => r.SentAt).HasDefaultValueSql("NOW()");
        // Day 8 threading
        b.Property(r => r.SmtpMessageId).HasMaxLength(255);
        b.Property(r => r.BodyHtml).HasColumnType("text");
        b.Property(r => r.InReplyToMessageId).HasMaxLength(255);

        b.HasIndex(r => r.InboxMessageId);
        b.HasIndex(r => r.SentByUserId);
        b.HasIndex(r => r.SmtpMessageId);   // recipient reply-to-our-reply matching
        b.HasIndex(r => r.ThreadId);        // thread assembly

        b.HasOne(r => r.InboxMessage).WithMany().HasForeignKey(r => r.InboxMessageId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(r => r.SentByUser).WithMany().HasForeignKey(r => r.SentByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
