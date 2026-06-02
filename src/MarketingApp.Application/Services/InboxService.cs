using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Application.Interfaces.Inbox;
using MarketingApp.Domain.Entities;
using MarketingApp.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Application.Services;

/// <summary>
/// Per-user scoped inbox operations (Day 7 G4).
/// Auth model: every read/write enforces `msg.OwnerUserId == requesterUserId` unless requester is admin
/// AND viewAll is explicitly true. No silent cross-user leakage.
/// </summary>
public class InboxService : IInboxService
{
    private readonly IGenericRepository<InboxMessage> _inboxRepo;
    private readonly IGenericRepository<OutboundReply> _outboundRepo;
    private readonly IGenericRepository<Campaign> _campaignRepo;
    private readonly IGenericRepository<CampaignMessage> _messageRepo;
    private readonly IGenericRepository<Contact> _contactRepo;
    private readonly IGenericRepository<SmtpGroup> _groupRepo;
    private readonly ISmtpGroupService _smtpGroups;
    private readonly IEmailService _emailService;
    private readonly IAuditService _audit;
    private readonly ILogger<InboxService> _logger;

    public InboxService(
        IGenericRepository<InboxMessage> inboxRepo,
        IGenericRepository<OutboundReply> outboundRepo,
        IGenericRepository<Campaign> campaignRepo,
        IGenericRepository<CampaignMessage> messageRepo,
        IGenericRepository<Contact> contactRepo,
        IGenericRepository<SmtpGroup> groupRepo,
        ISmtpGroupService smtpGroups,
        IEmailService emailService,
        IAuditService audit,
        ILogger<InboxService> logger)
    {
        _inboxRepo = inboxRepo;
        _outboundRepo = outboundRepo;
        _campaignRepo = campaignRepo;
        _messageRepo = messageRepo;
        _contactRepo = contactRepo;
        _groupRepo = groupRepo;
        _smtpGroups = smtpGroups;
        _emailService = emailService;
        _audit = audit;
        _logger = logger;
    }

    public async Task<PagedResponse<InboxMessageListItemDto>> GetAllAsync(
        Guid requesterUserId, bool requesterIsAdmin, bool viewAll,
        int pageNumber, int pageSize,
        bool? unreadOnly, string? category, Guid? smtpGroupId, string? search,
        CancellationToken ct)
    {
        var pn = Math.Max(1, pageNumber);
        var ps = Math.Clamp(pageSize, 1, 100);

        // Pull a generous superset and filter in-memory. For now we don't have direct IQueryable<InboxMessage>
        // exposed via the generic repository, so we use FindAsync with a predicate.
        var all = await _inboxRepo.FindAsync(m =>
            (requesterIsAdmin && viewAll || m.OwnerUserId == requesterUserId) &&
            !m.IsArchived, ct);
        var filtered = all.AsQueryable();

        if (unreadOnly == true) filtered = filtered.Where(m => !m.IsRead);
        if (!string.IsNullOrWhiteSpace(category)) filtered = filtered.Where(m => m.AiCategory == category);
        if (smtpGroupId.HasValue) filtered = filtered.Where(m => m.SmtpGroupId == smtpGroupId.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLowerInvariant();
            filtered = filtered.Where(m =>
                m.Subject.ToLower().Contains(s)
                || m.FromEmail.ToLower().Contains(s)
                || (m.FromName != null && m.FromName.ToLower().Contains(s))
                || (m.AiSummary != null && m.AiSummary.ToLower().Contains(s)));
        }

        var ordered = filtered.OrderByDescending(m => m.ReceivedAt).ToList();
        var totalCount = ordered.Count;
        var page = ordered.Skip((pn - 1) * ps).Take(ps).ToList();

        // Load matched campaign / group names for the page (single-shot lookups)
        var campaignMessageIds = page.Where(p => p.MatchedCampaignMessageId.HasValue)
                                     .Select(p => p.MatchedCampaignMessageId!.Value).Distinct().ToList();
        var campaignMessages = (await _messageRepo.FindAsync(
            m => campaignMessageIds.Contains(m.Id), ct)).ToDictionary(m => m.Id, m => m);
        var campaignIds = campaignMessages.Values.Select(m => m.CampaignId).Distinct().ToList();
        var campaigns = (await _campaignRepo.FindAsync(
            c => campaignIds.Contains(c.Id), ct)).ToDictionary(c => c.Id, c => c);

        var groupIds = page.Select(p => p.SmtpGroupId).Distinct().ToList();
        var groups = (await _groupRepo.FindAsync(g => groupIds.Contains(g.Id), ct)).ToDictionary(g => g.Id, g => g);

        var items = page.Select(m => new InboxMessageListItemDto
        {
            Id = m.Id,
            Channel = m.Channel,
            FromEmail = m.FromEmail,
            FromName = m.FromName,
            Subject = m.Subject,
            Preview = BuildPreview(m),
            ReceivedAt = m.ReceivedAt,
            IsRead = m.IsRead,
            IsArchived = m.IsArchived,
            IsOrphanReply = m.IsOrphanReply,
            AiCategory = m.AiCategory,
            AiSummary = m.AiSummary,
            HasAiSuggestion = !string.IsNullOrEmpty(m.AiSuggestedReply),
            RepliedAt = m.RepliedAt,
            MatchedCampaignId = m.MatchedCampaignMessageId.HasValue && campaignMessages.TryGetValue(m.MatchedCampaignMessageId.Value, out var cm)
                ? cm.CampaignId : null,
            MatchedCampaignName = m.MatchedCampaignMessageId.HasValue && campaignMessages.TryGetValue(m.MatchedCampaignMessageId.Value, out var cm2)
                && campaigns.TryGetValue(cm2.CampaignId, out var camp) ? camp.Name : null,
            SmtpGroupName = groups.TryGetValue(m.SmtpGroupId, out var grp) ? grp.Name : null,
        }).ToList();

        return new PagedResponse<InboxMessageListItemDto>
        {
            Data = items,
            PageNumber = pn,
            PageSize = ps,
            TotalCount = totalCount,
        };
    }

    public async Task<InboxMessageDetailDto> GetByIdAsync(Guid id, Guid requesterUserId, bool requesterIsAdmin, CancellationToken ct)
    {
        var m = await _inboxRepo.GetByIdAsync(id, ct) ?? throw new NotFoundException("InboxMessage", id);
        EnsureAccess(m, requesterUserId, requesterIsAdmin);

        Campaign? campaign = null;
        Contact? matchedContact = null;
        SmtpGroup? group = null;
        if (m.MatchedCampaignMessageId.HasValue)
        {
            var cm = await _messageRepo.GetByIdAsync(m.MatchedCampaignMessageId.Value, ct);
            if (cm is not null) campaign = await _campaignRepo.GetByIdAsync(cm.CampaignId, ct);
        }
        if (m.MatchedContactId.HasValue)
            matchedContact = await _contactRepo.GetByIdAsync(m.MatchedContactId.Value, ct);
        group = await _groupRepo.GetByIdAsync(m.SmtpGroupId, ct);

        return new InboxMessageDetailDto
        {
            Id = m.Id,
            OwnerUserId = m.OwnerUserId,
            FromEmail = m.FromEmail,
            FromName = m.FromName,
            ToEmail = m.ToEmail,
            Subject = m.Subject,
            HtmlBody = m.HtmlBody,
            TextBody = m.TextBody,
            Preview = BuildPreview(m),
            ReceivedAt = m.ReceivedAt,
            IsRead = m.IsRead,
            IsArchived = m.IsArchived,
            IsOrphanReply = m.IsOrphanReply,
            InReplyToMessageId = m.InReplyToMessageId,
            ReferencesHeader = m.ReferencesHeader,
            MatchedCampaignMessageId = m.MatchedCampaignMessageId,
            MatchedCampaignId = campaign?.Id,
            MatchedCampaignName = campaign?.Name,
            MatchedContactId = m.MatchedContactId,
            MatchedContactName = matchedContact?.FullName,
            SmtpGroupName = group?.Name,
            AiCategory = m.AiCategory,
            AiSummary = m.AiSummary,
            AiSuggestedReply = m.AiSuggestedReply,
            UserEditedReply = m.UserEditedReply,
            HasAiSuggestion = !string.IsNullOrEmpty(m.AiSuggestedReply),
            AiGeneratedAt = m.AiGeneratedAt,
            DraftSavedAt = m.DraftSavedAt,
            AiGenerationError = m.AiGenerationError,
            AiProviderUsed = m.AiProviderUsed,
            AiInputTokens = m.AiInputTokens,
            AiOutputTokens = m.AiOutputTokens,
            RepliedAt = m.RepliedAt,
        };
    }

    public async Task<InboxUnreadCountDto> GetUnreadCountAsync(Guid requesterUserId, CancellationToken ct)
    {
        var all = (await _inboxRepo.FindAsync(
            m => m.OwnerUserId == requesterUserId && !m.IsArchived && !m.IsRead, ct)).ToList();
        return new InboxUnreadCountDto
        {
            TotalUnread = all.Count,
            OrphanCount = all.Count(m => m.IsOrphanReply),
        };
    }

    public async Task MarkReadAsync(Guid id, Guid requesterUserId, bool requesterIsAdmin, CancellationToken ct)
    {
        var m = await _inboxRepo.GetByIdAsync(id, ct) ?? throw new NotFoundException("InboxMessage", id);
        EnsureAccess(m, requesterUserId, requesterIsAdmin);
        if (m.IsRead) return;
        m.IsRead = true;
        m.UpdatedAt = DateTime.UtcNow;
        await _inboxRepo.UpdateAsync(m, ct);
    }

    public async Task MarkAllReadAsync(Guid requesterUserId, CancellationToken ct)
    {
        var msgs = (await _inboxRepo.FindAsync(m => m.OwnerUserId == requesterUserId && !m.IsRead, ct)).ToList();
        foreach (var m in msgs) { m.IsRead = true; m.UpdatedAt = DateTime.UtcNow; await _inboxRepo.UpdateAsync(m, ct); }
    }

    public async Task ArchiveAsync(Guid id, Guid requesterUserId, bool requesterIsAdmin, CancellationToken ct)
    {
        var m = await _inboxRepo.GetByIdAsync(id, ct) ?? throw new NotFoundException("InboxMessage", id);
        EnsureAccess(m, requesterUserId, requesterIsAdmin);
        m.IsArchived = true;
        m.UpdatedAt = DateTime.UtcNow;
        await _inboxRepo.UpdateAsync(m, ct);
        await _audit.LogAsync(requesterUserId, "InboxArchived", "InboxMessage", id, ct: ct);
    }

    public async Task SaveDraftAsync(Guid id, string html, Guid requesterUserId, bool requesterIsAdmin, CancellationToken ct)
    {
        var m = await _inboxRepo.GetByIdAsync(id, ct) ?? throw new NotFoundException("InboxMessage", id);
        EnsureAccess(m, requesterUserId, requesterIsAdmin);
        m.UserEditedReply = html;
        m.DraftSavedAt = DateTime.UtcNow;
        m.UpdatedAt = DateTime.UtcNow;
        await _inboxRepo.UpdateAsync(m, ct);
    }

    public async Task SendReplyAsync(Guid id, SendReplyDto dto, Guid requesterUserId, bool requesterIsAdmin, CancellationToken ct)
    {
        var m = await _inboxRepo.GetByIdAsync(id, ct) ?? throw new NotFoundException("InboxMessage", id);
        EnsureAccess(m, requesterUserId, requesterIsAdmin);

        if (string.IsNullOrWhiteSpace(dto.Subject)) throw new AppValidationException(new List<string> { "Subject is required." });
        if (string.IsNullOrWhiteSpace(dto.Html)) throw new AppValidationException(new List<string> { "Reply body is required." });

        // Resolve sender's SmtpGroup so we send via the same provider creds the original campaign used.
        var group = await _smtpGroups.ResolveForUserAsync(m.OwnerUserId, ct);
        if (group is null) throw new AppValidationException(new List<string> { "Owner has no SmtpGroup configured." });

        var smtpSettings = MarketingApp.Application.Services.SmtpGroupService.ToUserSmtpSettings(group);

        var wasEdited = !string.Equals(m.AiSuggestedReply ?? "", dto.Html, StringComparison.Ordinal);

        // === Day 8 chain fix ===
        // Generate OUR reply's Message-Id and set it on the outgoing email. When the recipient replies
        // back, their In-Reply-To will be THIS id — letting the poller thread it correctly (instead of
        // becoming an orphan, which was the root-cause bug).
        var fromAddr = smtpSettings.SmtpFromEmail ?? smtpSettings.SmtpUsername ?? "noreply@localhost";
        var fromDomain = fromAddr.Contains('@') ? fromAddr[(fromAddr.IndexOf('@') + 1)..] : "localhost";
        var replyMessageId = $"<reply-{Guid.NewGuid():N}@{fromDomain}>";

        // Thread the email: In-Reply-To = the message we're answering; References = full chain.
        var inReplyToTarget = m.MessageId ?? m.InReplyToMessageId;
        var headers = new EmailHeaders
        {
            MessageId = replyMessageId,
            InReplyTo = inReplyToTarget,
            References = string.IsNullOrEmpty(m.ReferencesHeader)
                ? inReplyToTarget
                : m.ReferencesHeader + (string.IsNullOrEmpty(inReplyToTarget) ? "" : " " + inReplyToTarget),
        };

        bool success = false;
        string? errorMessage = null;
        try
        {
            success = await _emailService.SendWithUserSettingsAsync(
                m.FromEmail, dto.Subject, dto.Html, smtpSettings, headers, ct);
        }
        catch (Exception ex)
        {
            success = false;
            errorMessage = ex.Message;
            _logger.LogError(ex, "[InboxReply] Failed to send reply for inbox message {Id}", id);
        }

        await _outboundRepo.AddAsync(new OutboundReply
        {
            InboxMessageId = id,
            SentByUserId = requesterUserId,
            Subject = dto.Subject,
            WasEdited = wasEdited,
            SendSucceeded = success,
            ErrorMessage = Truncate(errorMessage, 2000),
            // Day 8: persist Message-Id + full body + thread linkage so the conversation is complete
            // and the recipient's reply-to-our-reply can be threaded.
            SmtpMessageId = replyMessageId,
            BodyHtml = dto.Html,
            InReplyToMessageId = inReplyToTarget,
            ThreadId = m.ThreadId,
        }, ct);

        if (success)
        {
            m.RepliedAt = DateTime.UtcNow;
            m.RepliedByUserId = requesterUserId;
            m.ReplyWasEdited = wasEdited;
            m.UserEditedReply = dto.Html;
            m.IsRead = true;
            m.UpdatedAt = DateTime.UtcNow;
            await _inboxRepo.UpdateAsync(m, ct);

            await _audit.LogAsync(requesterUserId, "InboxReplySent", "InboxMessage", id,
                details: new { wasEdited, to = m.FromEmail }, ct: ct);
        }
        else
        {
            throw new InvalidOperationException(errorMessage ?? "Reply send failed.");
        }
    }

    // ===================== Day 8: Conversation (thread) views =====================

    public async Task<PagedResponse<InboxThreadListItemDto>> GetThreadsAsync(
        Guid requesterUserId, bool requesterIsAdmin, bool viewAll,
        int pageNumber, int pageSize,
        bool? unreadOnly, string? category, string? search,
        CancellationToken ct)
    {
        var pn = Math.Max(1, pageNumber);
        var ps = Math.Clamp(pageSize, 1, 100);

        var all = (await _inboxRepo.FindAsync(m =>
            (requesterIsAdmin && viewAll || m.OwnerUserId == requesterUserId) && !m.IsArchived, ct)).ToList();

        // Group into conversations by ThreadId.
        var groups = all.GroupBy(m => m.ThreadId).ToList();

        // Resolve campaign + group names once for the whole set.
        var campaignMessageIds = all.Where(m => m.MatchedCampaignMessageId.HasValue)
            .Select(m => m.MatchedCampaignMessageId!.Value).Distinct().ToList();
        var campaignMessages = (await _messageRepo.FindAsync(m => campaignMessageIds.Contains(m.Id), ct))
            .ToDictionary(m => m.Id, m => m);
        var campaignIds = campaignMessages.Values.Select(m => m.CampaignId).Distinct().ToList();
        var campaigns = (await _campaignRepo.FindAsync(c => campaignIds.Contains(c.Id), ct))
            .ToDictionary(c => c.Id, c => c);
        var groupIds = all.Select(m => m.SmtpGroupId).Distinct().ToList();
        var smtpGroups = (await _groupRepo.FindAsync(g => groupIds.Contains(g.Id), ct))
            .ToDictionary(g => g.Id, g => g);

        var threadItems = groups.Select(g =>
        {
            var msgs = g.OrderBy(m => m.ReceivedAt).ToList();
            var latest = msgs.Last();
            var latestInbound = msgs.LastOrDefault(); // all inbox_messages are inbound
            var campaignMsg = latest.MatchedCampaignMessageId.HasValue
                && campaignMessages.TryGetValue(latest.MatchedCampaignMessageId.Value, out var cm) ? cm : null;
            var campaign = campaignMsg != null && campaigns.TryGetValue(campaignMsg.CampaignId, out var cp) ? cp : null;

            return new InboxThreadListItemDto
            {
                ThreadId = g.Key,
                Channel = latest.Channel,
                ParticipantEmail = latest.FromEmail,
                ParticipantName = latest.FromName,
                Subject = msgs.First().Subject,
                LatestPreview = BuildPreview(latest),
                LastActivityAt = latest.ReceivedAt,
                MessageCount = msgs.Count,
                UnreadCount = msgs.Count(m => !m.IsRead),
                AiCategory = latestInbound?.AiCategory,
                HasAiSuggestion = !string.IsNullOrEmpty(latestInbound?.AiSuggestedReply),
                MatchedCampaignId = campaign?.Id,
                MatchedCampaignName = campaign?.Name,
                SmtpGroupName = smtpGroups.TryGetValue(latest.SmtpGroupId, out var sg) ? sg.Name : null,
                IsOrphan = msgs.All(m => m.IsOrphanReply),
                LatestInboundMessageId = latestInbound?.Id,
            };
        }).AsEnumerable();

        // Filters operate at the thread level.
        if (unreadOnly == true) threadItems = threadItems.Where(t => t.UnreadCount > 0);
        if (!string.IsNullOrWhiteSpace(category)) threadItems = threadItems.Where(t => t.AiCategory == category);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLowerInvariant();
            threadItems = threadItems.Where(t =>
                t.Subject.ToLower().Contains(s)
                || t.ParticipantEmail.ToLower().Contains(s)
                || (t.ParticipantName != null && t.ParticipantName.ToLower().Contains(s))
                || (t.LatestPreview != null && t.LatestPreview.ToLower().Contains(s)));
        }

        var ordered = threadItems.OrderByDescending(t => t.LastActivityAt).ToList();
        var total = ordered.Count;
        var page = ordered.Skip((pn - 1) * ps).Take(ps).ToList();

        return new PagedResponse<InboxThreadListItemDto>
        {
            Data = page,
            PageNumber = pn,
            PageSize = ps,
            TotalCount = total,
        };
    }

    public async Task<InboxThreadDetailDto> GetThreadByIdAsync(Guid threadId, Guid requesterUserId, bool requesterIsAdmin, CancellationToken ct)
    {
        var inbound = (await _inboxRepo.FindAsync(m => m.ThreadId == threadId, ct)).ToList();
        if (inbound.Count == 0) throw new NotFoundException("Thread", threadId);

        // Access check — requester must own at least one message in the thread (or be admin).
        if (!requesterIsAdmin && inbound.All(m => m.OwnerUserId != requesterUserId))
            throw new ForbiddenException();

        var outbound = (await _outboundRepo.FindAsync(o => o.ThreadId == threadId && o.SendSucceeded, ct)).ToList();

        // Build unified chronological timeline.
        var timeline = new List<ThreadMessageDto>();
        foreach (var m in inbound)
        {
            timeline.Add(new ThreadMessageDto
            {
                Id = m.Id,
                Direction = "in",
                FromEmail = m.FromEmail,
                FromName = m.FromName,
                ToEmail = m.ToEmail,
                Subject = m.Subject,
                HtmlBody = m.HtmlBody,
                TextBody = m.TextBody,
                At = m.ReceivedAt,
                IsRead = m.IsRead,
                AiCategory = m.AiCategory,
            });
        }
        foreach (var o in outbound)
        {
            timeline.Add(new ThreadMessageDto
            {
                Id = o.Id,
                Direction = "out",
                FromEmail = "", // our side — UI shows "You"
                ToEmail = "",
                Subject = o.Subject,
                HtmlBody = o.BodyHtml,
                At = o.SentAt,
                IsRead = true,
            });
        }
        timeline = timeline.OrderBy(t => t.At).ToList();

        // The reply composer + AI draft bind to the LATEST inbound message.
        var latestInbound = inbound.OrderByDescending(m => m.ReceivedAt).First();

        // Mark all inbound as read on open.
        foreach (var m in inbound.Where(m => !m.IsRead))
        {
            m.IsRead = true; m.UpdatedAt = DateTime.UtcNow;
            await _inboxRepo.UpdateAsync(m, ct);
        }

        // Resolve display names.
        Campaign? campaign = null;
        if (latestInbound.MatchedCampaignMessageId.HasValue)
        {
            var cm = await _messageRepo.GetByIdAsync(latestInbound.MatchedCampaignMessageId.Value, ct);
            if (cm is not null) campaign = await _campaignRepo.GetByIdAsync(cm.CampaignId, ct);
        }
        var group = await _groupRepo.GetByIdAsync(latestInbound.SmtpGroupId, ct);

        return new InboxThreadDetailDto
        {
            ThreadId = threadId,
            Subject = inbound.OrderBy(m => m.ReceivedAt).First().Subject,
            ParticipantEmail = latestInbound.FromEmail,
            ParticipantName = latestInbound.FromName,
            SmtpGroupName = group?.Name,
            MatchedCampaignId = campaign?.Id,
            MatchedCampaignName = campaign?.Name,
            Messages = timeline,
            LatestInboundMessageId = latestInbound.Id,
            AiCategory = latestInbound.AiCategory,
            AiSummary = latestInbound.AiSummary,
            AiSuggestedReply = latestInbound.AiSuggestedReply,
            UserEditedReply = latestInbound.UserEditedReply,
            AiGenerationError = latestInbound.AiGenerationError,
            AiProviderUsed = latestInbound.AiProviderUsed,
            AiInputTokens = latestInbound.AiInputTokens,
            AiOutputTokens = latestInbound.AiOutputTokens,
            AiGeneratedAt = latestInbound.AiGeneratedAt,
            AiSuggestedQuestions = ParseQuestions(latestInbound.AiSuggestedQuestions),
        };
    }

    /// <summary>Deserialize the stored questions JSON; fall back to 3 sensible defaults so chips are never empty.</summary>
    private static List<string> ParseQuestions(string? json)
    {
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
                if (list is { Count: > 0 }) return list.Where(s => !string.IsNullOrWhiteSpace(s)).Take(3).ToList();
            }
            catch { /* fall through */ }
        }
        return new List<string> { "Summarize this email", "What is the sender asking for?", "List action items / next steps" };
    }

    public async Task DeleteThreadAsync(Guid threadId, Guid requesterUserId, bool requesterIsAdmin, CancellationToken ct)
    {
        var inbound = (await _inboxRepo.FindAsync(m => m.ThreadId == threadId, ct)).ToList();
        if (inbound.Count == 0) throw new NotFoundException("Thread", threadId);
        if (!requesterIsAdmin && inbound.All(m => m.OwnerUserId != requesterUserId))
            throw new ForbiddenException();

        // Delete outbound replies first (FK), then inbound messages.
        var outbound = (await _outboundRepo.FindAsync(o => o.ThreadId == threadId, ct)).ToList();
        foreach (var o in outbound) await _outboundRepo.DeleteAsync(o, ct);
        foreach (var m in inbound) await _inboxRepo.DeleteAsync(m, ct);

        await _audit.LogAsync(requesterUserId, "InboxThreadDeleted", "InboxThread", threadId,
            details: new { messages = inbound.Count, replies = outbound.Count }, ct: ct);
    }

    public async Task<int> ClearAllAsync(Guid requesterUserId, bool requesterIsAdmin, bool resetImapCursor, CancellationToken ct)
    {
        // Scope: admin clears everyone's; regular user clears only their own.
        var messages = requesterIsAdmin
            ? (await _inboxRepo.FindAsync(m => true, ct)).ToList()
            : (await _inboxRepo.FindAsync(m => m.OwnerUserId == requesterUserId, ct)).ToList();

        var threadIds = messages.Select(m => m.ThreadId).Distinct().ToHashSet();
        var outbound = (await _outboundRepo.FindAsync(o => threadIds.Contains(o.ThreadId), ct)).ToList();
        foreach (var o in outbound) await _outboundRepo.DeleteAsync(o, ct);
        foreach (var m in messages) await _inboxRepo.DeleteAsync(m, ct);

        // Optionally rewind the IMAP cursor so the SAME emails get re-polled (re-test from scratch).
        if (resetImapCursor)
        {
            var groups = (await _groupRepo.FindAsync(g => g.EnableInboxPolling, ct)).ToList();
            foreach (var g in groups)
            {
                g.LastImapUid = 0;
                g.LastInboxPolledAt = null;
                await _groupRepo.UpdateAsync(g, ct);
            }
        }

        await _audit.LogAsync(requesterUserId, "InboxCleared", "Inbox", null,
            details: new { deleted = messages.Count, resetImapCursor }, ct: ct);
        return messages.Count;
    }

    private static void EnsureAccess(InboxMessage m, Guid requesterUserId, bool isAdmin)
    {
        if (m.OwnerUserId != requesterUserId && !isAdmin)
            throw new ForbiddenException();
    }

    private static string? BuildPreview(InboxMessage m)
    {
        var src = !string.IsNullOrWhiteSpace(m.TextBody) ? m.TextBody : StripTags(m.HtmlBody ?? "");
        if (string.IsNullOrWhiteSpace(src)) return null;
        src = src.Trim();
        return src.Length > 200 ? src[..200] : src;
    }

    private static string StripTags(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        return System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
    }

    private static string? Truncate(string? s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max];
}
