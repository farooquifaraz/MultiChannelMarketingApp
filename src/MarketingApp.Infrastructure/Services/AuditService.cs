using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MarketingApp.Application.DTOs;
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

    public async Task<PagedResponse<AuditLogDto>> QueryAsync(AuditLogQuery query, CancellationToken ct = default)
    {
        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var q = _context.AuditLogs.AsNoTracking().Include(a => a.User).AsQueryable();

        if (query.UserId.HasValue)
            q = q.Where(a => a.UserId == query.UserId.Value);
        if (!string.IsNullOrWhiteSpace(query.Action))
            q = q.Where(a => a.Action == query.Action);
        if (!string.IsNullOrWhiteSpace(query.Entity))
            q = q.Where(a => a.Entity == query.Entity);
        if (query.FromDate.HasValue)
            q = q.Where(a => a.CreatedAt >= query.FromDate.Value);
        if (query.ToDate.HasValue)
            q = q.Where(a => a.CreatedAt <= query.ToDate.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = $"%{query.Search}%";
            q = q.Where(a =>
                EF.Functions.ILike(a.Action, s) ||
                (a.Entity != null && EF.Functions.ILike(a.Entity, s)) ||
                (a.Details != null && EF.Functions.ILike(a.Details, s)) ||
                (a.User != null && (EF.Functions.ILike(a.User.FullName, s) || EF.Functions.ILike(a.User.Email, s))));
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(a => a.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogDto
            {
                Id = a.Id,
                UserId = a.UserId,
                UserFullName = a.User != null ? a.User.FullName : null,
                UserEmail = a.User != null ? a.User.Email : null,
                Action = a.Action,
                Entity = a.Entity,
                EntityId = a.EntityId,
                Details = a.Details,
                IpAddress = a.IpAddress,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync(ct);

        return new PagedResponse<AuditLogDto>
        {
            Data = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = total,
        };
    }

    public async Task<IEnumerable<string>> GetDistinctActionsAsync(CancellationToken ct = default)
    {
        return await _context.AuditLogs.AsNoTracking()
            .Select(a => a.Action)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<string>> GetDistinctEntitiesAsync(CancellationToken ct = default)
    {
        return await _context.AuditLogs.AsNoTracking()
            .Where(a => a.Entity != null)
            .Select(a => a.Entity!)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync(ct);
    }
}
