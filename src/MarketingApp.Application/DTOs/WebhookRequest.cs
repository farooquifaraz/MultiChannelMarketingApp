namespace MarketingApp.Application.DTOs;

/// <summary>
/// HTTP-agnostic view of an inbound webhook request, populated by the API controller
/// after buffering the raw body. Keeps the Application layer free of Microsoft.AspNetCore types.
/// </summary>
public sealed record WebhookRequest
{
    /// <summary>Raw body bytes — providers compute HMAC over this, so don't decode/re-encode.</summary>
    public byte[] RawBody { get; init; } = Array.Empty<byte>();

    /// <summary>Decoded UTF-8 body for convenience (parsers usually want a string).</summary>
    public string BodyText { get; init; } = string.Empty;

    /// <summary>All request headers as a case-insensitive dictionary.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Query string params (Mailgun puts signature data here in older webhook versions).</summary>
    public IReadOnlyDictionary<string, string> Query { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
