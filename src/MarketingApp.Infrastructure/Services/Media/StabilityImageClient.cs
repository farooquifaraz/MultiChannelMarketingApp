using System.Diagnostics;
using System.Text.Json;
using MarketingApp.Application.Interfaces.Media;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Infrastructure.Services.Media;

/// <summary>
/// Stability AI image generation (Phase 3 / P3.3) — Stable Image Core endpoint. Paid. Returns the
/// base64 image as a data-URI. Activates when ImageProvider="stability" + key.
/// </summary>
public class StabilityImageClient : IImageGenerationClient
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<StabilityImageClient> _logger;

    public StabilityImageClient(IHttpClientFactory httpFactory, ILogger<StabilityImageClient> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public string Provider => "stability";

    public async Task<ImageGenerationResult> GenerateAsync(ImageGenerationRequest request, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(request.ApiKey))
            return Fail("Stability API key missing — set it in admin Integrations.", request, sw);

        try
        {
            var client = _httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(request.TimeoutSeconds <= 0 ? 60 : request.TimeoutSeconds);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", request.ApiKey);
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            var baseUrl = string.IsNullOrWhiteSpace(request.BaseUrl) ? "https://api.stability.ai" : request.BaseUrl!.TrimEnd('/');

            // Stable Image Core takes multipart form; aspect_ratio rather than explicit w/h.
            using var form = new MultipartFormDataContent
            {
                { new StringContent(request.Prompt), "prompt" },
                { new StringContent("png"), "output_format" },
                { new StringContent(AspectRatio(request.Width, request.Height)), "aspect_ratio" },
            };
            var resp = await client.PostAsync($"{baseUrl}/v2beta/stable-image/generate/core", form, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Stability image failed: {Status} {Body}", resp.StatusCode, body);
                return Fail($"Stability: {MediaErrorHelper.Extract(body)}", request, sw);
            }

            var dataUri = ParseImage(body);
            if (string.IsNullOrEmpty(dataUri))
                return Fail("Stability response had no image.", request, sw);

            sw.Stop();
            return new ImageGenerationResult(true, dataUri, request.Width, request.Height, Provider, sw.Elapsed, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stability image threw");
            return Fail(ex.Message, request, sw);
        }
    }

    /// <summary>Reads { image: base64 } → PNG data-URI. Pure → testable.</summary>
    internal static string? ParseImage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("image", out var img))
            {
                var b64 = img.GetString();
                if (!string.IsNullOrEmpty(b64)) return "data:image/png;base64," + b64;
            }
        }
        catch { /* fall through */ }
        return null;
    }

    /// <summary>Maps pixel dimensions to the nearest Stability aspect-ratio token. Pure → testable.</summary>
    internal static string AspectRatio(int w, int h)
    {
        if (w <= 0 || h <= 0) return "1:1";
        var r = (double)w / h;
        return r switch
        {
            >= 1.6 => "16:9",
            >= 1.2 => "3:2",
            >= 0.9 => "1:1",
            >= 0.6 => "2:3",
            _ => "9:16",
        };
    }

    private ImageGenerationResult Fail(string error, ImageGenerationRequest req, Stopwatch sw)
    {
        sw.Stop();
        return new ImageGenerationResult(false, null, req.Width, req.Height, Provider, sw.Elapsed, error);
    }
}
