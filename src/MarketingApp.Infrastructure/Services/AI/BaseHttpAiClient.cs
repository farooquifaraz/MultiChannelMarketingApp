using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MarketingApp.Application.Interfaces.AI;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Infrastructure.Services.AI;

/// <summary>
/// Shared HTTP plumbing for every AI client (Day 7 G5). Handles HttpClient injection,
/// timeout wiring, latency tracking, and a uniform JSON serialization style.
/// Concrete clients implement only request body shaping + response parsing.
/// </summary>
public abstract class BaseHttpAiClient : IAiClient
{
    protected readonly IHttpClientFactory HttpClientFactory;
    protected readonly ILogger Logger;

    protected BaseHttpAiClient(IHttpClientFactory httpClientFactory, ILogger logger)
    {
        HttpClientFactory = httpClientFactory;
        Logger = logger;
    }

    public abstract string Provider { get; }

    public async Task<AiCompletion> GenerateAsync(AiRequest request, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var client = HttpClientFactory.CreateClient($"ai-{Provider}");
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, request.TimeoutSeconds));

            ConfigureAuth(client, request);

            var (url, payload) = BuildRequest(request);
            var jsonBody = JsonSerializer.Serialize(payload);
            using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            using var response = await client.PostAsync(url, content, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                Logger.LogError("[{Provider}] AI call failed: {Status} {Body}", Provider, response.StatusCode, Truncate(body, 500));
                var status = (int)response.StatusCode;
                var humanMsg = ExtractProviderErrorMessage(body);
                // Friendly, actionable messages for the common cases.
                var message = status switch
                {
                    429 => $"{Provider} rate limit / quota exceeded. Wait a minute and retry, or switch provider in Settings → AI Assistant (Groq has a free, faster tier).",
                    401 or 403 => $"{Provider} rejected the API key (HTTP {status}). Check the key in Settings → AI Assistant.",
                    >= 500 => $"{Provider} is having a server issue (HTTP {status}). Please retry shortly.",
                    _ => $"{Provider} error (HTTP {status}): {humanMsg}",
                };
                throw new InvalidOperationException(message);
            }

            var parsed = ParseResponse(body);
            return parsed with { Latency = sw.Elapsed, Provider = Provider };
        }
        catch (TaskCanceledException tex)
        {
            Logger.LogWarning(tex, "[{Provider}] AI call timed out after {Seconds}s", Provider, request.TimeoutSeconds);
            throw new InvalidOperationException($"AI provider '{Provider}' timed out.", tex);
        }
    }

    protected abstract void ConfigureAuth(HttpClient client, AiRequest request);
    protected abstract (string Url, object Payload) BuildRequest(AiRequest request);
    protected abstract AiCompletion ParseResponse(string body);

    protected static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max];

    /// <summary>Pull a human-readable message out of a provider's JSON error body (e.g. {"error":{"message":"..."}}).</summary>
    private static string ExtractProviderErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return "Unknown error.";
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var err))
            {
                if (err.ValueKind == System.Text.Json.JsonValueKind.Object && err.TryGetProperty("message", out var m))
                    return m.GetString() ?? "Unknown error.";
                if (err.ValueKind == System.Text.Json.JsonValueKind.String)
                    return err.GetString() ?? "Unknown error.";
            }
            if (root.TryGetProperty("message", out var topMsg))
                return topMsg.GetString() ?? "Unknown error.";
        }
        catch { /* not JSON */ }
        return Truncate(body, 200);
    }
}
