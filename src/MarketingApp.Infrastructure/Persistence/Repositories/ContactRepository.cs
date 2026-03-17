using Microsoft.EntityFrameworkCore;
using MarketingApp.Application.Interfaces;
using MarketingApp.Domain.Entities;

namespace MarketingApp.Infrastructure.Persistence.Repositories;

public class ContactRepository : GenericRepository<Contact>, IContactRepository
{
    public ContactRepository(AppDbContext context) : base(context) { }

    public async Task<(IEnumerable<Contact> Items, int TotalCount)> GetPagedAsync(
        Guid userId, int pageNumber, int pageSize, Guid? groupId, string? search, CancellationToken ct)
    {
        var query = _dbSet.Where(c => c.UserId == userId && c.IsActive);

        if (groupId.HasValue)
            query = query.Where(c => c.GroupId == groupId.Value);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.FullName.Contains(search) ||
                                     (c.Email != null && c.Email.Contains(search)) ||
                                     (c.Phone != null && c.Phone.Contains(search)));

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Include(c => c.Group)
            .OrderByDescending(c => c.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<IEnumerable<Contact>> GetByGroupAsync(Guid groupId, CancellationToken ct)
        => await _dbSet.Where(c => c.GroupId == groupId && c.IsActive).AsNoTracking().ToListAsync(ct);

    public async Task<bool> EmailExistsForUserAsync(Guid userId, string email, Guid? excludeId, CancellationToken ct)
    {
        var query = _dbSet.Where(c => c.UserId == userId && c.Email == email);
        if (excludeId.HasValue)
            query = query.Where(c => c.Id != excludeId.Value);
        return await query.AnyAsync(ct);
    }
}
