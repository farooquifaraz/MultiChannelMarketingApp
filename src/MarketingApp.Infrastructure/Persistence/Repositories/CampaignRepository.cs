using Microsoft.EntityFrameworkCore;
using MarketingApp.Application.Interfaces;
using MarketingApp.Domain.Entities;

namespace MarketingApp.Infrastructure.Persistence.Repositories;

public class CampaignRepository : GenericRepository<Campaign>, ICampaignRepository
{
    public CampaignRepository(AppDbContext context) : base(context) { }

    public async Task<(IEnumerable<Campaign> Items, int TotalCount)> GetPagedAsync(
        Guid userId, int pageNumber, int pageSize, string? status, string? channel, CancellationToken ct)
    {
        var query = _dbSet.Where(c => c.UserId == userId);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(c => c.Status == status);

        if (!string.IsNullOrWhiteSpace(channel))
            query = query.Where(c => c.Channel == channel);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Include(c => c.Template)
            .Include(c => c.Group)
            .OrderByDescending(c => c.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<Campaign?> GetWithTemplateAsync(Guid id, CancellationToken ct)
        => await _dbSet.Include(c => c.Template).Include(c => c.Group).FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IEnumerable<CampaignMessage>> GetPendingMessagesAsync(Guid campaignId, CancellationToken ct)
        => await _context.CampaignMessages
            .Include(m => m.Contact)
            .Where(m => m.CampaignId == campaignId && m.Status == "pending")
            .ToListAsync(ct);

    public async Task BulkInsertMessagesAsync(IEnumerable<CampaignMessage> messages, CancellationToken ct)
    {
        await _context.CampaignMessages.AddRangeAsync(messages, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateMessageAsync(CampaignMessage message, CancellationToken ct)
    {
        _context.CampaignMessages.Update(message);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<int> GetSentCountAsync(Guid campaignId, CancellationToken ct)
        => await _context.CampaignMessages.CountAsync(m => m.CampaignId == campaignId && m.Status == "sent", ct);

    public async Task<int> GetFailedCountAsync(Guid campaignId, CancellationToken ct)
        => await _context.CampaignMessages.CountAsync(m => m.CampaignId == campaignId && m.Status == "failed", ct);

    public async Task<(IEnumerable<CampaignMessage> Items, int TotalCount)> GetMessagesPagedAsync(
        Guid campaignId, int pageNumber, int pageSize, string? status, CancellationToken ct)
    {
        var query = _context.CampaignMessages.Where(m => m.CampaignId == campaignId);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(m => m.Status == status);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Include(m => m.Contact)
            .OrderByDescending(m => m.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
