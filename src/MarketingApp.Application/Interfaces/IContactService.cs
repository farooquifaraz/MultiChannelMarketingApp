using MarketingApp.Application.DTOs;

namespace MarketingApp.Application.Interfaces;

public interface IContactService
{
    Task<PagedResponse<ContactDto>> GetAllAsync(Guid userId, int pageNumber, int pageSize, Guid? groupId, string? search, CancellationToken ct);
    Task<ContactDto> GetByIdAsync(Guid id, Guid userId, CancellationToken ct);
    Task<ContactDto> CreateAsync(Guid userId, CreateContactDto dto, CancellationToken ct);
    Task<ContactDto> UpdateAsync(Guid id, Guid userId, UpdateContactDto dto, CancellationToken ct);
    Task DeleteAsync(Guid id, Guid userId, CancellationToken ct);
    Task<ImportResultDto> ImportContactsAsync(Guid userId, Stream fileStream, string fileName, Guid? groupId, CancellationToken ct);
    Task<IEnumerable<ContactGroupDto>> GetGroupsAsync(Guid userId, CancellationToken ct);
    Task<ContactGroupDto> CreateGroupAsync(Guid userId, CreateContactGroupDto dto, CancellationToken ct);
    Task<ContactGroupDto> UpdateGroupAsync(Guid groupId, Guid userId, CreateContactGroupDto dto, CancellationToken ct);
    Task DeleteGroupAsync(Guid groupId, Guid userId, CancellationToken ct);
    Task<int> AssignToGroupAsync(Guid userId, List<Guid> contactIds, Guid? groupId, CancellationToken ct);

    /// <summary>Clear the bounce flag on a contact so they are deliverable again.</summary>
    Task<ContactDto> ClearBounceAsync(Guid contactId, Guid userId, CancellationToken ct);
}
