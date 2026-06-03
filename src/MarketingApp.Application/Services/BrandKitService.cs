using System.Text.RegularExpressions;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Domain.Entities;
using MarketingApp.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Application.Services;

/// <summary>Per-user brand kit CRUD (P3.2). Enforces a single default kit per user.</summary>
public class BrandKitService : IBrandKitService
{
    private readonly IGenericRepository<BrandKit> _repo;
    private readonly ILogger<BrandKitService> _logger;

    public BrandKitService(IGenericRepository<BrandKit> repo, ILogger<BrandKitService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<IEnumerable<BrandKitDto>> ListMineAsync(Guid userId, CancellationToken ct = default)
    {
        var kits = await _repo.FindAsync(k => k.UserId == userId, ct);
        return kits.OrderByDescending(k => k.IsDefault).ThenBy(k => k.Name).Select(ToDto).ToList();
    }

    public async Task<BrandKitDto> CreateAsync(Guid userId, CreateBrandKitDto dto, CancellationToken ct = default)
    {
        Validate(dto.Name, dto.PrimaryColor, dto.SecondaryColor, dto.AccentColor);
        var kit = new BrandKit
        {
            UserId = userId,
            Name = dto.Name.Trim(),
            LogoUrl = string.IsNullOrWhiteSpace(dto.LogoUrl) ? null : dto.LogoUrl,
            PrimaryColor = dto.PrimaryColor.Trim(),
            SecondaryColor = string.IsNullOrWhiteSpace(dto.SecondaryColor) ? null : dto.SecondaryColor!.Trim(),
            AccentColor = string.IsNullOrWhiteSpace(dto.AccentColor) ? null : dto.AccentColor!.Trim(),
            FontFamily = string.IsNullOrWhiteSpace(dto.FontFamily) ? "Inter" : dto.FontFamily.Trim(),
            IsDefault = dto.IsDefault,
        };
        await _repo.AddAsync(kit, ct);
        if (kit.IsDefault) await ClearOtherDefaultsAsync(userId, kit.Id, ct);
        _logger.LogInformation("Brand kit {Id} created for user {UserId}", kit.Id, userId);
        return ToDto(kit);
    }

    public async Task<BrandKitDto> UpdateAsync(Guid userId, Guid id, UpdateBrandKitDto dto, CancellationToken ct = default)
    {
        Validate(dto.Name, dto.PrimaryColor, dto.SecondaryColor, dto.AccentColor);
        var kit = await _repo.GetByIdAsync(id, ct) ?? throw new NotFoundException("BrandKit", id);
        if (kit.UserId != userId) throw new ForbiddenException();

        kit.Name = dto.Name.Trim();
        kit.LogoUrl = string.IsNullOrWhiteSpace(dto.LogoUrl) ? null : dto.LogoUrl;
        kit.PrimaryColor = dto.PrimaryColor.Trim();
        kit.SecondaryColor = string.IsNullOrWhiteSpace(dto.SecondaryColor) ? null : dto.SecondaryColor!.Trim();
        kit.AccentColor = string.IsNullOrWhiteSpace(dto.AccentColor) ? null : dto.AccentColor!.Trim();
        kit.FontFamily = string.IsNullOrWhiteSpace(dto.FontFamily) ? "Inter" : dto.FontFamily.Trim();
        kit.IsDefault = dto.IsDefault;
        kit.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(kit, ct);
        if (kit.IsDefault) await ClearOtherDefaultsAsync(userId, kit.Id, ct);
        return ToDto(kit);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var kit = await _repo.GetByIdAsync(id, ct) ?? throw new NotFoundException("BrandKit", id);
        if (kit.UserId != userId) throw new ForbiddenException();
        await _repo.DeleteAsync(kit, ct);
    }

    private async Task ClearOtherDefaultsAsync(Guid userId, Guid keepId, CancellationToken ct)
    {
        var others = await _repo.FindAsync(k => k.UserId == userId && k.IsDefault && k.Id != keepId, ct);
        foreach (var o in others)
        {
            o.IsDefault = false;
            o.UpdatedAt = DateTime.UtcNow;
            await _repo.UpdateAsync(o, ct);
        }
    }

    private static void Validate(string name, string primary, string? secondary, string? accent)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new AppValidationException("Brand kit name is required.");
        if (!IsValidHexColor(primary))
            throw new AppValidationException("Primary color must be a hex value like #4f46e5.");
        if (!string.IsNullOrWhiteSpace(secondary) && !IsValidHexColor(secondary!))
            throw new AppValidationException("Secondary color must be a hex value like #4f46e5.");
        if (!string.IsNullOrWhiteSpace(accent) && !IsValidHexColor(accent!))
            throw new AppValidationException("Accent color must be a hex value like #4f46e5.");
    }

    /// <summary>True for #RGB / #RRGGBB hex colors. Pure → testable.</summary>
    internal static bool IsValidHexColor(string value) =>
        !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value.Trim(), "^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$");

    private static BrandKitDto ToDto(BrandKit k) => new()
    {
        Id = k.Id,
        Name = k.Name,
        LogoUrl = k.LogoUrl,
        PrimaryColor = k.PrimaryColor,
        SecondaryColor = k.SecondaryColor,
        AccentColor = k.AccentColor,
        FontFamily = k.FontFamily,
        IsDefault = k.IsDefault,
        CreatedAt = k.CreatedAt,
    };
}
