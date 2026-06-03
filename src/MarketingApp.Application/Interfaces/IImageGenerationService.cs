using MarketingApp.Application.DTOs;

namespace MarketingApp.Application.Interfaces;

/// <summary>
/// AI image / banner generation (Phase 3). Resolves the configured provider (mock by default, so it
/// works keyless), persists each generation as a GeneratedAsset, and lists a user's recent assets.
/// Credit metering is tracked on the asset but NOT enforced yet (mirrors the quota-off approach).
/// </summary>
public interface IImageGenerationService
{
    /// <summary>Generate an image from a prompt + size token. Throws on invalid prompt/size.</summary>
    Task<GeneratedAssetDto> GenerateAsync(Guid userId, GenerateImageDto dto, CancellationToken ct = default);

    /// <summary>The user's most recent generated assets (newest first).</summary>
    Task<IEnumerable<GeneratedAssetDto>> ListMineAsync(Guid userId, int take = 50, CancellationToken ct = default);

    /// <summary>Allowed size tokens for the UI picker.</summary>
    IEnumerable<ImageSizeOptionDto> SizeOptions();
}
