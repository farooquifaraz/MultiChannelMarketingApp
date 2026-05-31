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
    private readonly IGenericRepository<User> _userRepo;
    private readonly IGenericRepository<SmtpGroup> _smtpGroupRepo;
    private readonly ISystemSettingsService _systemSettings;
    private readonly IAuditService _audit;
    private readonly IMapper _mapper;
    private readonly ILogger<ContactService> _logger;

    public ContactService(
        IContactRepository contactRepo,
        IGenericRepository<ContactGroup> groupRepo,
        IGenericRepository<User> userRepo,
        IGenericRepository<SmtpGroup> smtpGroupRepo,
        ISystemSettingsService systemSettings,
        IAuditService audit,
        IMapper mapper,
        ILogger<ContactService> logger)
    {
        _contactRepo = contactRepo;
        _groupRepo = groupRepo;
        _userRepo = userRepo;
        _smtpGroupRepo = smtpGroupRepo;
        _systemSettings = systemSettings;
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

    public async Task<ContactDto> ClearBounceAsync(Guid contactId, Guid userId, CancellationToken ct)
    {
        var contact = await _contactRepo.GetByIdAsync(contactId, ct)
            ?? throw new NotFoundException("Contact", contactId);
        if (contact.UserId != userId)
            throw new ForbiddenException();
        if (!contact.IsBounced)
            return _mapper.Map<ContactDto>(contact);

        contact.IsBounced = false;
        contact.BouncedAt = null;
        contact.BounceReason = null;
        contact.UpdatedAt = DateTime.UtcNow;
        await _contactRepo.UpdateAsync(contact, ct);
        await _audit.LogAsync(userId, "ContactBounceCleared", "Contact", contactId, ct: ct);
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
        // Resolve requester's SmtpGroupId so we can include shared contact groups linked to it.
        var requester = await _userRepo.GetByIdAsync(userId, ct);
        var requesterSmtpGroupId = requester?.SmtpGroupId;

        // Groups visible to this user:
        //   1. Groups OWNED by the requester, OR
        //   2. Groups whose SmtpGroupId == requester's SmtpGroupId (team-shared)
        var groups = (await _groupRepo.FindAsync(g =>
            g.UserId == userId ||
            (requesterSmtpGroupId != null && g.SmtpGroupId == requesterSmtpGroupId),
            ct)).ToList();

        // Lookup table for SmtpGroup names (for the "Shared with: X" label)
        var allSmtpGroupIds = groups.Where(g => g.SmtpGroupId.HasValue).Select(g => g.SmtpGroupId!.Value).Distinct().ToList();
        var smtpGroupNames = allSmtpGroupIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _smtpGroupRepo.FindAsync(sg => allSmtpGroupIds.Contains(sg.Id), ct))
                .ToDictionary(sg => sg.Id, sg => sg.Name);

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
                SmtpGroupId = g.SmtpGroupId,
                SmtpGroupName = g.SmtpGroupId.HasValue && smtpGroupNames.TryGetValue(g.SmtpGroupId.Value, out var n) ? n : null,
                OwnerUserId = g.UserId,
                CreatedAt = g.CreatedAt
            });
        }
        return dtos.OrderByDescending(g => g.CreatedAt);
    }

    public async Task<ContactGroupDto> CreateGroupAsync(Guid userId, CreateContactGroupDto dto, CancellationToken ct)
    {
        // Resolve creator + platform-wide sharing policy
        var creator = await _userRepo.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User", userId);
        var isAdmin = string.Equals(creator.Role, "admin", StringComparison.OrdinalIgnoreCase);

        bool sharingAllowedByPlatform = true;
        try
        {
            var sys = await _systemSettings.GetAsync(ct);
            sharingAllowedByPlatform = sys.AllowUsersToSeeSharedContacts || isAdmin;
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not load system settings — defaulting to allow share"); }

        // Decide which SmtpGroup the contact group is linked to:
        //   - Admin: can explicitly target any SmtpGroupId via dto, or null = private.
        //   - Regular user: if ShareWithTeam=true AND platform allows AND they have an SmtpGroup → inherit it.
        //                   else → private (null).
        Guid? linkedSmtpGroupId = null;
        if (isAdmin)
        {
            linkedSmtpGroupId = dto.SmtpGroupId;
        }
        else if (dto.ShareWithTeam && sharingAllowedByPlatform && creator.SmtpGroupId.HasValue)
        {
            linkedSmtpGroupId = creator.SmtpGroupId.Value;
        }

        var group = new ContactGroup
        {
            UserId = userId,
            Name = dto.Name,
            Description = dto.Description,
            SmtpGroupId = linkedSmtpGroupId,
        };
        await _groupRepo.AddAsync(group, ct);

        // Resolve linked SmtpGroup name for the response (helps UI show "Shared with: X" badge)
        string? linkedName = null;
        // Note: we can't reach SmtpGroups directly here without a repo; leave name lookup to GET endpoint.

        return new ContactGroupDto
        {
            Id = group.Id,
            Name = group.Name,
            Description = group.Description,
            SmtpGroupId = group.SmtpGroupId,
            SmtpGroupName = linkedName,
            OwnerUserId = group.UserId,
            CreatedAt = group.CreatedAt,
        };
    }

    public async Task<ContactGroupDto> UpdateGroupAsync(Guid groupId, Guid userId, CreateContactGroupDto dto, CancellationToken ct)
    {
        var group = await _groupRepo.GetByIdAsync(groupId, ct)
            ?? throw new NotFoundException("ContactGroup", groupId);
        // Only the OWNER can edit (mirrors share/delete authorisation)
        if (group.UserId != userId) throw new ForbiddenException();

        var creator = await _userRepo.GetByIdAsync(userId, ct);
        var isAdmin = string.Equals(creator?.Role, "admin", StringComparison.OrdinalIgnoreCase);

        bool sharingAllowed = true;
        try { sharingAllowed = (await _systemSettings.GetAsync(ct)).AllowUsersToSeeSharedContacts || isAdmin; }
        catch { /* allow */ }

        if (!string.IsNullOrWhiteSpace(dto.Name)) group.Name = dto.Name.Trim();
        group.Description = dto.Description;

        // Re-evaluate SmtpGroup link with same rules as Create
        Guid? linkedSmtpGroupId = group.SmtpGroupId; // default: keep existing
        if (isAdmin)
        {
            linkedSmtpGroupId = dto.SmtpGroupId; // admin explicitly sets/clears
        }
        else if (dto.ShareWithTeam && sharingAllowed && creator?.SmtpGroupId.HasValue == true)
        {
            linkedSmtpGroupId = creator.SmtpGroupId.Value;
        }
        else if (!dto.ShareWithTeam)
        {
            linkedSmtpGroupId = null; // user explicitly made it private
        }
        group.SmtpGroupId = linkedSmtpGroupId;

        await _groupRepo.UpdateAsync(group, ct);

        var count = await _contactRepo.CountAsync(c => c.GroupId == group.Id && c.IsActive, ct);
        string? linkedName = null;
        if (group.SmtpGroupId.HasValue)
        {
            var sg = await _smtpGroupRepo.GetByIdAsync(group.SmtpGroupId.Value, ct);
            linkedName = sg?.Name;
        }
        return new ContactGroupDto
        {
            Id = group.Id,
            Name = group.Name,
            Description = group.Description,
            ContactCount = count,
            SmtpGroupId = group.SmtpGroupId,
            SmtpGroupName = linkedName,
            OwnerUserId = group.UserId,
            CreatedAt = group.CreatedAt,
        };
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
