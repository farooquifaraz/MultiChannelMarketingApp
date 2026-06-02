using MarketingApp.Domain.Entities;

namespace MarketingApp.Application.Interfaces;

public interface ICampaignRepository : IGenericRepository<Campaign>
{
    Task<(IEnumerable<Campaign> Items, int TotalCount)> GetPagedAsync(Guid? userId, int pageNumber, int pageSize, string? status, string? channel, CancellationToken ct);
    Task<Campaign?> GetWithTemplateAsync(Guid id, CancellationToken ct);
    Task<IEnumerable<CampaignMessage>> GetPendingMessagesAsync(Guid campaignId, CancellationToken ct);
    Task BulkInsertMessagesAsync(IEnumerable<CampaignMessage> messages, CancellationToken ct);
    Task UpdateMessageAsync(CampaignMessage message, CancellationToken ct);
    Task<int> GetSentCountAsync(Guid campaignId, CancellationToken ct);
    Task<int> GetFailedCountAsync(Guid campaignId, CancellationToken ct);
    /// <summary>Count this campaign's messages whose Status is in the given set. Used to recompute
    /// campaign tallies from the source of truth (the messages) rather than a drifting snapshot.</summary>
    Task<int> CountMessagesByStatusAsync(Guid campaignId, IEnumerable<string> statuses, CancellationToken ct);
    Task<(IEnumerable<CampaignMessage> Items, int TotalCount)> GetMessagesPagedAsync(Guid campaignId, int pageNumber, int pageSize, string? status, CancellationToken ct);
    Task<IEnumerable<CampaignMessage>> GetMessagesByStatusAsync(Guid campaignId, string status, CancellationToken ct);
    Task BulkUpdateMessageStatusAsync(IEnumerable<Guid> messageIds, string newStatus, CancellationToken ct);
}
