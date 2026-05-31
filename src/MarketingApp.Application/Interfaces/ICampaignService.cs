using MarketingApp.Application.DTOs;

namespace MarketingApp.Application.Interfaces;

public interface ICampaignService
{
    /// <summary>userId == null → admin view (all users); non-null → that user's campaigns.</summary>
    Task<PagedResponse<CampaignDto>> GetAllAsync(Guid? userId, int pageNumber, int pageSize, string? status, string? channel, CancellationToken ct);
    Task<CampaignDetailDto> GetByIdAsync(Guid id, Guid userId, CancellationToken ct);
    Task<CampaignDetailDto> GetByIdAsync(Guid id, Guid? requesterUserId, bool requesterIsAdmin, CancellationToken ct);
    Task<CampaignDto> CreateAsync(Guid userId, CreateCampaignDto dto, CancellationToken ct);
    Task<CampaignDto> UpdateAsync(Guid id, Guid userId, UpdateCampaignDto dto, CancellationToken ct);
    Task DeleteAsync(Guid id, Guid userId, CancellationToken ct);
    Task SendAsync(Guid id, Guid userId, DateTime? scheduledAt, CancellationToken ct);
    Task<CampaignReportDto> GetReportAsync(Guid id, Guid requesterUserId, bool requesterIsAdmin, CancellationToken ct);
    Task<PagedResponse<CampaignMessageDto>> GetMessagesAsync(Guid id, Guid requesterUserId, bool requesterIsAdmin, int pageNumber, int pageSize, string? status, CancellationToken ct);

    /// <summary>Cancel a scheduled (queued + future scheduledAt) campaign. Reverts to draft so the scheduled job becomes a no-op.</summary>
    Task CancelScheduledAsync(Guid id, Guid requesterUserId, bool requesterIsAdmin, CancellationToken ct);

    /// <summary>Reset all failed messages on a completed campaign back to pending and re-enqueue the job.</summary>
    Task<int> RetryFailedAsync(Guid id, Guid requesterUserId, bool requesterIsAdmin, CancellationToken ct);
}
