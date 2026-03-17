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
    private readonly ILogger<CampaignService> _logger;

    public CampaignService(
        ICampaignRepository campaignRepo,
        IContactRepository contactRepo,
        IGenericRepository<MessageTemplate> templateRepo,
        IBackgroundJobClient backgroundJobs,
        ICacheService cache,
        IAuditService audit,
        IMapper mapper,
        ILogger<CampaignService> logger)
    {
        _campaignRepo = campaignRepo;
        _contactRepo = contactRepo;
        _templateRepo = templateRepo;
        _backgroundJobs = backgroundJobs;
        _cache = cache;
        _audit = audit;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<PagedResponse<CampaignDto>> GetAllAsync(Guid userId, int pageNumber, int pageSize, string? status, string? channel, CancellationToken ct)
    {
        var (items, totalCount) = await _campaignRepo.GetPagedAsync(userId, pageNumber, pageSize, status, channel, ct);
        return new PagedResponse<CampaignDto>
        {
            Data = _mapper.Map<IEnumerable<CampaignDto>>(items),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<CampaignDetailDto> GetByIdAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var campaign = await _campaignRepo.GetWithTemplateAsync(id, ct)
            ?? throw new NotFoundException("Campaign", id);
        if (campaign.UserId != userId) throw new ForbiddenException();
        return _mapper.Map<CampaignDetailDto>(campaign);
    }

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
        var contactList = contacts.ToList();
        if (!contactList.Any())
            throw new AppValidationException("Campaign group has no contacts.");

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

    public async Task<CampaignReportDto> GetReportAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var campaign = await _campaignRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Campaign", id);
        if (campaign.UserId != userId) throw new ForbiddenException();

        var messages = await _campaignRepo.GetPendingMessagesAsync(id, ct); // gets all messages actually
        var allMessages = (await _campaignRepo.GetMessagesPagedAsync(id, 1, int.MaxValue, null, ct)).Items;

        return new CampaignReportDto
        {
            CampaignId = campaign.Id,
            CampaignName = campaign.Name,
            Channel = campaign.Channel,
            Status = campaign.Status,
            TotalContacts = campaign.TotalContacts,
            SentCount = campaign.SentCount,
            FailedCount = campaign.FailedCount,
            DeliveredCount = allMessages.Count(m => m.Status == "delivered"),
            OpenedCount = allMessages.Count(m => m.Status == "opened"),
            StartedAt = campaign.StartedAt,
            CompletedAt = campaign.CompletedAt
        };
    }

    public async Task<PagedResponse<CampaignMessageDto>> GetMessagesAsync(Guid id, Guid userId, int pageNumber, int pageSize, string? status, CancellationToken ct)
    {
        var campaign = await _campaignRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Campaign", id);
        if (campaign.UserId != userId) throw new ForbiddenException();

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
