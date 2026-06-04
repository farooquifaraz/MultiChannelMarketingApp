using System.Diagnostics;
using MarketingApp.Application.Interfaces.Media;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Infrastructure.Services.Media;

/// <summary>
/// Pollinations.ai image provider (Phase 3 / P3.3) — free, no key required (an optional token raises
/// the rate limit). The free anonymous endpoint is rate-limited per IP and may return a JSON error
/// instead of an image, so we fetch server-side, verify the response is really an image, and embed it
/// as a data-URI (the browser never hits Pollinations directly → no broken images). On a rate-limit /
/// error we retry once, then fail cleanly with a clear message.
/// </summary>
public class PollinationsImageClient : IImageGenerationClient
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<PollinationsImageClient> _logger;

    public PollinationsImageClient(IHttpClientFactory httpFactory, ILogger<PollinationsImageClient> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public string Provider => "pollinations";

    public async Task<ImageGenerationResult> GenerateAsync(ImageGenerationRequest request, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        // ApiKey (optional) is used as the Pollinations token for higher/unlimited limits.
        var url = BuildUrl(request.Prompt, request.Width, request.Height, request.BaseUrl, request.ApiKey);

        try
        {
            var client = _httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(request.TimeoutSeconds <= 0 ? 60 : request.TimeoutSeconds);

            for (var attempt = 1; attempt <= 2; attempt++)
            {
                var resp = await client.GetAsync(url, ct);
                var mediaType = resp.Content.Headers.ContentType?.MediaType;
                if (resp.IsSuccessStatusCode && mediaType is not null && mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
                    if (bytes.Length > 0)
                    {
                        sw.Stop();
                        var dataUri = $"data:{mediaType};base64,{Convert.ToBase64String(bytes)}";
                        return new ImageGenerationResult(true, dataUri, request.Width, request.Height, Provider, sw.Elapsed, null);
                    }
                }

                // Not an image → rate-limited / error. Log the reason, wait briefly, retry once.
                var body = await SafeReadAsync(resp, ct);
                _logger.LogWarning("Pollinations attempt {Attempt} not an image: {Status} {Body}", attempt, resp.StatusCode, body);
                if (attempt == 1) await Task.Delay(1200, ct);
            }

            sw.Stop();
            return new ImageGenerationResult(false, null, request.Width, request.Height, Provider, sw.Elapsed,
                "Pollinations free endpoint is rate-limited right now. Try again in a moment, add a Pollinations token in Integrations, or switch to another image provider (e.g. the built-in placeholder, Gemini, or Hugging Face).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pollinations image threw");
            sw.Stop();
            return new ImageGenerationResult(false, null, request.Width, request.Height, Provider, sw.Elapsed, ex.Message);
        }
    }

    private static async Task<string> SafeReadAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try { var s = await resp.Content.ReadAsStringAsync(ct); return s.Length > 300 ? s[..300] : s; }
        catch { return string.Empty; }
    }

    /// <summary>Builds the Pollinations image URL (+ optional token). Pure + deterministic → testable.</summary>
    internal static string BuildUrl(string prompt, int width, int height, string? baseUrl = null, string? token = null)
    {
        var root = string.IsNullOrWhiteSpace(baseUrl) ? "https://image.pollinations.ai" : baseUrl!.TrimEnd('/');
        var encoded = Uri.EscapeDataString(prompt?.Trim() ?? string.Empty);
        var url = $"{root}/prompt/{encoded}?width={width}&height={height}&nologo=true";
        if (!string.IsNullOrWhiteSpace(token)) url += $"&token={Uri.EscapeDataString(token!.Trim())}";
        return url;
    }
}
