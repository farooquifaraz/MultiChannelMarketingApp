namespace MarketingApp.Application.Interfaces.Webhooks;

/// <summary>
/// Resolves an inbound webhook handler by provider key. Reads from DI.
/// New provider = register one IInboundWebhookHandler — no factory change needed.
/// </summary>
public interface IWebhookHandlerFactory
{
    IInboundWebhookHandler? Resolve(string provider);
}
