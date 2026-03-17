using AutoMapper;
using Microsoft.Extensions.Logging;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Domain.Entities;
using MarketingApp.Domain.Exceptions;

namespace MarketingApp.Application.Services;

public class TemplateService : ITemplateService
{
    private readonly IGenericRepository<MessageTemplate> _templateRepo;
    private readonly ICacheService _cache;
    private readonly IAuditService _audit;
    private readonly IMapper _mapper;
    private readonly ILogger<TemplateService> _logger;

    public TemplateService(
        IGenericRepository<MessageTemplate> templateRepo,
        ICacheService cache,
        IAuditService audit,
        IMapper mapper,
        ILogger<TemplateService> logger)
    {
        _templateRepo = templateRepo;
        _cache = cache;
        _audit = audit;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<IEnumerable<TemplateDto>> GetAllAsync(Guid userId, string? channel, CancellationToken ct)
    {
        var cacheKey = string.Format(Domain.Constants.AppConstants.CacheKeys.Templates, userId);
        var cached = await _cache.GetAsync<IEnumerable<TemplateDto>>(cacheKey, ct);
        if (cached != null && channel == null) return cached;

        var templates = await _templateRepo.FindAsync(t => t.UserId == userId && t.IsActive, ct);
        if (!string.IsNullOrWhiteSpace(channel))
            templates = templates.Where(t => t.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase));

        var result = _mapper.Map<IEnumerable<TemplateDto>>(templates.OrderByDescending(t => t.UpdatedAt));

        if (channel == null)
            await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(Domain.Constants.AppConstants.TemplateCacheDurationMinutes), ct);

        return result;
    }

    public async Task<TemplateDto> GetByIdAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var template = await _templateRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Template", id);
        if (template.UserId != userId) throw new ForbiddenException();
        return _mapper.Map<TemplateDto>(template);
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

    public async Task<string> PreviewAsync(Guid id, Guid userId, Dictionary<string, string> sampleData, CancellationToken ct)
    {
        var template = await _templateRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Template", id);
        if (template.UserId != userId) throw new ForbiddenException();

        var body = template.Body;
        foreach (var kvp in sampleData)
            body = body.Replace($"{{{{{kvp.Key}}}}}", kvp.Value);
        return body;
    }

    private async Task InvalidateCache(Guid userId, CancellationToken ct)
        => await _cache.RemoveAsync(string.Format(Domain.Constants.AppConstants.CacheKeys.Templates, userId), ct);
}
