using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces.Inbox;
using MarketingApp.Domain.Entities;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace MarketingApp.Infrastructure.Services.Inbox;

/// <summary>
/// IMAP fetcher built on MailKit (Day 7 G3). One per-poll connection per SmtpGroup — short-lived,
/// auto-disposed. We DO NOT mutate server state (no message flags written) so the same mailbox
/// stays usable in Gmail / Outlook / Hostinger UIs.
/// </summary>
public class MailKitImapInboxFetcher : IInboxFetcher
{
    private readonly ILogger<MailKitImapInboxFetcher> _logger;

    public MailKitImapInboxFetcher(ILogger<MailKitImapInboxFetcher> logger) { _logger = logger; }

    public async Task<IReadOnlyList<RawInboxMessage>> FetchSinceAsync(SmtpGroup group, uint lastUid, CancellationToken ct)
    {
        // Resolve credentials with sensible fallbacks to SMTP creds.
        // For host fallback: SMTP and IMAP hostnames almost always differ by prefix only
        // (smtp.zoho.com vs imap.zoho.com, smtp.gmail.com vs imap.gmail.com, etc.).
        // If admin left ImapHost blank, derive it from SmtpHost by swapping the prefix.
        var host = !string.IsNullOrWhiteSpace(group.ImapHost) ? group.ImapHost : DeriveImapHostFromSmtp(group.SmtpHost);
        var username = group.ImapUsername ?? group.SmtpUsername;
        var password = group.ImapPassword ?? group.SmtpPassword;

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("[IMAP] Group {GroupId} ({GroupName}) missing IMAP credentials — skipping poll.", group.Id, group.Name);
            return Array.Empty<RawInboxMessage>();
        }

        var folderName = string.IsNullOrWhiteSpace(group.ImapFolder) ? "INBOX" : group.ImapFolder;
        var results = new List<RawInboxMessage>();

        using var client = new ImapClient();
        try
        {
            await client.ConnectAsync(host, group.ImapPort, group.ImapEnableSsl, ct);
            await client.AuthenticateAsync(username, password, ct);

            var folder = await client.GetFolderAsync(folderName, ct);
            await folder.OpenAsync(FolderAccess.ReadOnly, ct);

            // SearchQuery.Uids requires a UniqueId range. UIDs strictly greater than lastUid:
            var lower = new UniqueId(lastUid + 1);
            var query = SearchQuery.Uids(new UniqueIdRange(lower, UniqueId.MaxValue));
            var uids = await folder.SearchAsync(query, ct);

            // Cap per-poll batch size so a backlog doesn't lock the worker for too long.
            const int MaxPerPoll = 50;
            var batch = uids.Take(MaxPerPoll).ToList();

            foreach (var uid in batch)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var msg = await folder.GetMessageAsync(uid, ct);
                    results.Add(MapToRaw(uid, folderName, msg));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[IMAP] Failed to fetch UID {Uid} from group {GroupId} — skipping.", uid.Id, group.Id);
                }
            }

            await client.DisconnectAsync(true, ct);
            _logger.LogInformation("[IMAP] Group {GroupName} polled — fetched {Count} new messages (lastUid was {LastUid}).",
                group.Name, results.Count, lastUid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[IMAP] Poll failed for group {GroupId} ({GroupName}).", group.Id, group.Name);
        }
        return results;
    }

    /// <summary>
    /// Derive an IMAP hostname from an SMTP hostname when the admin didn't set one explicitly.
    /// Almost every email provider follows the same convention:
    ///   smtp.zoho.com   -> imap.zoho.com
    ///   smtp.gmail.com  -> imap.gmail.com
    ///   smtp.office365.com -> outlook.office365.com (special case)
    ///   smtp.hostinger.com -> imap.hostinger.com
    ///   mail.example.com -> mail.example.com (no prefix to swap — try as-is)
    /// </summary>
    private static string? DeriveImapHostFromSmtp(string? smtpHost)
    {
        if (string.IsNullOrWhiteSpace(smtpHost)) return null;

        // Office365 special case.
        if (smtpHost.Contains("office365.com", StringComparison.OrdinalIgnoreCase))
            return "outlook.office365.com";

        // Standard "smtp." -> "imap." swap.
        if (smtpHost.StartsWith("smtp.", StringComparison.OrdinalIgnoreCase))
            return "imap." + smtpHost.Substring(5);

        // "smtp-mail.outlook.com" -> "outlook.office365.com"
        if (smtpHost.StartsWith("smtp-mail.outlook", StringComparison.OrdinalIgnoreCase))
            return "outlook.office365.com";

        // Fall through — the admin's SMTP host might already work for IMAP (shared mail server).
        return smtpHost;
    }

    /// <summary>
    /// Read a delivery-style header (Delivered-To / X-Original-To / Envelope-To) and pull a bare,
    /// lowercased email address out of it. These headers may be "&lt;ali@x.com&gt;" or "ali@x.com".
    /// Returns null when the header is absent or has no parseable address.
    /// </summary>
    private static string? FirstHeaderAddress(MimeMessage msg, string headerName)
    {
        var raw = msg.Headers[headerName];
        if (string.IsNullOrWhiteSpace(raw)) return null;

        // Prefer MailKit's tolerant parser; fall back to a trimmed raw value.
        if (MailboxAddress.TryParse(raw, out var parsed) && !string.IsNullOrWhiteSpace(parsed.Address))
            return parsed.Address.Trim().ToLowerInvariant();

        return raw.Trim().TrimStart('<').TrimEnd('>').ToLowerInvariant();
    }

    private static RawInboxMessage MapToRaw(UniqueId uid, string folder, MimeMessage msg)
    {
        var from = msg.From?.Mailboxes.FirstOrDefault();
        var to = msg.To?.Mailboxes.FirstOrDefault();

        var toEmails = (msg.To?.Mailboxes ?? Enumerable.Empty<MailboxAddress>())
            .Select(m => m.Address?.Trim().ToLowerInvariant())
            .Where(a => !string.IsNullOrEmpty(a))
            .Select(a => a!)
            .Distinct()
            .ToList();
        var ccEmails = (msg.Cc?.Mailboxes ?? Enumerable.Empty<MailboxAddress>())
            .Select(m => m.Address?.Trim().ToLowerInvariant())
            .Where(a => !string.IsNullOrEmpty(a))
            .Select(a => a!)
            .Distinct()
            .ToList();

        // The real recipient in a shared mailbox is usually on a delivery header, not To/Cc.
        var deliveredTo = FirstHeaderAddress(msg, "Delivered-To")
            ?? FirstHeaderAddress(msg, "X-Original-To")
            ?? FirstHeaderAddress(msg, "Envelope-To");

        return new RawInboxMessage
        {
            ImapUid = uid.Id,
            Folder = folder,
            FromEmail = from?.Address ?? "",
            FromName = from?.Name,
            ToEmail = to?.Address ?? "",
            ToEmails = toEmails,
            CcEmails = ccEmails,
            DeliveredTo = deliveredTo,
            Subject = msg.Subject ?? "(no subject)",
            HtmlBody = msg.HtmlBody,
            TextBody = msg.TextBody,
            ReceivedAt = msg.Date.UtcDateTime,
            // MimeKit exposes the message's own Message-Id WITHOUT angle brackets. Re-add them so it
            // matches the "<id@domain>" format we store on outbound replies + campaign messages.
            MessageId = string.IsNullOrEmpty(msg.MessageId) ? null
                : (msg.MessageId.StartsWith('<') ? msg.MessageId : $"<{msg.MessageId}>"),
            InReplyToMessageId = msg.InReplyTo,
            ReferencesHeader = msg.References != null && msg.References.Count > 0
                ? string.Join(" ", msg.References.Select(r => $"<{r}>"))
                : null,
        };
    }
}
