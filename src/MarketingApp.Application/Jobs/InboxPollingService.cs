using Hangfire;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Application.Interfaces.Inbox;
using MarketingApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Application.Jobs;

/// <summary>
/// Polls every enabled SmtpGroup mailbox, deduplicates by (SmtpGroupId, ImapFolder, ImapUid),
/// resolves per-user owner, persists InboxMessages, notifies the owner, and enqueues AI processing.
/// </summary>
public class InboxPollingService : IInboxPollingService
{
    private readonly IInboxFetcher _fetcher;
    private readonly IGenericRepository<SmtpGroup> _groupRepo;
    private readonly IGenericRepository<InboxMessage> _inboxRepo;
    private readonly IGenericRepository<CampaignMessage> _msgRepo;
    private readonly IGenericRepository<Contact> _contactRepo;
    private readonly IGenericRepository<Campaign> _campaignRepo;
    private readonly IGenericRepository<OutboundReply> _outboundRepo;
    private readonly IGenericRepository<InboxAlias> _aliasRepo;
    private readonly INotificationService _notifications;
    private readonly IInboxRealtimeNotifier _realtime;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ILogger<InboxPollingService> _logger;

    public InboxPollingService(
        IInboxFetcher fetcher,
        IGenericRepository<SmtpGroup> groupRepo,
        IGenericRepository<InboxMessage> inboxRepo,
        IGenericRepository<CampaignMessage> msgRepo,
        IGenericRepository<Contact> contactRepo,
        IGenericRepository<Campaign> campaignRepo,
        IGenericRepository<OutboundReply> outboundRepo,
        IGenericRepository<InboxAlias> aliasRepo,
        INotificationService notifications,
        IInboxRealtimeNotifier realtime,
        IBackgroundJobClient backgroundJobs,
        ILogger<InboxPollingService> logger)
    {
        _fetcher = fetcher;
        _groupRepo = groupRepo;
        _inboxRepo = inboxRepo;
        _msgRepo = msgRepo;
        _contactRepo = contactRepo;
        _campaignRepo = campaignRepo;
        _outboundRepo = outboundRepo;
        _aliasRepo = aliasRepo;
        _notifications = notifications;
        _realtime = realtime;
        _backgroundJobs = backgroundJobs;
        _logger = logger;
    }

    public async Task PollAllAsync(CancellationToken ct)
    {
        // Visibility into the EnableInboxPolling state — bumped to Information so admins can
        // see in logs whether the job is running but finding no enabled groups.
        var allActive = (await _groupRepo.FindAsync(g => g.IsActive, ct)).ToList();
        var groups = allActive.Where(g => g.EnableInboxPolling).ToList();

        _logger.LogInformation(
            "[InboxPoll] Tick — {Active} active groups, {Enabled} with EnableInboxPolling=true.",
            allActive.Count, groups.Count);

        if (groups.Count == 0)
        {
            _logger.LogWarning("[InboxPoll] No SmtpGroup has EnableInboxPolling=true. Enable it via Admin → SMTP Groups → edit → 'Inbox Polling' section.");
            return;
        }

        foreach (var group in groups)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                _logger.LogInformation("[InboxPoll] Polling group '{GroupName}' (host={Host}, port={Port}, lastUid={Uid}).",
                    group.Name, group.ImapHost ?? group.SmtpHost, group.ImapPort, group.LastImapUid);
                await PollGroupAsync(group, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[InboxPoll] Group {GroupId} ({GroupName}) crashed during poll.", group.Id, group.Name);
            }
        }
    }

    private async Task PollGroupAsync(SmtpGroup group, CancellationToken ct)
    {
        var raws = await _fetcher.FetchSinceAsync(group, group.LastImapUid, ct);
        if (raws.Count == 0) { await TouchPolledAtAsync(group, ct); return; }

        uint maxUid = group.LastImapUid;
        foreach (var raw in raws)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                await PersistOneAsync(group, raw, ct);
                if (raw.ImapUid > maxUid) maxUid = raw.ImapUid;
            }
            catch (DbUpdateException dx) when (IsUniqueViolation(dx))
            {
                // Concurrent run already inserted this (SmtpGroupId, Folder, UID). Safe to skip.
                _logger.LogDebug("[InboxPoll] Duplicate UID {Uid} for group {GroupId} — already persisted.", raw.ImapUid, group.Id);
                if (raw.ImapUid > maxUid) maxUid = raw.ImapUid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[InboxPoll] Failed to persist UID {Uid} for group {GroupId}", raw.ImapUid, group.Id);
            }
        }

        group.LastImapUid = maxUid;
        group.LastInboxPolledAt = DateTime.UtcNow;
        await _groupRepo.UpdateAsync(group, ct);
    }

    private async Task TouchPolledAtAsync(SmtpGroup group, CancellationToken ct)
    {
        group.LastInboxPolledAt = DateTime.UtcNow;
        await _groupRepo.UpdateAsync(group, ct);
    }

    private async Task PersistOneAsync(SmtpGroup group, RawInboxMessage raw, CancellationToken ct)
    {
        // Idempotency by RFC Message-Id. The same physical email can be pulled by MULTIPLE SmtpGroups
        // polling the same mailbox (e.g. two Zoho logins into one shared INBOX) — they share imap_uid +
        // message_id but differ in smtp_group_id, so the (smtp_group_id, folder, uid) unique index does
        // NOT catch them and the message gets stored twice. Message-Id is globally unique per physical
        // email, so a prior row with the same Message-Id means "already ingested" — skip the duplicate.
        if (!string.IsNullOrWhiteSpace(raw.MessageId))
        {
            var alreadyStored = await _inboxRepo.AnyAsync(m => m.MessageId == raw.MessageId, ct);
            if (alreadyStored)
            {
                _logger.LogDebug(
                    "[InboxPoll] Skipping duplicate message {MessageId} (UID {Uid}, group {GroupName}) — already ingested by another group/UID.",
                    raw.MessageId, raw.ImapUid, group.Name);
                return;
            }
        }

        Guid? matchedCampaignMessageId = null;
        Guid? matchedContactId = null;
        Guid? ownerFromChain = null;       // owner resolved via the threading chain (strongest signal)
        Guid? threadIdFromChain = null;    // existing thread this reply belongs to
        Guid ownerUserId;
        var isOrphan = false;

        // Candidate Message-Ids this reply could be answering — In-Reply-To first, then References chain.
        var candidateIds = ExtractCandidateMessageIds(raw.InReplyToMessageId, raw.ReferencesHeader);

        // --- 1. Match against a CampaignMessage we sent (original outbound campaign) ---
        if (candidateIds.Count > 0)
        {
            var campaignMatches = await _msgRepo.FindAsync(
                m => m.SmtpMessageId != null && candidateIds.Contains(m.SmtpMessageId!), ct);
            var cm = campaignMatches.FirstOrDefault();
            if (cm is not null)
            {
                matchedCampaignMessageId = cm.Id;
                var campaign = await _campaignRepo.GetByIdAsync(cm.CampaignId, ct);
                ownerFromChain = campaign?.UserId;
                // Campaign reply seeds a NEW thread (campaign itself isn't in inbox_messages).
            }
        }

        // --- 2. Match against an OutboundReply WE sent from the app (Day 8 chain fix) ---
        //    This is the case that was broken: recipient replies to our app-sent reply.
        if (candidateIds.Count > 0)
        {
            var outboundMatches = await _outboundRepo.FindAsync(
                o => o.SmtpMessageId != null && candidateIds.Contains(o.SmtpMessageId!), ct);
            var ob = outboundMatches.FirstOrDefault();
            if (ob is not null)
            {
                ownerFromChain ??= ob.SentByUserId;
                threadIdFromChain = ob.ThreadId != Guid.Empty ? ob.ThreadId : threadIdFromChain;
            }
        }

        // --- 3. Match against an existing InboxMessage (recipient replied again in same thread) ---
        if (candidateIds.Count > 0)
        {
            var inboxMatches = await _inboxRepo.FindAsync(
                m => m.MessageId != null && candidateIds.Contains(m.MessageId!), ct);
            var im = inboxMatches.FirstOrDefault();
            if (im is not null)
            {
                ownerFromChain ??= im.OwnerUserId;
                if (threadIdFromChain is null && im.ThreadId != Guid.Empty) threadIdFromChain = im.ThreadId;
            }
        }

        // --- 4. Alias lookup by recipient address (shared-mailbox per-user routing) ---
        //    Delivered-To/X-Original-To/Envelope-To is the strongest signal, then To, then Cc.
        Guid? ownerFromAlias = null;
        var recipientCandidates = BuildRecipientCandidates(raw);
        if (recipientCandidates.Count > 0)
        {
            var aliasMatches = await _aliasRepo.FindAsync(
                a => a.IsActive
                     && recipientCandidates.Contains(a.Address.ToLower())
                     && (a.SmtpGroupId == null || a.SmtpGroupId == group.Id), ct);
            // Prefer a group-scoped alias over a global one when both match.
            var alias = aliasMatches
                .OrderByDescending(a => a.SmtpGroupId == group.Id)
                .FirstOrDefault();
            if (alias is not null) ownerFromAlias = alias.UserId;
        }

        // --- 5. Contact lookup by FromEmail (for display + cold-reply owner fallback) ---
        if (!string.IsNullOrWhiteSpace(raw.FromEmail))
        {
            var contacts = (await _contactRepo.FindAsync(
                c => c.Email != null && c.Email.ToLower() == raw.FromEmail.ToLower(), ct)).ToList();
            if (contacts.Count >= 1) matchedContactId = contacts[0].Id;
        }

        // --- 6. Owner resolution: chain wins, then alias, then contact owner, then catch-all ---
        string route;
        if (ownerFromChain.HasValue)
        {
            ownerUserId = ownerFromChain.Value;
            route = "chain";
        }
        else if (ownerFromAlias.HasValue)
        {
            ownerUserId = ownerFromAlias.Value;
            route = "alias";
        }
        else if (matchedContactId.HasValue)
        {
            var c = await _contactRepo.GetByIdAsync(matchedContactId.Value, ct);
            ownerUserId = c?.UserId ?? group.DefaultInboxOwnerUserId ?? group.CreatedByUserId;
            route = c?.UserId != null ? "contact" : "catch-all(contact-missing-owner)";
            isOrphan = c?.UserId == null;
        }
        else
        {
            ownerUserId = group.DefaultInboxOwnerUserId ?? group.CreatedByUserId;
            isOrphan = true;
            route = "catch-all";
        }

        _logger.LogInformation(
            "[InboxPoll] UID {Uid} from {From} (to={To}, delivered-to={Delivered}) routed via {Route} -> owner {Owner}, orphan={Orphan}.",
            raw.ImapUid, raw.FromEmail, raw.ToEmail, raw.DeliveredTo ?? "(none)", route, ownerUserId, isOrphan);

        var normalizedSubject = NormalizeSubject(raw.Subject);

        // --- 6. ThreadId resolution ---
        Guid threadId;
        if (threadIdFromChain.HasValue && threadIdFromChain.Value != Guid.Empty)
        {
            threadId = threadIdFromChain.Value;
        }
        else if (matchedCampaignMessageId.HasValue)
        {
            // First reply to a campaign — try to reuse an existing thread for the same campaign-message
            // (in case multiple replies arrive), else start a new one.
            var existingForCampaign = (await _inboxRepo.FindAsync(
                m => m.MatchedCampaignMessageId == matchedCampaignMessageId && m.ThreadId != Guid.Empty, ct))
                .FirstOrDefault();
            threadId = existingForCampaign?.ThreadId ?? Guid.NewGuid();
        }
        else if (!string.IsNullOrWhiteSpace(normalizedSubject) && !string.IsNullOrWhiteSpace(raw.FromEmail))
        {
            // Subject-based fallback (only when header threading didn't match). MUST also require the
            // SAME participant (from_email) — otherwise two different people replying with the same
            // subject (e.g. a campaign subject) get merged into one thread, which is exactly the
            // "unrelated emails in one thread" bug. Genuine header-linked replies are handled above.
            var fromLower = raw.FromEmail.ToLowerInvariant();
            var cutoff = raw.ReceivedAt.AddDays(-30);
            var subjectMatch = (await _inboxRepo.FindAsync(
                m => m.OwnerUserId == ownerUserId
                     && m.NormalizedSubject == normalizedSubject
                     && m.FromEmail.ToLower() == fromLower
                     && m.ReceivedAt >= cutoff
                     && m.ThreadId != Guid.Empty, ct))
                .OrderByDescending(m => m.ReceivedAt)
                .FirstOrDefault();
            threadId = subjectMatch?.ThreadId ?? Guid.NewGuid();
        }
        else
        {
            threadId = Guid.NewGuid();
        }

        var entity = new InboxMessage
        {
            OwnerUserId = ownerUserId,
            SmtpGroupId = group.Id,
            ImapUid = raw.ImapUid,
            ImapFolder = raw.Folder,
            FromEmail = raw.FromEmail,
            FromName = raw.FromName,
            ToEmail = raw.ToEmail,
            Subject = raw.Subject,
            HtmlBody = raw.HtmlBody,
            TextBody = raw.TextBody,
            ReceivedAt = raw.ReceivedAt,
            MessageId = raw.MessageId,
            InReplyToMessageId = raw.InReplyToMessageId,
            ReferencesHeader = raw.ReferencesHeader,
            ThreadId = threadId,
            NormalizedSubject = normalizedSubject,
            MatchedCampaignMessageId = matchedCampaignMessageId,
            MatchedContactId = matchedContactId,
            IsOrphanReply = isOrphan,
        };

        await _inboxRepo.AddAsync(entity, ct);

        // Notify ONLY the owner — keeps per-user isolation in the bell.
        try
        {
            await _notifications.CreateNotificationAsync(
                ownerUserId,
                $"New reply from {raw.FromName ?? raw.FromEmail}",
                Truncate(raw.Subject, 200),
                "info",
                "InboxMessage",
                entity.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[InboxPoll] Failed to create notification for inbox message {Id}", entity.Id);
        }

        // Real-time push to the owner's open clients (best-effort).
        try { await _realtime.NotifyNewMessageAsync(ownerUserId, threadId, entity.Id); }
        catch (Exception ex) { _logger.LogWarning(ex, "[InboxPoll] Realtime notify failed for {Id}", entity.Id); }

        // Enqueue AI processing.
        try
        {
            _backgroundJobs.Enqueue<Interfaces.AI.IAiReplyService>(s => s.ProcessAsync(entity.Id, default));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[InboxPoll] Failed to enqueue AI processing for inbox message {Id}", entity.Id);
        }
    }

    /// <summary>All lowercased recipient addresses an alias could match — delivery headers + To + Cc.</summary>
    private static HashSet<string> BuildRecipientCandidates(RawInboxMessage raw)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string? a) { if (!string.IsNullOrWhiteSpace(a)) set.Add(a.Trim().ToLowerInvariant()); }
        Add(raw.DeliveredTo);
        Add(raw.ToEmail);
        foreach (var a in raw.ToEmails) Add(a);
        foreach (var a in raw.CcEmails) Add(a);
        return set;
    }

    /// <summary>Extract all candidate Message-Ids (bracketed form) from In-Reply-To + References headers.</summary>
    private static HashSet<string> ExtractCandidateMessageIds(string? inReplyTo, string? references)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            id = id.Trim();
            set.Add(id.StartsWith('<') ? id : $"<{id}>");
            set.Add(id.TrimStart('<').TrimEnd('>'));   // also the bracket-less form
        }
        Add(inReplyTo);
        if (!string.IsNullOrWhiteSpace(references))
            foreach (var token in references.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                Add(token);
        return set;
    }

    /// <summary>Strip Re:/Fwd:/Fw: prefixes (repeatedly) + lowercase + trim, for fallback thread grouping.</summary>
    private static string NormalizeSubject(string? subject)
    {
        if (string.IsNullOrWhiteSpace(subject)) return string.Empty;
        var s = subject.Trim();
        var prev = "";
        while (prev != s)
        {
            prev = s;
            s = System.Text.RegularExpressions.Regex.Replace(s, "^\\s*(re|fwd|fw)\\s*:\\s*", "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        return s.Trim().ToLowerInvariant();
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException?.Message?.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message?.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) == true;

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max];
}
