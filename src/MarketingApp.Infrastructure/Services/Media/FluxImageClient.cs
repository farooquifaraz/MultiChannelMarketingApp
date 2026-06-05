using System.Diagnostics;
using System.Text;
using System.Text.Json;
using MarketingApp.Application.Interfaces.Media;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Infrastructure.Services.Media;

/// <summary>
/// Black Forest Labs FLUX image provider (Phase 3 / P3.7). Async API: submit a generation, then poll
/// the returned polling_url until Ready, then fetch the delivered image and embed it as a data-URI
/// (the delivery URL is short-lived). Header auth is "x-key". Activates when ImageProvider="flux" + key.
/// </summary>
public class FluxImageClient : IImageGenerationClient
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<FluxImageClient> _logger;

    public FluxImageClient(IHttpClientFactory httpFactory, ILogger<FluxImageClient> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public string Provider => "flux";

    public async Task<ImageGenerationResult> GenerateAsync(ImageGenerationRequest request, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(request.ApiKey))
            return Fail("FLUX (Black Forest Labs) API key missing — set it in admin Integrations.", request, sw);

        try
        {
            var client = _httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(request.TimeoutSeconds <= 0 ? 90 : request.TimeoutSeconds);
            client.DefaultRequestHeaders.Add("x-key", request.ApiKey);

            var baseUrl = string.IsNullOrWhiteSpace(request.BaseUrl) ? "https://api.bfl.ai" : request.BaseUrl!.TrimEnd('/');
            var model = string.IsNullOrWhiteSpace(request.Model) ? "flux-dev" : request.Model;
            var (w, h) = (SnapDim(request.Width), SnapDim(request.Height));

            // 1) Submit
            var payload = new { prompt = request.Prompt, width = w, height = h };
            var submitContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var submit = await client.PostAsync($"{baseUrl}/v1/{model}", submitContent, ct);
            var submitBody = await submit.Content.ReadAsStringAsync(ct);
            if (!submit.IsSuccessStatusCode)
            {
                _logger.LogWarning("FLUX submit failed: {Status} {Body}", submit.StatusCode, submitBody);
                return Fail($"FLUX: {MediaErrorHelper.Extract(submitBody)}", request, sw);
            }
            var pollingUrl = JsonString(submitBody, "polling_url");
            if (string.IsNullOrEmpty(pollingUrl))
                return Fail("FLUX response had no polling_url.", request, sw);

            // 2) Poll until Ready (cap ~30s)
            for (var attempt = 0; attempt < 20; attempt++)
            {
                await Task.Delay(1500, ct);
                var poll = await client.GetAsync(pollingUrl, ct);
                var pollBody = await poll.Content.ReadAsStringAsync(ct);
                var status = JsonString(pollBody, "status");
                if (status == "Ready")
                {
                    var sampleUrl = ParseSampleUrl(pollBody);
                    if (string.IsNullOrEmpty(sampleUrl)) return Fail("FLUX ready but no image url.", request, sw);
                    // 3) Fetch the delivered image + embed (delivery URL is short-lived).
                    var imgBytes = await client.GetByteArrayAsync(sampleUrl, ct);
                    var dataUri = $"data:image/jpeg;base64,{Convert.ToBase64String(imgBytes)}";
                    sw.Stop();
                    return new ImageGenerationResult(true, dataUri, w, h, Provider, sw.Elapsed, null);
                }
                if (status is "Error" or "Failed" or "Content Moderated" or "Request Moderated")
                    return Fail($"FLUX status: {status}", request, sw);
            }
            return Fail("FLUX timed out waiting for the image.", request, sw);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FLUX image threw");
            sw.Stop();
            return new ImageGenerationResult(false, null, request.Width, request.Height, Provider, sw.Elapsed, ex.Message);
        }
    }

    /// <summary>BFL requires dimensions as multiples of 32, clamped to [256, 1440]. Pure → testable.</summary>
    internal static int SnapDim(int v)
    {
        if (v <= 0) return 1024;
        var snapped = (int)Math.Round(v / 32.0) * 32;
        return Math.Clamp(snapped, 256, 1440);
    }

    /// <summary>Reads result.sample (the image URL) from a poll body. Pure → testable.</summary>
    internal static string? ParseSampleUrl(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Object
                && result.TryGetProperty("sample", out var sample))
                return sample.GetString();
        }
        catch { /* ignore */ }
        return null;
    }

    private static string? JsonString(string body, string prop)
    {
        try { using var d = JsonDocument.Parse(body); return d.RootElement.TryGetProperty(prop, out var v) ? v.GetString() : null; }
        catch { return null; }
    }

    private ImageGenerationResult Fail(string error, ImageGenerationRequest req, Stopwatch sw)
    {
        sw.Stop();
        return new ImageGenerationResult(false, null, req.Width, req.Height, Provider, sw.Elapsed, error);
    }
}
