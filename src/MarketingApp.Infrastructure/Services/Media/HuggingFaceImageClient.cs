using System.Diagnostics;
using System.Text;
using System.Text.Json;
using MarketingApp.Application.Interfaces.Media;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Infrastructure.Services.Media;

/// <summary>
/// Hugging Face Inference API image generation (Phase 3 / P3.3) — **free tier** with a free HF token.
/// Posts the prompt to a text-to-image model and gets raw image bytes back, returned as a data-URI.
/// Activates when ImageProvider="huggingface" + token. Model defaults to a fast SDXL-class model.
/// </summary>
public class HuggingFaceImageClient : IImageGenerationClient
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<HuggingFaceImageClient> _logger;

    public HuggingFaceImageClient(IHttpClientFactory httpFactory, ILogger<HuggingFaceImageClient> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public string Provider => "huggingface";

    public async Task<ImageGenerationResult> GenerateAsync(ImageGenerationRequest request, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(request.ApiKey))
            return Fail("Hugging Face token missing — set it in admin Integrations.", request, sw);

        try
        {
            var client = _httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(request.TimeoutSeconds <= 0 ? 90 : request.TimeoutSeconds);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", request.ApiKey);
            var baseUrl = string.IsNullOrWhiteSpace(request.BaseUrl)
                ? "https://api-inference.huggingface.co" : request.BaseUrl!.TrimEnd('/');
            var model = string.IsNullOrWhiteSpace(request.Model) ? "black-forest-labs/FLUX.1-schnell" : request.Model;

            var payload = new { inputs = request.Prompt };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var resp = await client.PostAsync($"{baseUrl}/models/{model}", content, ct);

            var mediaTypeResp = resp.Content.Headers.ContentType?.MediaType ?? "";
            if (!resp.IsSuccessStatusCode || !mediaTypeResp.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                var err = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("HuggingFace image failed: {Status} {Body}", resp.StatusCode, err);
                return Fail($"Hugging Face: {MediaErrorHelper.Extract(err)} (HF free serverless inference is being deprecated for many models — try OpenAI gpt-image-1, enable Gemini billing, or use the built-in placeholder.)", request, sw);
            }

            // Success returns raw image bytes (image/png|jpeg).
            var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length == 0)
                return Fail("Hugging Face returned no image bytes.", request, sw);
            var mime = resp.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            var dataUri = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";

            sw.Stop();
            return new ImageGenerationResult(true, dataUri, request.Width, request.Height, Provider, sw.Elapsed, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HuggingFace image threw");
            return Fail(ex.Message, request, sw);
        }
    }

    private ImageGenerationResult Fail(string error, ImageGenerationRequest req, Stopwatch sw)
    {
        sw.Stop();
        return new ImageGenerationResult(false, null, req.Width, req.Height, Provider, sw.Elapsed, error);
    }
}
