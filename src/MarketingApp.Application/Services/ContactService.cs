using System.Text.Json;
using AutoMapper;
using Microsoft.Extensions.Logging;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Domain.Entities;
using MarketingApp.Domain.Exceptions;

namespace MarketingApp.Application.Services;

public class ContactService : IContactService
{
    private readonly IContactRepository _contactRepo;
    private readonly IGenericRepository<ContactGroup> _groupRepo;
    private readonly IAuditService _audit;
    private readonly IMapper _mapper;
    private readonly ILogger<ContactService> _logger;

    public ContactService(
        IContactRepository contactRepo,
        IGenericRepository<ContactGroup> groupRepo,
        IAuditService audit,
        IMapper mapper,
        ILogger<ContactService> logger)
    {
        _contactRepo = contactRepo;
        _groupRepo = groupRepo;
        _audit = audit;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<PagedResponse<ContactDto>> GetAllAsync(Guid userId, int pageNumber, int pageSize, Guid? groupId, string? search, CancellationToken ct)
    {
        var (items, totalCount) = await _contactRepo.GetPagedAsync(userId, pageNumber, pageSize, groupId, search, ct);
        return new PagedResponse<ContactDto>
        {
            Data = _mapper.Map<IEnumerable<ContactDto>>(items),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<ContactDto> GetByIdAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var contact = await _contactRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Contact", id);
        if (contact.UserId != userId)
            throw new ForbiddenException();
        return _mapper.Map<ContactDto>(contact);
    }

    public async Task<ContactDto> CreateAsync(Guid userId, CreateContactDto dto, CancellationToken ct)
    {
        // Check for duplicate email
        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            var emailExists = await _contactRepo.EmailExistsForUserAsync(userId, dto.Email, null, ct);
            if (emailExists)
                throw new ConflictException($"A contact with email '{dto.Email}' already exists.");
        }

        // Check for duplicate phone
        if (!string.IsNullOrWhiteSpace(dto.Phone))
        {
            var phoneExists = await _contactRepo.AnyAsync(c => c.UserId == userId && c.Phone == dto.Phone && c.IsActive, ct);
            if (phoneExists)
                throw new ConflictException($"A contact with phone '{dto.Phone}' already exists.");
        }

        // Check for duplicate WhatsApp
        if (!string.IsNullOrWhiteSpace(dto.WhatsAppNumber))
        {
            var waExists = await _contactRepo.AnyAsync(c => c.UserId == userId && c.WhatsAppNumber == dto.WhatsAppNumber && c.IsActive, ct);
            if (waExists)
                throw new ConflictException($"A contact with WhatsApp number '{dto.WhatsAppNumber}' already exists.");
        }

        var contact = _mapper.Map<Contact>(dto);
        contact.UserId = userId;
        contact.CustomFields = dto.CustomFields != null ? JsonSerializer.Serialize(dto.CustomFields) : null;

        await _contactRepo.AddAsync(contact, ct);
        await _audit.LogAsync(userId, "ContactCreated", "Contact", contact.Id, ct: ct);
        return _mapper.Map<ContactDto>(contact);
    }

    public async Task<ContactDto> UpdateAsync(Guid id, Guid userId, UpdateContactDto dto, CancellationToken ct)
    {
        var contact = await _contactRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Contact", id);
        if (contact.UserId != userId)
            throw new ForbiddenException();

        // Check for duplicate email (excluding current contact)
        if (!string.IsNullOrWhiteSpace(dto.Email) && dto.Email != contact.Email)
        {
            var emailExists = await _contactRepo.EmailExistsForUserAsync(userId, dto.Email, id, ct);
            if (emailExists)
                throw new ConflictException($"A contact with email '{dto.Email}' already exists.");
        }

        _mapper.Map(dto, contact);
        contact.CustomFields = dto.CustomFields != null ? JsonSerializer.Serialize(dto.CustomFields) : null;
        contact.UpdatedAt = DateTime.UtcNow;
        await _contactRepo.UpdateAsync(contact, ct);
        await _audit.LogAsync(userId, "ContactUpdated", "Contact", id, ct: ct);
        return _mapper.Map<ContactDto>(contact);
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var contact = await _contactRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Contact", id);
        if (contact.UserId != userId)
            throw new ForbiddenException();

        await _contactRepo.DeleteAsync(contact, ct);
        await _audit.LogAsync(userId, "ContactDeleted", "Contact", id, ct: ct);
    }

    public async Task<ImportResultDto> ImportContactsAsync(Guid userId, Stream fileStream, string fileName, Guid? groupId, CancellationToken ct)
    {
        var result = new ImportResultDto();
        var contacts = new List<Contact>();

        using var reader = new StreamReader(fileStream);
        var headerLine = await reader.ReadLineAsync(ct);
        if (headerLine == null) return result;

        var headers = headerLine.Split(',').Select(h => h.Trim().ToLowerInvariant()).ToArray();

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            result.TotalRows++;
            try
            {
                var values = line.Split(',');
                var email = GetValue(headers, values, "email");
                var phone = GetValue(headers, values, "phone");
                var whatsapp = GetValue(headers, values, "whatsapp") ?? GetValue(headers, values, "whatsapp_number");

                // Skip duplicates within this import batch
                if (!string.IsNullOrWhiteSpace(email) && contacts.Any(c => c.Email?.ToLower() == email.ToLower()))
                {
                    result.FailedCount++;
                    result.Errors.Add($"Row {result.TotalRows}: Duplicate email '{email}' in import file");
                    continue;
                }

                // Skip if already exists in database
                if (!string.IsNullOrWhiteSpace(email) && await _contactRepo.EmailExistsForUserAsync(userId, email, null, ct))
                {
                    result.FailedCount++;
                    result.Errors.Add($"Row {result.TotalRows}: Contact with email '{email}' already exists");
                    continue;
                }

                var contact = new Contact
                {
                    UserId = userId,
                    GroupId = groupId,
                    FullName = GetValue(headers, values, "full_name") ?? GetValue(headers, values, "name") ?? "Unknown",
                    Email = email,
                    Phone = phone,
                    WhatsAppNumber = whatsapp
                };
                contacts.Add(contact);
                result.SuccessCount++;
            }
            catch (Exception ex)
            {
                result.FailedCount++;
                result.Errors.Add($"Row {result.TotalRows}: {ex.Message}");
            }
        }

        if (contacts.Any())
            await _contactRepo.AddRangeAsync(contacts, ct);

        await _audit.LogAsync(userId, "ContactsImported", "Contact", null, new { result.TotalRows, result.SuccessCount, result.FailedCount }, ct: ct);
        return result;
    }

    private static string? GetValue(string[] headers, string[] values, string header)
    {
        var index = Array.IndexOf(headers, header);
        if (index < 0 || index >= values.Length) return null;
        var val = values[index].Trim().Trim('"');
        return string.IsNullOrWhiteSpace(val) ? null : val;
    }

    public async Task<IEnumerable<ContactGroupDto>> GetGroupsAsync(Guid userId, CancellationToken ct)
    {
        var groups = await _groupRepo.FindAsync(g => g.UserId == userId, ct);
        var dtos = new List<ContactGroupDto>();
        foreach (var g in groups)
        {
            var count = await _contactRepo.CountAsync(c => c.GroupId == g.Id && c.IsActive, ct);
            dtos.Add(new ContactGroupDto
            {
                Id = g.Id,
                Name = g.Name,
                Description = g.Description,
                ContactCount = count,
                CreatedAt = g.CreatedAt
            });
        }
        return dtos.OrderByDescending(g => g.CreatedAt);
    }

    public async Task<ContactGroupDto> CreateGroupAsync(Guid userId, CreateContactGroupDto dto, CancellationToken ct)
    {
        var group = new ContactGroup
        {
            UserId = userId,
            Name = dto.Name,
            Description = dto.Description
        };
        await _groupRepo.AddAsync(group, ct);
        return new ContactGroupDto { Id = group.Id, Name = group.Name, Description = group.Description, CreatedAt = group.CreatedAt };
    }

    public async Task DeleteGroupAsync(Guid groupId, Guid userId, CancellationToken ct)
    {
        var group = await _groupRepo.GetByIdAsync(groupId, ct)
            ?? throw new NotFoundException("ContactGroup", groupId);
        if (group.UserId != userId) throw new ForbiddenException();
        await _groupRepo.DeleteAsync(group, ct);
    }

    public async Task<int> AssignToGroupAsync(Guid userId, List<Guid> contactIds, Guid? groupId, CancellationToken ct)
    {
        int count = 0;
        foreach (var cid in contactIds)
        {
            var contact = await _contactRepo.GetByIdAsync(cid, ct);
            if (contact == null || contact.UserId != userId) continue;
            contact.GroupId = groupId;
            contact.UpdatedAt = DateTime.UtcNow;
            await _contactRepo.UpdateAsync(contact, ct);
            count++;
        }
        await _audit.LogAsync(userId, "ContactsAssignedToGroup", "ContactGroup", groupId, new { contactIds, count }, ct: ct);
        return count;
    }
}
