using MarketingApp.Application.DTOs;

namespace MarketingApp.Application.Interfaces;

public interface ITemplateService
{
    Task<IEnumerable<TemplateDto>> GetAllAsync(Guid userId, string? channel, CancellationToken ct);
    Task<TemplateDto> GetByIdAsync(Guid id, Guid userId, CancellationToken ct);
    Task<TemplateDto> CreateAsync(Guid userId, CreateTemplateDto dto, CancellationToken ct);
    Task<TemplateDto> UpdateAsync(Guid id, Guid userId, UpdateTemplateDto dto, CancellationToken ct);
    Task DeleteAsync(Guid id, Guid userId, CancellationToken ct);
    Task<string> PreviewAsync(Guid id, Guid userId, Dictionary<string, string> sampleData, CancellationToken ct);
    Task<TemplateDto> ToggleShareAsync(Guid id, Guid userId, bool isShared, CancellationToken ct);
    Task<TemplateDto> UpdateShareAsync(Guid id, Guid userId, UpdateTemplateShareDto dto, CancellationToken ct);
}
