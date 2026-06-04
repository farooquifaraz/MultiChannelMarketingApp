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

            var payload = new
            {
                contents = new[] { new { parts = new[] { new { text = request.Prompt } } } },
                generationConfig = new { responseModalities = new[] { "TEXT", "IMAGE" } },
            };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var resp = await client.PostAsync($"{baseUrl}/v1beta/models/{model}:generateContent?key={request.ApiKey}", content, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini image failed: {Status} {Body}", resp.StatusCode, body);
                return Fail($"Provider returned {(int)resp.StatusCode}.", request, sw);
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

    private ImageGenerationResult Fail(string error, ImageGenerationRequest req, Stopwatch sw)
    {
        sw.Stop();
        return new ImageGenerationResult(false, null, req.Width, req.Height, Provider, sw.Elapsed, error);
    }
}
