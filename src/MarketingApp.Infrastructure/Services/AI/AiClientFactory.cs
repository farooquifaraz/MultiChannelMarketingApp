using MarketingApp.Application.Interfaces.AI;

namespace MarketingApp.Infrastructure.Services.AI;

/// <summary>Resolves an IAiClient by provider key (Day 7 G5). Future providers register themselves in DI — no factory change.</summary>
public class AiClientFactory : IAiClientFactory
{
    private readonly IReadOnlyDictionary<string, IAiClient> _byProvider;

    public AiClientFactory(IEnumerable<IAiClient> clients)
    {
        _byProvider = clients.ToDictionary(c => c.Provider, c => c, StringComparer.OrdinalIgnoreCase);
    }

    public IAiClient? Resolve(string provider) =>
        !string.IsNullOrWhiteSpace(provider) && _byProvider.TryGetValue(provider, out var c) ? c : null;
}
