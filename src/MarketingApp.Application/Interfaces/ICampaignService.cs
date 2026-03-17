using MarketingApp.Application.DTOs;

namespace MarketingApp.Application.Interfaces;

public interface ICampaignService
{
    Task<PagedResponse<CampaignDto>> GetAllAsync(Guid userId, int pageNumber, int pageSize, string? status, string? channel, CancellationToken ct);
    Task<CampaignDetailDto> GetByIdAsync(Guid id, Guid userId, CancellationToken ct);
    Task<CampaignDto> CreateAsync(Guid userId, CreateCampaignDto dto, CancellationToken ct);
    Task<CampaignDto> UpdateAsync(Guid id, Guid userId, UpdateCampaignDto dto, CancellationToken ct);
    Task DeleteAsync(Guid id, Guid userId, CancellationToken ct);
    Task SendAsync(Guid id, Guid userId, DateTime? scheduledAt, CancellationToken ct);
    Task<CampaignReportDto> GetReportAsync(Guid id, Guid userId, CancellationToken ct);
    Task<PagedResponse<CampaignMessageDto>> GetMessagesAsync(Guid id, Guid userId, int pageNumber, int pageSize, string? status, CancellationToken ct);
}
