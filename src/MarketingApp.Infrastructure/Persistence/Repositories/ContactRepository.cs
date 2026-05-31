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
        // Resolve the requester's SmtpGroupId once — used for the "shared via SmtpGroup" branch.
        var requesterSmtpGroupId = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => u.SmtpGroupId)
            .FirstOrDefaultAsync(ct);

        // Visible contacts:
        //   1. The user's OWN contacts, OR
        //   2. Contacts whose ContactGroup is linked to the SAME SmtpGroup the requester belongs to.
        var query = _dbSet
            .Where(c => c.IsActive)
            .Where(c =>
                c.UserId == userId
                || (requesterSmtpGroupId != null
                    && c.GroupId != null
                    && c.Group != null
                    && c.Group.SmtpGroupId == requesterSmtpGroupId));

        if (groupId.HasValue)
            query = query.Where(c => c.GroupId == groupId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Case-INSENSITIVE match (ILIKE). Plain .Contains() translates to a case-sensitive LIKE in
            // PostgreSQL, so "faraz" wouldn't find "Faraz Farooqui". ILike fixes that across name/email/phone.
            var pattern = $"%{search}%";
            query = query.Where(c => EF.Functions.ILike(c.FullName, pattern) ||
                                     (c.Email != null && EF.Functions.ILike(c.Email, pattern)) ||
                                     (c.Phone != null && EF.Functions.ILike(c.Phone, pattern)));
        }

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
