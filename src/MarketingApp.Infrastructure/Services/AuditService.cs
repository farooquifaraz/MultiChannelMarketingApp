using System.Text.Json;
using Microsoft.Extensions.Logging;
using MarketingApp.Application.Interfaces;
using MarketingApp.Domain.Entities;
using MarketingApp.Infrastructure.Persistence;

namespace MarketingApp.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly AppDbContext _context;
    private readonly ILogger<AuditService> _logger;

    public AuditService(AppDbContext context, ILogger<AuditService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task LogAsync(Guid? userId, string action, string? entity, Guid? entityId, object? details = null, string? ipAddress = null, CancellationToken ct = default)
    {
        var log = new AuditLog
        {
            UserId = userId,
            Action = action,
            Entity = entity,
            EntityId = entityId,
            Details = details != null ? JsonSerializer.Serialize(details) : null,
            IpAddress = ipAddress
        };

        await _context.AuditLogs.AddAsync(log, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Audit: {Action} on {Entity} ({EntityId}) by user {UserId}", action, entity, entityId, userId);
    }
}
