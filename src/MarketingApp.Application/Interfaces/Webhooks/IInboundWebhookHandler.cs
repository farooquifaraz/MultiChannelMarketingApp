using MarketingApp.Application.DTOs;

namespace MarketingApp.Application.Interfaces.Webhooks;

/// <summary>
/// Strategy contract for inbound provider webhooks (Day 7 G2).
/// Each provider implementation owns: signature verification + payload parsing.
/// Adding a new provider = drop one class + 1 DI line. No controller change.
///
/// IMPORTANT: this interface lives in the Application layer so it stays HTTP-agnostic.
/// The API controller is responsible for buffering the raw body + extracting headers
/// from HttpRequest, then passing a plain <see cref="WebhookRequest"/> to the handler.
/// </summary>
public interface IInboundWebhookHandler
{
    /// <summary>Provider key — matched against the `{provider}` route segment. e.g. "sendgrid", "brevo", "mailgun".</summary>
    string Provider { get; }

    /// <summary>
    /// Verify the request signature using the per-group secret. MUST be called BEFORE Parse.
    /// </summary>
    Task<bool> VerifySignatureAsync(WebhookRequest request, string secret, CancellationToken ct);

    /// <summary>
    /// Parse the provider's payload into a normalized list of events.
    /// Handlers MUST NOT mutate state — they're pure parsers. Side effects happen in <c>IWebhookProcessor</c>.
    /// </summary>
    Task<IReadOnlyList<NormalizedWebhookEvent>> ParseAsync(WebhookRequest request, CancellationToken ct);
}
