using AutoMapper;
using Microsoft.Extensions.Logging;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Domain.Constants;
using MarketingApp.Domain.Entities;

namespace MarketingApp.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly IGenericRepository<Contact> _contactRepo;
    private readonly ICampaignRepository _campaignRepo;
    private readonly ICacheService _cache;
    private readonly IMapper _mapper;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(
        IGenericRepository<Contact> contactRepo,
        ICampaignRepository campaignRepo,
        ICacheService cache,
        IMapper mapper,
        ILogger<DashboardService> logger)
    {
        _contactRepo = contactRepo;
        _campaignRepo = campaignRepo;
        _cache = cache;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<DashboardStatsDto> GetStatsAsync(Guid userId, CancellationToken ct)
    {
        var cacheKey = string.Format(AppConstants.CacheKeys.DashboardStats, userId);
        var cached = await _cache.GetAsync<DashboardStatsDto>(cacheKey, ct);
        if (cached != null) return cached;

        var totalContacts = await _contactRepo.CountAsync(c => ((Contact)(object)c).UserId == userId, ct);
        var campaigns = await _campaignRepo.FindAsync(c => c.UserId == userId, ct);
        var campaignList = campaigns.ToList();

        var stats = new DashboardStatsDto
        {
            TotalContacts = totalContacts,
            TotalCampaigns = campaignList.Count,
            TotalMessagesSent = campaignList.Sum(c => c.SentCount),
            ActiveCampaigns = campaignList.Count(c => c.Status == "running" || c.Status == "queued"),
            OverallSuccessRate = campaignList.Sum(c => c.SentCount + c.FailedCount) > 0
                ? Math.Round((double)campaignList.Sum(c => c.SentCount) / campaignList.Sum(c => c.SentCount + c.FailedCount) * 100, 2)
                : 0
        };

        await _cache.SetAsync(cacheKey, stats, TimeSpan.FromMinutes(AppConstants.CacheDurationMinutes), ct);
        return stats;
    }

    public async Task<IEnumerable<CampaignDto>> GetRecentCampaignsAsync(Guid userId, int count, CancellationToken ct)
    {
        var (items, _) = await _campaignRepo.GetPagedAsync(userId, 1, count, null, null, ct);
        return _mapper.Map<IEnumerable<CampaignDto>>(items);
    }

    public async Task<ChannelBreakdownDto> GetChannelBreakdownAsync(Guid userId, CancellationToken ct)
    {
        var campaigns = await _campaignRepo.FindAsync(c => c.UserId == userId, ct);
        var list = campaigns.ToList();

        return new ChannelBreakdownDto
        {
            Email = BuildChannelStats(list, "email"),
            WhatsApp = BuildChannelStats(list, "whatsapp"),
            Sms = BuildChannelStats(list, "sms")
        };
    }

    private static ChannelStatsDto BuildChannelStats(List<Campaign> campaigns, string channel)
    {
        var filtered = campaigns.Where(c => c.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase)).ToList();
        return new ChannelStatsDto
        {
            CampaignCount = filtered.Count,
            MessagesSent = filtered.Sum(c => c.SentCount),
            MessagesFailed = filtered.Sum(c => c.FailedCount)
        };
    }
}
