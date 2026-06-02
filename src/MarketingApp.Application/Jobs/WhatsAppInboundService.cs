using System.Text.Json;
using Hangfire;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Application.Interfaces.Inbox;
using MarketingApp.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Application.Jobs;

/// <summary>
/// Turns Meta WhatsApp Cloud API inbound-message webhooks into InboxMessage rows in the unified inbox (L3).
/// Mirrors InboxPollingService's persist flow (dedup → save → notify → enqueue AI) but the source is a
/// pushed webhook payload instead of IMAP polling.
/// </summary>
public class WhatsAppInboundService : IWhatsAppInboundService
{
    private readonly IGenericRepository<SmtpGroup> _groupRepo;
    private readonly IGenericRepository<InboxMessage> _inboxRepo;
    private readonly IGenericRepository<Contact> _contactRepo;
    private readonly INotificationService _notifications;
    private readonly IInboxRealtimeNotifier _realtime;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ILogger<WhatsAppInboundService> _logger;

    public WhatsAppInboundService(
        IGenericRepository<SmtpGroup> groupRepo,
        IGenericRepository<InboxMessage> inboxRepo,
        IGenericRepository<Contact> contactRepo,
        INotificationService notifications,
        IInboxRealtimeNotifier realtime,
        IBackgroundJobClient backgroundJobs,
        ILogger<WhatsAppInboundService> logger)
    {
        _groupRepo = groupRepo;
        _inboxRepo = inboxRepo;
        _contactRepo = contactRepo;
        _notifications = notifications;
        _realtime = realtime;
        _backgroundJobs = backgroundJobs;
        _logger = logger;
    }

    public async Task<int> IngestAsync(string rawJson, CancellationToken ct = default)
    {
        var messages = ParseInbound(rawJson);
        if (messages.Count == 0) return 0;

        var ingested = 0;
        foreach (var m in messages)
        {
            try
            {
                if (await IngestOneAsync(m, ct)) ingested++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WA INBOUND] Failed to ingest message {MsgId} from {From}", m.MessageId, m.From);
            }
        }
        return ingested;
    }

    private async Task<bool> IngestOneAsync(WhatsAppInboundMessage m, CancellationToken ct)
    {
        // Route to the SmtpGroup that owns the receiving business phone number.
        var group = (await _groupRepo.FindAsync(g => g.WhatsAppPhoneNumberId == m.PhoneNumberId, ct)).FirstOrDefault();
        if (group is null)
        {
            _logger.LogWarning("[WA INBOUND] No SmtpGroup for phone_number_id {PhoneId} — dropping message {MsgId}", m.PhoneNumberId, m.MessageId);
            return false;
        }

        var ownerUserId = group.DefaultInboxOwnerUserId ?? group.CreatedByUserId;

        // Dedup on Meta's globally-unique message id.
        if (!string.IsNullOrEmpty(m.MessageId))
        {
            var exists = await _inboxRepo.AnyAsync(x => x.Channel == "whatsapp" && x.MessageId == m.MessageId, ct);
            if (exists)
            {
                _logger.LogDebug("[WA INBOUND] Duplicate message {MsgId} — skipping", m.MessageId);
                return false;
            }
        }

        // Best-effort contact match by WhatsApp number / phone (digits compare).
        var fromDigits = DigitsOnly(m.From);
        var contact = (await _contactRepo.FindAsync(c =>
            c.IsActive && (c.WhatsAppNumber != null || c.Phone != null), ct))
            .FirstOrDefault(c => DigitsOnly(c.WhatsAppNumber ?? "") == fromDigits
                              || DigitsOnly(c.Phone ?? "") == fromDigits);

        // Thread by sender within this owner's WhatsApp inbox: reuse an existing thread if we've
        // heard from this number before, otherwise start a new one.
        var prior = (await _inboxRepo.FindAsync(x =>
            x.Channel == "whatsapp" && x.OwnerUserId == ownerUserId && x.FromEmail == m.From, ct))
            .OrderByDescending(x => x.ReceivedAt).FirstOrDefault();
        var threadId = prior?.ThreadId ?? Guid.NewGuid();

        var entity = new InboxMessage
        {
            OwnerUserId = ownerUserId,
            SmtpGroupId = group.Id,
            Channel = "whatsapp",
            ImapUid = 0,
            ImapFolder = "WHATSAPP",
            FromEmail = m.From,                       // store the sender's WhatsApp number here
            FromName = m.FromName ?? contact?.FullName,
            ToEmail = group.WhatsAppPhoneNumberId ?? "whatsapp",
            Subject = "WhatsApp message",
            TextBody = m.Text,
            HtmlBody = null,
            ReceivedAt = m.ReceivedAt,
            MessageId = m.MessageId,
            ThreadId = threadId,
            NormalizedSubject = "whatsapp",
            MatchedContactId = contact?.Id,
            IsOrphanReply = contact is null,
        };

        await _inboxRepo.AddAsync(entity, ct);

        try
        {
            await _notifications.CreateNotificationAsync(
                ownerUserId,
                "New WhatsApp message",
                $"From {entity.FromName ?? m.From}: {Truncate(m.Text, 80)}",
                "info", "inbox", entity.Id);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "[WA INBOUND] notification failed for {Id}", entity.Id); }

        try { await _realtime.NotifyNewMessageAsync(ownerUserId, threadId, entity.Id); }
        catch (Exception ex) { _logger.LogWarning(ex, "[WA INBOUND] realtime push failed for {Id}", entity.Id); }

        try { _backgroundJobs.Enqueue<Interfaces.AI.IAiReplyService>(s => s.ProcessAsync(entity.Id, default)); }
        catch (Exception ex) { _logger.LogWarning(ex, "[WA INBOUND] AI enqueue failed for {Id}", entity.Id); }

        _logger.LogInformation("[WA INBOUND] Ingested message {MsgId} from {From} → owner {Owner}", m.MessageId, m.From, ownerUserId);
        return true;
    }

    private static string DigitsOnly(string s) => new string(s.Where(char.IsDigit).ToArray());

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "…";

    /// <summary>
    /// Parses a Meta WhatsApp Cloud API webhook payload into inbound messages. Pure + testable.
    /// Walks entry[].changes[].value.messages[], pulling the receiving phone_number_id + contact
    /// profile name. Non-text messages yield a short "[image]"-style placeholder body.
    /// </summary>
    internal static List<WhatsAppInboundMessage> ParseInbound(string json)
    {
        var result = new List<WhatsAppInboundMessage>();
        if (string.IsNullOrWhiteSpace(json)) return result;

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); } catch { return result; }
        using (doc)
        {
            var root = doc.RootElement;
            if (!root.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var change in changes.EnumerateArray())
                {
                    if (!change.TryGetProperty("value", out var value)) continue;

                    var phoneNumberId = "";
                    if (value.TryGetProperty("metadata", out var meta))
                        phoneNumberId = GetStr(meta, "phone_number_id") ?? "";

                    // Map wa_id → profile name from the contacts[] array.
                    var names = new Dictionary<string, string>();
                    if (value.TryGetProperty("contacts", out var contacts) && contacts.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var c in contacts.EnumerateArray())
                        {
                            var waId = GetStr(c, "wa_id");
                            var name = c.TryGetProperty("profile", out var prof) ? GetStr(prof, "name") : null;
                            if (!string.IsNullOrEmpty(waId) && !string.IsNullOrEmpty(name)) names[waId!] = name!;
                        }
                    }

                    if (!value.TryGetProperty("messages", out var msgs) || msgs.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var msg in msgs.EnumerateArray())
                    {
                        var from = GetStr(msg, "from") ?? "";
                        var id = GetStr(msg, "id") ?? "";
                        var type = (GetStr(msg, "type") ?? "text").ToLowerInvariant();

                        var ts = DateTime.UtcNow;
                        if (msg.TryGetProperty("timestamp", out var tsEl)
                            && long.TryParse(tsEl.ValueKind == JsonValueKind.String ? tsEl.GetString() : tsEl.GetRawText(), out var unix))
                            ts = DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;

                        var text = type switch
                        {
                            "text" => msg.TryGetProperty("text", out var t) ? (GetStr(t, "body") ?? "") : "",
                            "image" => "[image]",
                            "document" => "[document]",
                            "video" => "[video]",
                            "audio" => "[voice message]",
                            "location" => "[location]",
                            "button" => msg.TryGetProperty("button", out var b) ? (GetStr(b, "text") ?? "[button]") : "[button]",
                            "interactive" => "[interactive reply]",
                            _ => $"[{type}]",
                        };

                        result.Add(new WhatsAppInboundMessage
                        {
                            PhoneNumberId = phoneNumberId,
                            From = from,
                            FromName = names.TryGetValue(from, out var n) ? n : null,
                            MessageId = id,
                            Text = text,
                            Type = type,
                            ReceivedAt = ts,
                        });
                    }
                }
            }
        }
        return result;
    }

    private static string? GetStr(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
