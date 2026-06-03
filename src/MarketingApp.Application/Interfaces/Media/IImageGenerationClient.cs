namespace MarketingApp.Application.Interfaces.Media;

/// <summary>
/// Strategy contract for AI image generation (Phase 3) — mirrors IAiClient.
/// Implementations:
///   - MockImageGenerationClient (offline placeholder; works with zero keys, used for dev/demo)
///   - DalleImageClient          (OpenAI Images / DALL·E 3)
///   - (future) StabilityImageClient, IdeogramImageClient, ReplicateImageClient
/// Adding a new provider = drop one IImageGenerationClient + 1 DI line. No factory change required.
/// </summary>
public interface IImageGenerationClient
{
    /// <summary>Provider key — matched against SystemSettings.ImageProvider. e.g. "mock", "dalle".</summary>
    string Provider { get; }

    Task<ImageGenerationResult> GenerateAsync(ImageGenerationRequest request, CancellationToken ct);
}

public sealed record ImageGenerationRequest(
    string Prompt,
    int Width,
    int Height,
    string Model,
    string? ApiKey,
    string? BaseUrl,
    int TimeoutSeconds);

public sealed record ImageGenerationResult(
    bool IsSuccess,
    string? ImageUrl,
    int Width,
    int Height,
    string Provider,
    TimeSpan Latency,
    string? Error);

public interface IImageGenerationClientFactory
{
    /// <summary>Returns the client registered for the given provider key, or null if none / disabled.</summary>
    IImageGenerationClient? Resolve(string provider);
}
