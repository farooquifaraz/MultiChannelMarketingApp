using MarketingApp.Application.Interfaces.Media;

namespace MarketingApp.Infrastructure.Services.Media;

/// <summary>Resolves an IImageGenerationClient by provider key (Phase 3) — mirrors AiClientFactory.
/// New providers register themselves in DI; no factory change required.</summary>
public class ImageGenerationClientFactory : IImageGenerationClientFactory
{
    private readonly IReadOnlyDictionary<string, IImageGenerationClient> _byProvider;

    public ImageGenerationClientFactory(IEnumerable<IImageGenerationClient> clients)
    {
        _byProvider = clients.ToDictionary(c => c.Provider, c => c, StringComparer.OrdinalIgnoreCase);
    }

    public IImageGenerationClient? Resolve(string provider) =>
        !string.IsNullOrWhiteSpace(provider) && _byProvider.TryGetValue(provider, out var c) ? c : null;
}
