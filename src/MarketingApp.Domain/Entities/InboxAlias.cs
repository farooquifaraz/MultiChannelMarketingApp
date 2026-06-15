namespace MarketingApp.Domain.Entities;

/// <summary>
/// Maps a receiving email address (alias) to the user who owns it, so the inbox poller can route
/// inbound mail in a SHARED-mailbox setup by the To/Cc/Delivered-To address instead of guessing.
///
/// Example: a single IMAP mailbox receives mail for ali@samdigital.ae and sara@samdigital.ae.
/// Two InboxAlias rows (ali@... -> Ali, sara@... -> Sara) let each user see only their own mail.
///
/// Purely additive: when no alias matches, owner resolution falls back to the existing
/// chain -> contact -> catch-all behaviour, so existing routing is unchanged.
/// </summary>
public class InboxAlias
{
    public Guid Id { get; set; }

    /// <summary>The receiving address, stored lowercased + trimmed for case-insensitive matching.</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>The user every message delivered to <see cref="Address"/> belongs to.</summary>
    public Guid UserId { get; set; }

    /// <summary>Optional scope — if set, this alias only applies to mail polled from that SmtpGroup.
    /// Null means it matches across any group (typical single-mailbox deployment).</summary>
    public Guid? SmtpGroupId { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
    public SmtpGroup? SmtpGroup { get; set; }
}
