using System.Text.Json;

namespace MarketingApp.Infrastructure.Services.Media;

/// <summary>
/// Shared parser that pulls a human-readable message out of an image provider's error body. Both
/// OpenAI and Google use {"error":{"message":...}}; others may use a plain "error" string. Falls back
/// to a trimmed raw body. Pure → unit-testable. Lets the UI show the real reason (quota, billing,
/// invalid model, verification required) instead of a bare HTTP status code.
/// </summary>
public static class MediaErrorHelper
{
    public static string Extract(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return "no response body";
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var err))
            {
                if (err.ValueKind == JsonValueKind.Object && err.TryGetProperty("message", out var m))
                    return Trim(m.GetString());
                if (err.ValueKind == JsonValueKind.String)
                    return Trim(err.GetString());
            }
            if (root.TryGetProperty("message", out var topMsg))
                return Trim(topMsg.GetString());
        }
        catch { /* not json */ }
        return Trim(body);
    }

    private static string Trim(string? s)
    {
        s = (s ?? string.Empty).Trim();
        return s.Length > 220 ? s[..220] + "…" : s;
    }
}
