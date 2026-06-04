using MarketingApp.Application.Interfaces.Media;

namespace MarketingApp.Infrastructure.Services.Media;

/// <summary>
/// Pollinations.ai image provider (Phase 3 / P3.3) — **completely FREE, no API key required**.
/// It serves a generated image directly from a URL, so we just build that URL and return it; the
/// browser fetches the image. Great default "real" provider when no paid key is configured.
/// </summary>
public class PollinationsImageClient : IImageGenerationClient
{
    public string Provider => "pollinations";

    public Task<ImageGenerationResult> GenerateAsync(ImageGenerationRequest request, CancellationToken ct)
    {
        var url = BuildUrl(request.Prompt, request.Width, request.Height, request.BaseUrl);
        return Task.FromResult(new ImageGenerationResult(
            IsSuccess: true,
            ImageUrl: url,
            Width: request.Width,
            Height: request.Height,
            Provider: Provider,
            Latency: TimeSpan.Zero,
            Error: null));
    }

    /// <summary>Builds the Pollinations image URL. Pure + deterministic → unit-testable.</summary>
    internal static string BuildUrl(string prompt, int width, int height, string? baseUrl = null)
    {
        var root = string.IsNullOrWhiteSpace(baseUrl) ? "https://image.pollinations.ai" : baseUrl!.TrimEnd('/');
        var encoded = Uri.EscapeDataString(prompt?.Trim() ?? string.Empty);
        return $"{root}/prompt/{encoded}?width={width}&height={height}&nologo=true";
    }
}
