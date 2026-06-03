using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MarketingApp.Application.Interfaces.Media;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Infrastructure.Services.Media;

/// <summary>
/// OpenAI Images (DALL·E 3) provider (Phase 3). Activates when SystemSettings.ImageProvider="dalle"
/// and an API key is set. Returns the hosted image URL OpenAI provides. Until a key is configured the
/// factory resolves to the mock provider instead, so this never blocks keyless operation.
/// </summary>
public class DalleImageClient : IImageGenerationClient
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<DalleImageClient> _logger;

    public DalleImageClient(IHttpClientFactory httpFactory, ILogger<DalleImageClient> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public string Provider => "dalle";

    public async Task<ImageGenerationResult> GenerateAsync(ImageGenerationRequest request, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(request.ApiKey))
            return Fail("OpenAI API key missing — set it in admin Image Settings.", request, sw);

        try
        {
            var client = _httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(request.TimeoutSeconds <= 0 ? 60 : request.TimeoutSeconds);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey);

            var baseUrl = string.IsNullOrWhiteSpace(request.BaseUrl) ? "https://api.openai.com/v1" : request.BaseUrl!.TrimEnd('/');
            var payload = new
            {
                model = string.IsNullOrWhiteSpace(request.Model) ? "dall-e-3" : request.Model,
                prompt = request.Prompt,
                n = 1,
                size = $"{request.Width}x{request.Height}",
            };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var resp = await client.PostAsync($"{baseUrl}/images/generations", content, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("DALL·E generation failed: {Status} {Body}", resp.StatusCode, body);
                return Fail($"Provider returned {(int)resp.StatusCode}.", request, sw);
            }

            var url = ParseImageUrl(body);
            if (string.IsNullOrEmpty(url))
                return Fail("Provider response had no image url.", request, sw);

            sw.Stop();
            return new ImageGenerationResult(true, url, request.Width, request.Height, Provider, sw.Elapsed, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DALL·E generation threw");
            return Fail(ex.Message, request, sw);
        }
    }

    /// <summary>Extracts data[0].url from the OpenAI Images response. Pure + testable.</summary>
    internal static string? ParseImageUrl(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0)
            {
                var first = data[0];
                if (first.TryGetProperty("url", out var url)) return url.GetString();
                if (first.TryGetProperty("b64_json", out var b64))
                    return "data:image/png;base64," + b64.GetString();
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
