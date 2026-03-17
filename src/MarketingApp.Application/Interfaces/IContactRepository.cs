using MarketingApp.Domain.Entities;

namespace MarketingApp.Application.Interfaces;

public interface IContactRepository : IGenericRepository<Contact>
{
    Task<(IEnumerable<Contact> Items, int TotalCount)> GetPagedAsync(Guid userId, int pageNumber, int pageSize, Guid? groupId, string? search, CancellationToken ct);
    Task<IEnumerable<Contact>> GetByGroupAsync(Guid groupId, CancellationToken ct);
    Task<bool> EmailExistsForUserAsync(Guid userId, string email, Guid? excludeId, CancellationToken ct);
}
