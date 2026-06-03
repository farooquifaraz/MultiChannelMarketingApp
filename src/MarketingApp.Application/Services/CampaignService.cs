using AutoMapper;
using Hangfire;
using Microsoft.Extensions.Logging;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Domain.Entities;
using MarketingApp.Domain.Exceptions;

namespace MarketingApp.Application.Services;

public class CampaignService : ICampaignService
{
    private readonly ICampaignRepository _campaignRepo;
    private readonly IContactRepository _contactRepo;
    private readonly IGenericRepository<MessageTemplate> _templateRepo;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ICacheService _cache;
    private readonly IAuditService _audit;
    private readonly IMapper _mapper;
    private readonly IQuotaService _quota;
    private readonly ILogger<CampaignService> _logger;

    public CampaignService(
        ICampaignRepository campaignRepo,
        IContactRepository contactRepo,
        IGenericRepository<MessageTemplate> templateRepo,
        IBackgroundJobClient backgroundJobs,
        ICacheService cache,
        IAuditService audit,
        IMapper mapper,
        IQuotaService quota,
        ILogger<CampaignService> logger)
    {
        _campaignRepo = campaignRepo;
        _contactRepo = contactRepo;
        _templateRepo = templateRepo;
        _backgroundJobs = backgroundJobs;
        _cache = cache;
        _audit = audit;
        _mapper = mapper;
        _quota = quota;
        _logger = logger;
    }

    public async Task<PagedResponse<CampaignDto>> GetAllAsync(Guid? userId, int pageNumber, int pageSize, string? status, string? channel, CancellationToken ct)
    {
        // userId == null → admin-wide view (all users, all groups)
        var (items, totalCount) = await _campaignRepo.GetPagedAsync(userId, pageNumber, pageSize, status, channel, ct);
        var dtos = items.Select(c => new CampaignDto
        {
            Id = c.Id,
            Name = c.Name,
            Channel = c.Channel,
            Status = c.Status,
            TemplateName = c.Template?.Name,
            GroupName = c.Group?.Name,
            TotalContacts = c.TotalContacts,
            SentCount = c.SentCount,
            FailedCount = c.FailedCount,
            ScheduledAt = c.ScheduledAt,
            CompletedAt = c.CompletedAt,
            CreatedAt = c.CreatedAt,
            OwnerUserId = c.UserId,
            OwnerName = c.User?.FullName,
            OwnerEmail = c.User?.Email,
            SmtpGroupName = c.User?.SmtpGroup?.Name,
        });
        return new PagedResponse<CampaignDto>
        {
            Data = dtos,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<CampaignDetailDto> GetByIdAsync(Guid id, Guid? requesterUserId, bool requesterIsAdmin, CancellationToken ct)
    {
        var campaign = await _campaignRepo.GetWithTemplateAsync(id, ct)
            ?? throw new NotFoundException("Campaign", id);
        // Admin can view any campaign; regular user only their own.
        if (!requesterIsAdmin && campaign.UserId != requesterUserId)
            throw new ForbiddenException();
        return _mapper.Map<CampaignDetailDto>(campaign);
    }

    public async Task<CampaignDetailDto> GetByIdAsync(Guid id, Guid userId, CancellationToken ct)
        => await GetByIdAsync(id, userId, false, ct);

    public async Task<CampaignDto> CreateAsync(Guid userId, CreateCampaignDto dto, CancellationToken ct)
    {
        var template = await _templateRepo.GetByIdAsync(dto.TemplateId, ct)
            ?? throw new NotFoundException("Template", dto.TemplateId);

        var campaign = _mapper.Map<Campaign>(dto);
        campaign.UserId = userId;
        campaign.Status = "draft";
        await _campaignRepo.AddAsync(campaign, ct);

        // Reload with navigation properties
        var created = await _campaignRepo.GetWithTemplateAsync(campaign.Id, ct);
        await _audit.LogAsync(userId, "CampaignCreated", "Campaign", campaign.Id, ct: ct);
        return _mapper.Map<CampaignDto>(created);
    }

    public async Task<CampaignDto> UpdateAsync(Guid id, Guid userId, UpdateCampaignDto dto, CancellationToken ct)
    {
        var campaign = await _campaignRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Campaign", id);
        if (campaign.UserId != userId) throw new ForbiddenException();
        if (campaign.Status != "draft")
            throw new ConflictException("Only draft campaigns can be updated.");

        _mapper.Map(dto, campaign);
        await _campaignRepo.UpdateAsync(campaign, ct);
        var updated = await _campaignRepo.GetWithTemplateAsync(id, ct);
        return _mapper.Map<CampaignDto>(updated);
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var campaign = await _campaignRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Campaign", id);
        if (campaign.UserId != userId) throw new ForbiddenException();
        if (campaign.Status == "running")
            throw new ConflictException("Cannot delete a running campaign.");
        await _campaignRepo.DeleteAsync(campaign, ct);
    }

    public async Task SendAsync(Guid id, Guid userId, DateTime? scheduledAt, CancellationToken ct)
    {
        var campaign = await _campaignRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Campaign", id);
        if (campaign.UserId != userId) throw new ForbiddenException();
        if (campaign.Status != "draft")
            throw new ConflictException($"Campaign is already in {campaign.Status} state.");

        var contacts = await _contactRepo.GetByGroupAsync(campaign.GroupId, ct);
        // Auto-exclude bounced contacts (hard-bounced previously) AND inactive ones.
        // Protects sender reputation by not re-emailing addresses we know are bad.
        var contactList = contacts.Where(c => c.IsActive && !c.IsBounced).ToList();
        if (!contactList.Any())
            throw new AppValidationException("Campaign group has no deliverable contacts (after excluding bounced and inactive).");

        // P2.3 — plan quota check (gated by enable_quotas; a no-op when the flag is off, so existing
        // sends are unaffected). Blocks BEFORE inserting/queuing if the send would exceed the limit.
        var quotaKind = campaign.Channel?.ToLowerInvariant() switch
        {
            "whatsapp" => (QuotaKind?)QuotaKind.WhatsApp,
            "email" => QuotaKind.Email,
            _ => null,
        };
        if (quotaKind is QuotaKind qk)
        {
            var quota = await _quota.CheckAsync(userId, qk, contactList.Count, ct);
            if (!quota.Allowed)
                throw new AppValidationException(quota.Reason ?? "Plan limit reached. Upgrade your plan to send more.");
        }

        var messages = contactList.Select(c => new CampaignMessage
        {
            CampaignId = id,
            ContactId = c.Id,
            Status = "pending"
        }).ToList();

        await _campaignRepo.BulkInsertMessagesAsync(messages, ct);

        campaign.Status = "queued";
        campaign.TotalContacts = contactList.Count;
        await _campaignRepo.UpdateAsync(campaign, ct);

        if (scheduledAt.HasValue && scheduledAt > DateTime.UtcNow)
        {
            _backgroundJobs.Schedule<ICampaignJobService>(j => j.ProcessCampaignAsync(id), scheduledAt.Value);
            _logger.LogInformation("Campaign {CampaignId} scheduled for {ScheduledAt}", id, scheduledAt);
        }
        else
        {
            _backgroundJobs.Enqueue<ICampaignJobService>(j => j.ProcessCampaignAsync(id));
            _logger.LogInformation("Campaign {CampaignId} enqueued for immediate processing", id);
        }

        await _audit.LogAsync(userId, "CampaignSent", "Campaign", id, new { scheduledAt }, ct: ct);
    }

    public async Task CancelScheduledAsync(Guid id, Guid requesterUserId, bool requesterIsAdmin, CancellationToken ct)
    {
        var campaign = await _campaignRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Campaign", id);

        // Owner or admin only
        if (!requesterIsAdmin && campaign.UserId != requesterUserId)
            throw new ForbiddenException();

        // Only queued campaigns with a future scheduledAt can be cancelled.
        if (campaign.Status != "queued")
            throw new ConflictException($"Cannot cancel a campaign in '{campaign.Status}' state.");
        if (!campaign.ScheduledAt.HasValue || campaign.ScheduledAt <= DateTime.UtcNow)
            throw new ConflictException("Only future scheduled campaigns can be cancelled.");

        // Revert to draft. The Hangfire-scheduled job will still fire but ProcessCampaignAsync
        // checks the status upfront and exits early when it isn't 'queued'.
        campaign.Status = "draft";
        campaign.ScheduledAt = null;
        campaign.TotalContacts = 0;
        await _campaignRepo.UpdateAsync(campaign, ct);

        await _audit.LogAsync(requesterUserId, "CampaignScheduleCancelled", "Campaign", id, ct: ct);
        _logger.LogInformation("Campaign {CampaignId} scheduled send cancelled by user {UserId}", id, requesterUserId);
    }

    public async Task<int> RetryFailedAsync(Guid id, Guid requesterUserId, bool requesterIsAdmin, CancellationToken ct)
    {
        var campaign = await _campaignRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Campaign", id);

        if (!requesterIsAdmin && campaign.UserId != requesterUserId)
            throw new ForbiddenException();

        // Retry only makes sense on a finished campaign. Running/queued mean the job is still
        // chewing through messages on its own.
        if (campaign.Status != "completed" && campaign.Status != "failed")
            throw new ConflictException($"Cannot retry while campaign is in '{campaign.Status}' state. Wait for it to finish first.");

        var failed = (await _campaignRepo.GetMessagesByStatusAsync(id, "failed", ct)).ToList();
        if (failed.Count == 0)
            throw new ConflictException("No failed messages to retry.");

        // Reset failed messages back to pending so the job picks them up.
        await _campaignRepo.BulkUpdateMessageStatusAsync(failed.Select(m => m.Id), "pending", ct);

        // Adjust counts and re-queue.
        campaign.Status = "queued";
        campaign.FailedCount = Math.Max(0, campaign.FailedCount - failed.Count);
        campaign.CompletedAt = null;
        await _campaignRepo.UpdateAsync(campaign, ct);

        _backgroundJobs.Enqueue<ICampaignJobService>(j => j.ProcessCampaignAsync(id));
        await _audit.LogAsync(requesterUserId, "CampaignRetryFailed", "Campaign", id, new { retriedCount = failed.Count }, ct: ct);
        _logger.LogInformation("Retry queued for {Count} failed messages in campaign {CampaignId}", failed.Count, id);

        return failed.Count;
    }

    public async Task<CampaignReportDto> GetReportAsync(Guid id, Guid requesterUserId, bool requesterIsAdmin, CancellationToken ct)
    {
        var campaign = await _campaignRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Campaign", id);
        // Admin can pull any campaign's report; regular users only their own.
        if (!requesterIsAdmin && campaign.UserId != requesterUserId)
            throw new ForbiddenException();

        var allMessages = (await _campaignRepo.GetMessagesPagedAsync(id, 1, int.MaxValue, null, ct)).Items.ToList();

        // M6 — compute every tally LIVE from the messages (the source of truth), not from the
        // campaign's snapshot counters. Snapshots drift: a retry resets SentCount, an async bounce
        // webhook flips a message after completion, a deleted contact removes messages. Counting the
        // actual rows here keeps the Delivery Report correct in all of those cases.
        //
        // Status ladder (a message sits at exactly one): pending → sent → delivered → opened → clicked,
        // with failed / bounced as terminal failure states. "Delivered or better" therefore means the
        // message definitely reached the inbox, so delivered/opened/clicked all count as delivered.
        static bool In(CampaignMessage m, params string[] s) => s.Contains(m.Status);
        var reached    = allMessages.Count(m => In(m, "delivered", "opened", "clicked"));
        var dispatched = allMessages.Count(m => In(m, "sent", "delivered", "opened", "clicked"));
        var failed     = allMessages.Count(m => In(m, "failed", "bounced"));
        var bounced    = allMessages.Count(m => In(m, "bounced"));

        return new CampaignReportDto
        {
            CampaignId = campaign.Id,
            CampaignName = campaign.Name,
            Channel = campaign.Channel,
            Status = campaign.Status,
            // Live total = the messages that actually exist (handles contacts deleted after send).
            TotalContacts = allMessages.Count,
            SentCount = dispatched,
            FailedCount = failed,
            DeliveredCount = reached,
            // OpenedAt / ClickedAt are set by the tracking endpoints + webhooks regardless of status,
            // so count by those fields, not the status string.
            OpenedCount = allMessages.Count(m => m.OpenedAt != null),
            ClickedCount = allMessages.Count(m => m.ClickedAt != null),
            TotalClicks = allMessages.Sum(m => m.ClickCount),
            BouncedCount = bounced,
            StartedAt = campaign.StartedAt,
            CompletedAt = campaign.CompletedAt
        };
    }

    public async Task<PagedResponse<CampaignMessageDto>> GetMessagesAsync(Guid id, Guid requesterUserId, bool requesterIsAdmin, int pageNumber, int pageSize, string? status, CancellationToken ct)
    {
        var campaign = await _campaignRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Campaign", id);
        if (!requesterIsAdmin && campaign.UserId != requesterUserId)
            throw new ForbiddenException();

        var (items, totalCount) = await _campaignRepo.GetMessagesPagedAsync(id, pageNumber, pageSize, status, ct);
        return new PagedResponse<CampaignMessageDto>
        {
            Data = _mapper.Map<IEnumerable<CampaignMessageDto>>(items),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}
