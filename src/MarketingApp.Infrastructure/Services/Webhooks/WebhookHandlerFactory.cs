using MarketingApp.Application.Interfaces.Webhooks;

namespace MarketingApp.Infrastructure.Services.Webhooks;

/// <summary>
/// Resolves an inbound webhook handler by provider key (Day 7 G2).
/// Pulls all registered <see cref="IInboundWebhookHandler"/> instances from DI and indexes them by Provider.
/// Adding a new provider = register the handler in DI, nothing else.
/// </summary>
public class WebhookHandlerFactory : IWebhookHandlerFactory
{
    private readonly IReadOnlyDictionary<string, IInboundWebhookHandler> _byProvider;

    public WebhookHandlerFactory(IEnumerable<IInboundWebhookHandler> handlers)
    {
        _byProvider = handlers.ToDictionary(h => h.Provider, h => h, StringComparer.OrdinalIgnoreCase);
    }

    public IInboundWebhookHandler? Resolve(string provider) =>
        !string.IsNullOrWhiteSpace(provider) && _byProvider.TryGetValue(provider, out var h) ? h : null;
}
