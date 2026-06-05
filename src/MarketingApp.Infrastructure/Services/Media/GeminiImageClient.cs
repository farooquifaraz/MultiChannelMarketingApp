using System.Diagnostics;
using System.Text;
using System.Text.Json;
using MarketingApp.Application.Interfaces.Media;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Infrastructure.Services.Media;

/// <summary>
/// Google Gemini image generation (Phase 3 / P3.3 + P3.4). Uses the Generative Language API
/// (generateContent with an IMAGE response modality). Default model is **Nano Banana**
/// (gemini-2.5-flash-image) — Google's image model with a generous free tier on AI Studio. Returns
/// the inline base64 image as a data-URI. Activates when ImageProvider="gemini" + key.
/// </summary>
public class GeminiImageClient : IImageGenerationClient
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<GeminiImageClient> _logger;

    public GeminiImageClient(IHttpClientFactory httpFactory, ILogger<GeminiImageClient> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public string Provider => "gemini";

    public async Task<ImageGenerationResult> GenerateAsync(ImageGenerationRequest request, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(request.ApiKey))
            return Fail("Gemini API key missing — set it in admin Integrations.", request, sw);

        try
        {
            var client = _httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(request.TimeoutSeconds <= 0 ? 60 : request.TimeoutSeconds);
            var baseUrl = string.IsNullOrWhiteSpace(request.BaseUrl)
                ? "https://generativelanguage.googleapis.com" : request.BaseUrl!.TrimEnd('/');
            // "Nano Banana" = gemini-2.5-flash-image (current default image model).
            var model = string.IsNullOrWhiteSpace(request.Model) ? "gemini-2.5-flash-image" : request.Model;

            // The native image models (gemini-2.5-flash-image) return an image with a plain
            // generateContent call — exactly like the google-generativeai SDK. Only the older
            // gemini-2.0-flash-preview-image-generation needs an explicit responseModalities.
            var needsModalities = model.Contains("2.0", StringComparison.OrdinalIgnoreCase)
                || model.Contains("preview-image-generation", StringComparison.OrdinalIgnoreCase);
            object payload = needsModalities
                ? new
                {
                    contents = new[] { new { parts = new[] { new { text = request.Prompt } } } },
                    generationConfig = new { responseModalities = new[] { "TEXT", "IMAGE" } },
                }
                : new
                {
                    contents = new[] { new { parts = new[] { new { text = request.Prompt } } } },
                };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var resp = await client.PostAsync($"{baseUrl}/v1beta/models/{model}:generateContent?key={request.ApiKey}", content, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini image failed: {Status} {Body}", resp.StatusCode, body);
                return Fail(FriendlyError((int)resp.StatusCode, body), request, sw);
            }

            var dataUri = ParseInlineImage(body);
            if (string.IsNullOrEmpty(dataUri))
                return Fail("Gemini response had no image data.", request, sw);

            sw.Stop();
            return new ImageGenerationResult(true, dataUri, request.Width, request.Height, Provider, sw.Elapsed, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini image threw");
            return Fail(ex.Message, request, sw);
        }
    }

    /// <summary>Extracts candidates[0].content.parts[].inlineData → data-URI. Pure → testable.</summary>
    internal static string? ParseInlineImage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("candidates", out var cands) || cands.GetArrayLength() == 0)
                return null;
            if (!cands[0].TryGetProperty("content", out var contentEl) || !contentEl.TryGetProperty("parts", out var parts))
                return null;
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("inlineData", out var inline) || part.TryGetProperty("inline_data", out inline))
                {
                    var mime = inline.TryGetProperty("mimeType", out var m) ? m.GetString()
                             : inline.TryGetProperty("mime_type", out var m2) ? m2.GetString() : "image/png";
                    var data = inline.TryGetProperty("data", out var d) ? d.GetString() : null;
                    if (!string.IsNullOrEmpty(data)) return $"data:{mime};base64,{data}";
                }
            }
        }
        catch { /* fall through */ }
        return null;
    }

    /// <summary>
    /// Turns a Gemini API error into a clear, actionable message. Image generation needs billing on
    /// the Google project (free-tier quota is 0), so a 429/403 gets an explicit hint. Pure → testable.
    /// </summary>
    internal static string FriendlyError(int statusCode, string body)
    {
        var apiMsg = ExtractApiError(body);
        return statusCode switch
        {
            429 or 403 => $"Gemini image generation needs billing enabled on your Google Cloud project — the free tier limit for image models is 0. Enable billing in Google AI Studio, or switch to Hugging Face (free) / the built-in placeholder. (Provider said: {apiMsg})",
            404 => $"Gemini image model not found for this key/API version — check the model name in Integrations. (Provider said: {apiMsg})",
            401 => "Gemini rejected the API key (401). Re-check the key in Integrations.",
            _ => $"Gemini error {statusCode}: {apiMsg}",
        };
    }

    /// <summary>Extracts error.message from a Google API error body (trimmed). Pure → testable.</summary>
    internal static string ExtractApiError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err) && err.TryGetProperty("message", out var m))
            {
                var s = m.GetString() ?? "";
                return s.Length > 200 ? s[..200] + "…" : s;
            }
        }
        catch { /* not json */ }
        return body.Length > 120 ? body[..120] + "…" : body;
    }

    private ImageGenerationResult Fail(string error, ImageGenerationRequest req, Stopwatch sw)
    {
        sw.Stop();
        return new ImageGenerationResult(false, null, req.Width, req.Height, Provider, sw.Elapsed, error);
    }
}
