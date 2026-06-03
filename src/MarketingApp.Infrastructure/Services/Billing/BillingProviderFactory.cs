using MarketingApp.Application.Interfaces.Billing;

namespace MarketingApp.Infrastructure.Services.Billing;

/// <summary>Resolves an IBillingProvider by provider key (Phase 2 / P2.2) — mirrors AiClientFactory.
/// New providers register themselves in DI; no factory change required.</summary>
public class BillingProviderFactory : IBillingProviderFactory
{
    private readonly IReadOnlyDictionary<string, IBillingProvider> _byProvider;

    public BillingProviderFactory(IEnumerable<IBillingProvider> providers)
    {
        _byProvider = providers.ToDictionary(p => p.Provider, p => p, StringComparer.OrdinalIgnoreCase);
    }

    public IBillingProvider? Resolve(string provider) =>
        !string.IsNullOrWhiteSpace(provider) && _byProvider.TryGetValue(provider, out var p) ? p : null;
}
