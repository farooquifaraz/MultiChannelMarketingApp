using MarketingApp.Application.DTOs;

namespace MarketingApp.Application.Interfaces;

/// <summary>Per-user brand kit CRUD (P3.2). Used by the Banner Studio to brand generated images.</summary>
public interface IBrandKitService
{
    Task<IEnumerable<BrandKitDto>> ListMineAsync(Guid userId, CancellationToken ct = default);
    Task<BrandKitDto> CreateAsync(Guid userId, CreateBrandKitDto dto, CancellationToken ct = default);
    Task<BrandKitDto> UpdateAsync(Guid userId, Guid id, UpdateBrandKitDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid userId, Guid id, CancellationToken ct = default);
}
