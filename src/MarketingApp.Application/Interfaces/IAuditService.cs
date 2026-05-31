using MarketingApp.Application.DTOs;

namespace MarketingApp.Application.Interfaces;

public interface IAuditService
{
    Task LogAsync(Guid? userId, string action, string? entity, Guid? entityId, object? details = null, string? ipAddress = null, CancellationToken ct = default);

    Task<PagedResponse<AuditLogDto>> QueryAsync(AuditLogQuery query, CancellationToken ct = default);

    Task<IEnumerable<string>> GetDistinctActionsAsync(CancellationToken ct = default);

    Task<IEnumerable<string>> GetDistinctEntitiesAsync(CancellationToken ct = default);
}
