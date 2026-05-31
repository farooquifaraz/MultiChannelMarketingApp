using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Domain.Entities;
using MarketingApp.Domain.Exceptions;

namespace MarketingApp.Application.Services;

public class TemplateService : ITemplateService
{
    private readonly IGenericRepository<MessageTemplate> _templateRepo;
    private readonly IGenericRepository<User> _userRepo;
    private readonly IGenericRepository<TemplateSharedGroup> _shareGroupsRepo;
    private readonly IGenericRepository<TemplateSharedUser> _shareUsersRepo;
    private readonly ISystemSettingsService _systemSettings;
    private readonly ICacheService _cache;
    private readonly IAuditService _audit;
    private readonly IMapper _mapper;
    private readonly ILogger<TemplateService> _logger;

    public TemplateService(
        IGenericRepository<MessageTemplate> templateRepo,
        IGenericRepository<User> userRepo,
        IGenericRepository<TemplateSharedGroup> shareGroupsRepo,
        IGenericRepository<TemplateSharedUser> shareUsersRepo,
        ISystemSettingsService systemSettings,
        ICacheService cache,
        IAuditService audit,
        IMapper mapper,
        ILogger<TemplateService> logger)
    {
        _templateRepo = templateRepo;
        _userRepo = userRepo;
        _shareGroupsRepo = shareGroupsRepo;
        _shareUsersRepo = shareUsersRepo;
        _systemSettings = systemSettings;
        _cache = cache;
        _audit = audit;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<IEnumerable<TemplateDto>> GetAllAsync(Guid userId, string? channel, CancellationToken ct)
    {
        // 1. Always include the user's OWN templates
        var ownTemplates = (await _templateRepo.FindAsync(t => t.UserId == userId && t.IsActive, ct)).ToList();

        // 2. Compute the visible SHARED templates (owned by admins) based on each template's ShareScope:
        //    - "global" → visible to all users
        //    - "groups" → visible only if the user belongs to one of the linked SmtpGroups
        //    - "users"  → visible only if the user is explicitly listed
        var sharedTemplates = new List<MessageTemplate>();
        try
        {
            var sys = await _systemSettings.GetAsync(ct);
            if (sys.AllowUsersToSeeSharedTemplates)
            {
                var adminIds = (await _userRepo.FindAsync(u => u.Role == "admin", ct)).Select(u => u.Id).ToHashSet();
                if (adminIds.Count > 0)
                {
                    var currentUser = await _userRepo.GetByIdAsync(userId, ct);
                    var userSmtpGroupId = currentUser?.SmtpGroupId;

                    // Load all admin-shared templates with their junction tables (Include)
                    var query = _templateRepo.Query()
                        .Include(t => t.SharedWithGroups)
                        .Include(t => t.SharedWithUsers)
                        .Where(t => t.IsShared && t.IsActive && t.UserId != userId && adminIds.Contains(t.UserId));
                    var candidates = await query.ToListAsync(ct);

                    foreach (var t in candidates)
                    {
                        var scope = (t.ShareScope ?? "global").ToLowerInvariant();
                        bool visible = scope switch
                        {
                            "global" => true,
                            "groups" => userSmtpGroupId.HasValue
                                        && t.SharedWithGroups.Any(g => g.SmtpGroupId == userSmtpGroupId.Value),
                            "users"  => t.SharedWithUsers.Any(u => u.UserId == userId),
                            _ => false,
                        };
                        if (visible) sharedTemplates.Add(t);
                    }
                }
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not load shared templates"); }

        var combined = ownTemplates.Concat(sharedTemplates);
        if (!string.IsNullOrWhiteSpace(channel))
            combined = combined.Where(t => t.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase));

        return _mapper.Map<IEnumerable<TemplateDto>>(combined.OrderByDescending(t => t.UpdatedAt));
    }

    public async Task<TemplateDto> GetByIdAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var template = await _templateRepo.Query()
            .Include(t => t.SharedWithGroups)
            .Include(t => t.SharedWithUsers)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Template", id);

        // Allow the owner, or any user the template is shared with via its current scope.
        if (template.UserId != userId)
        {
            var allowed = false;
            if (template.IsShared)
            {
                var owner = await _userRepo.GetByIdAsync(template.UserId, ct);
                if (string.Equals(owner?.Role, "admin", StringComparison.OrdinalIgnoreCase))
                {
                    var scope = (template.ShareScope ?? "global").ToLowerInvariant();
                    allowed = scope switch
                    {
                        "global" => true,
                        "groups" => (await _userRepo.GetByIdAsync(userId, ct))?.SmtpGroupId is Guid sgid
                                    && template.SharedWithGroups.Any(g => g.SmtpGroupId == sgid),
                        "users" => template.SharedWithUsers.Any(u => u.UserId == userId),
                        _ => false,
                    };
                }
            }
            if (!allowed) throw new ForbiddenException();
        }

        var dto = _mapper.Map<TemplateDto>(template);
        dto.SharedWithGroupIds = template.SharedWithGroups.Select(g => g.SmtpGroupId).ToList();
        dto.SharedWithUserIds = template.SharedWithUsers.Select(u => u.UserId).ToList();
        return dto;
    }

    public async Task<TemplateDto> CreateAsync(Guid userId, CreateTemplateDto dto, CancellationToken ct)
    {
        var template = _mapper.Map<MessageTemplate>(dto);
        template.UserId = userId;
        await _templateRepo.AddAsync(template, ct);
        await InvalidateCache(userId, ct);
        await _audit.LogAsync(userId, "TemplateCreated", "Template", template.Id, ct: ct);
        return _mapper.Map<TemplateDto>(template);
    }

    public async Task<TemplateDto> UpdateAsync(Guid id, Guid userId, UpdateTemplateDto dto, CancellationToken ct)
    {
        var template = await _templateRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Template", id);
        if (template.UserId != userId) throw new ForbiddenException();

        _mapper.Map(dto, template);
        template.UpdatedAt = DateTime.UtcNow;
        await _templateRepo.UpdateAsync(template, ct);
        await InvalidateCache(userId, ct);
        await _audit.LogAsync(userId, "TemplateUpdated", "Template", id, ct: ct);
        return _mapper.Map<TemplateDto>(template);
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var template = await _templateRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Template", id);
        if (template.UserId != userId) throw new ForbiddenException();
        await _templateRepo.DeleteAsync(template, ct);
        await InvalidateCache(userId, ct);
    }

    public async Task<TemplateDto> ToggleShareAsync(Guid id, Guid userId, bool isShared, CancellationToken ct)
    {
        // Backward-compat: toggle on = share globally; toggle off = private.
        return await UpdateShareAsync(id, userId, new UpdateTemplateShareDto
        {
            IsShared = isShared,
            ShareScope = "global",
        }, ct);
    }

    public async Task<TemplateDto> UpdateShareAsync(Guid id, Guid userId, UpdateTemplateShareDto dto, CancellationToken ct)
    {
        var template = await _templateRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Template", id);
        if (template.UserId != userId) throw new ForbiddenException();

        var scope = (dto.ShareScope ?? "global").ToLowerInvariant();
        if (scope is not ("global" or "groups" or "users"))
            throw new AppValidationException(new List<string> { "ShareScope must be one of: global, groups, users." });

        template.IsShared = dto.IsShared;
        template.ShareScope = scope;
        template.UpdatedAt = DateTime.UtcNow;
        await _templateRepo.UpdateAsync(template, ct);

        // Reset junction tables atomically — delete all existing, then add the new selection.
        var existingGroups = await _shareGroupsRepo.FindAsync(g => g.TemplateId == id, ct);
        foreach (var g in existingGroups) await _shareGroupsRepo.DeleteAsync(g, ct);
        var existingUsers = await _shareUsersRepo.FindAsync(u => u.TemplateId == id, ct);
        foreach (var u in existingUsers) await _shareUsersRepo.DeleteAsync(u, ct);

        // Only persist the relevant set based on the chosen scope.
        if (dto.IsShared && scope == "groups" && dto.SharedWithGroupIds.Count > 0)
        {
            foreach (var gid in dto.SharedWithGroupIds.Distinct())
                await _shareGroupsRepo.AddAsync(new TemplateSharedGroup { TemplateId = id, SmtpGroupId = gid }, ct);
        }
        else if (dto.IsShared && scope == "users" && dto.SharedWithUserIds.Count > 0)
        {
            foreach (var uid in dto.SharedWithUserIds.Distinct())
                await _shareUsersRepo.AddAsync(new TemplateSharedUser { TemplateId = id, UserId = uid }, ct);
        }

        await InvalidateCache(userId, ct);
        await _audit.LogAsync(userId,
            dto.IsShared ? $"TemplateShared:{scope}" : "TemplateUnshared",
            "Template", id, new { scope, dto.SharedWithGroupIds, dto.SharedWithUserIds }, ct: ct);

        // Return DTO with the freshly-loaded share info
        return await GetByIdAsync(id, userId, ct);
    }

    public async Task<string> PreviewAsync(Guid id, Guid userId, Dictionary<string, string> sampleData, CancellationToken ct)
    {
        // Load with junction tables so we can verify share visibility for non-owners
        var template = await _templateRepo.Query()
            .Include(t => t.SharedWithGroups)
            .Include(t => t.SharedWithUsers)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Template", id);

        // Owner always allowed.
        if (template.UserId != userId)
        {
            // Non-owner: allowed only if the template is visible to them per its share scope.
            var allowed = false;
            if (template.IsShared)
            {
                // The owner must be an admin for shared templates to be visible (mirrors GetAllAsync).
                var owner = await _userRepo.GetByIdAsync(template.UserId, ct);
                var isOwnerAdmin = string.Equals(owner?.Role, "admin", StringComparison.OrdinalIgnoreCase);
                if (isOwnerAdmin)
                {
                    var scope = (template.ShareScope ?? "global").ToLowerInvariant();
                    if (scope == "global")
                    {
                        allowed = true;
                    }
                    else if (scope == "groups")
                    {
                        var requester = await _userRepo.GetByIdAsync(userId, ct);
                        if (requester?.SmtpGroupId is Guid sgid
                            && template.SharedWithGroups.Any(g => g.SmtpGroupId == sgid))
                            allowed = true;
                    }
                    else if (scope == "users")
                    {
                        if (template.SharedWithUsers.Any(u => u.UserId == userId))
                            allowed = true;
                    }
                }
            }
            if (!allowed) throw new ForbiddenException();
        }

        var body = template.Body;
        foreach (var kvp in sampleData)
            body = body.Replace($"{{{{{kvp.Key}}}}}", kvp.Value);
        return body;
    }

    private async Task InvalidateCache(Guid userId, CancellationToken ct)
        => await _cache.RemoveAsync(string.Format(Domain.Constants.AppConstants.CacheKeys.Templates, userId), ct);
}
