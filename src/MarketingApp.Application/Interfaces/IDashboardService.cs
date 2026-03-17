using MarketingApp.Application.DTOs;

namespace MarketingApp.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardStatsDto> GetStatsAsync(Guid userId, CancellationToken ct);
    Task<IEnumerable<CampaignDto>> GetRecentCampaignsAsync(Guid userId, int count, CancellationToken ct);
    Task<ChannelBreakdownDto> GetChannelBreakdownAsync(Guid userId, CancellationToken ct);
}
