namespace MarketingApp.Application.Interfaces;

public interface IAuditService
{
    Task LogAsync(Guid? userId, string action, string? entity, Guid? entityId, object? details = null, string? ipAddress = null, CancellationToken ct = default);
}
