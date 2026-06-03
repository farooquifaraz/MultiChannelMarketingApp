using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Application.Interfaces.Media;
using MarketingApp.Domain.Entities;
using MarketingApp.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Application.Services;

/// <summary>
/// AI image / banner generation (Phase 3). Resolves the configured provider (mock by default → works
/// keyless), persists each generation as a GeneratedAsset, and lists a user's recent assets. Credit
/// metering is recorded on the asset but not enforced yet (mirrors the quota-off approach).
/// </summary>
public class ImageGenerationService : IImageGenerationService
{
    private readonly IImageGenerationClientFactory _factory;
    private readonly ISystemSettingsService _settings;
    private readonly IGenericRepository<GeneratedAsset> _repo;
    private readonly IAuditService _audit;
    private readonly ILogger<ImageGenerationService> _logger;

    private const int MaxPromptLength = 1000;

    public ImageGenerationService(
        IImageGenerationClientFactory factory,
        ISystemSettingsService settings,
        IGenericRepository<GeneratedAsset> repo,
        IAuditService audit,
        ILogger<ImageGenerationService> logger)
    {
        _factory = factory;
        _settings = settings;
        _repo = repo;
        _audit = audit;
        _logger = logger;
    }

    private static readonly (string Token, int W, int H, string Label)[] Sizes =
    {
        ("1024x1024", 1024, 1024, "Square — Instagram post"),
        ("1024x1792", 1024, 1792, "Portrait — Story / Reel"),
        ("1792x1024", 1792, 1024, "Landscape — LinkedIn / banner"),
        ("1080x1080", 1080, 1080, "Square — social (1080)"),
        ("1200x628",  1200, 628,  "Wide — link preview / ad"),
    };

    public IEnumerable<ImageSizeOptionDto> SizeOptions() =>
        Sizes.Select(s => new ImageSizeOptionDto { Token = s.Token, Width = s.W, Height = s.H, Label = s.Label });

    public async Task<GeneratedAssetDto> GenerateAsync(Guid userId, GenerateImageDto dto, CancellationToken ct = default)
    {
        var prompt = dto.Prompt?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(prompt))
            throw new AppValidationException("Prompt is required.");
        if (prompt.Length > MaxPromptLength)
            throw new AppValidationException($"Prompt must be {MaxPromptLength} characters or fewer.");

        var (w, h) = ParseSize(dto.Size);

        var settings = await _settings.GetAsync(ct);
        var providerKey = string.IsNullOrWhiteSpace(settings.ImageProvider) ? "mock" : settings.ImageProvider;
        if (string.Equals(providerKey, "disabled", StringComparison.OrdinalIgnoreCase))
            throw new AppValidationException("Image generation is disabled. Enable a provider in admin settings.");

        // Resolve; fall back to mock so the studio always produces something even if misconfigured.
        var client = _factory.Resolve(providerKey) ?? _factory.Resolve("mock");
        if (client is null)
            throw new AppValidationException("No image generation provider is available.");

        var apiKey = string.Equals(client.Provider, "mock", StringComparison.OrdinalIgnoreCase)
            ? null
            : await _settings.GetRawImageApiKeyAsync(ct);

        var asset = new GeneratedAsset
        {
            UserId = userId,
            Prompt = prompt,
            Provider = client.Provider,
            Size = $"{w}x{h}",
            Width = w,
            Height = h,
            Status = "pending",
            CreditCost = 1,
        };

        var request = new ImageGenerationRequest(prompt, w, h, settings.ImageModel, apiKey, settings.ImageBaseUrl, 60);
        try
        {
            var result = await client.GenerateAsync(request, ct);
            if (result.IsSuccess && !string.IsNullOrEmpty(result.ImageUrl))
            {
                asset.Status = "completed";
                asset.ImageUrl = result.ImageUrl;
            }
            else
            {
                asset.Status = "failed";
                asset.ErrorMessage = result.Error ?? "Generation failed.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Image generation threw for user {UserId}", userId);
            asset.Status = "failed";
            asset.ErrorMessage = ex.Message;
        }

        await _repo.AddAsync(asset, ct);
        await _audit.LogAsync(userId, "ImageGenerated", "GeneratedAsset", asset.Id, ct: ct);
        return ToDto(asset);
    }

    public async Task<IEnumerable<GeneratedAssetDto>> ListMineAsync(Guid userId, int take = 50, CancellationToken ct = default)
    {
        var mine = await _repo.FindAsync(a => a.UserId == userId, ct);
        return mine
            .OrderByDescending(a => a.CreatedAt)
            .Take(take <= 0 ? 50 : Math.Min(take, 200))
            .Select(ToDto)
            .ToList();
    }

    /// <summary>
    /// Parses a "WxH" size token against the allowed set. Unknown tokens fall back to 1024x1024.
    /// Pure + deterministic → unit-testable.
    /// </summary>
    internal static (int Width, int Height) ParseSize(string? token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            var match = Sizes.FirstOrDefault(s => string.Equals(s.Token, token.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match.Token is not null) return (match.W, match.H);
        }
        return (1024, 1024);
    }

    private static GeneratedAssetDto ToDto(GeneratedAsset a) => new()
    {
        Id = a.Id,
        Prompt = a.Prompt,
        Provider = a.Provider,
        Size = a.Size,
        Width = a.Width,
        Height = a.Height,
        Status = a.Status,
        ImageUrl = a.ImageUrl,
        CreditCost = a.CreditCost,
        ErrorMessage = a.ErrorMessage,
        CreatedAt = a.CreatedAt,
    };
}
