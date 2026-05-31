using MarketingApp.Application.Interfaces;
using MarketingApp.Application.Interfaces.AI;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Application.Services.AI;

/// <summary>
/// Implements primary→fallback AI execution (Day 10). See IAiExecutor.
/// </summary>
public class AiExecutor : IAiExecutor
{
    private readonly IAiClientFactory _factory;
    private readonly ISystemSettingsService _systemSettings;
    private readonly ILogger<AiExecutor> _logger;

    public AiExecutor(IAiClientFactory factory, ISystemSettingsService systemSettings, ILogger<AiExecutor> logger)
    {
        _factory = factory;
        _systemSettings = systemSettings;
        _logger = logger;
    }

    public async Task<AiCompletion> GenerateAsync(string systemPrompt, string userPrompt, AiResponseShape shape, CancellationToken ct)
    {
        var s = await _systemSettings.GetAsync(ct);
        if (string.IsNullOrWhiteSpace(s.AiProvider) || s.AiProvider == "disabled")
            throw new InvalidOperationException("AI is not configured. Ask your admin to set it up in Settings → AI Assistant.");

        var primaryClient = _factory.Resolve(s.AiProvider)
            ?? throw new InvalidOperationException($"No AI client registered for provider '{s.AiProvider}'.");
        var primaryKey = await _systemSettings.GetRawAiApiKeyAsync(ct);

        var request = new AiRequest(
            SystemPrompt: systemPrompt,
            UserPrompt: userPrompt,
            Model: s.AiModel,
            MaxTokens: Math.Max(s.AiMaxTokens, 1000),
            Temperature: s.AiTemperature,
            ApiKey: primaryKey,
            BaseUrl: s.AiBaseUrl,
            TimeoutSeconds: s.AiTimeoutSeconds,
            Shape: shape);

        try
        {
            return await primaryClient.GenerateAsync(request, ct);
        }
        catch (Exception ex) when (IsQuotaOrRateLimit(ex) && HasFallback(s))
        {
            _logger.LogWarning(
                "[AiExecutor] Primary provider '{Primary}' hit quota/rate-limit — failing over to fallback '{Fallback}' ({Model}).",
                s.AiProvider, s.AiFallbackProvider, s.AiFallbackModel);

            var fbClient = _factory.Resolve(s.AiFallbackProvider)
                ?? throw new InvalidOperationException($"Fallback provider '{s.AiFallbackProvider}' is not registered.");
            var fbKey = await _systemSettings.GetRawAiFallbackApiKeyAsync(ct);

            var fbRequest = request with
            {
                Model = s.AiFallbackModel,
                ApiKey = fbKey,
                BaseUrl = s.AiFallbackBaseUrl,
            };
            var completion = await fbClient.GenerateAsync(fbRequest, ct);
            // Tag so the UI can show "(fallback)" if desired.
            return completion with { Provider = $"{completion.Provider} (fallback)" };
        }
    }

    /// <summary>True when the primary failed specifically due to quota / rate-limit (429), so fallback makes sense.
    /// Other failures (bad key, 5xx, timeout) are NOT auto-failed-over — those would likely fail on the fallback too
    /// or indicate a config problem the admin should see.</summary>
    private static bool IsQuotaOrRateLimit(Exception ex)
    {
        var m = ex.Message?.ToLowerInvariant() ?? "";
        return m.Contains("quota") || m.Contains("rate limit") || m.Contains("rate-limit")
            || m.Contains("429") || m.Contains("too many requests") || m.Contains("resource_exhausted");
    }

    private static bool HasFallback(MarketingApp.Application.DTOs.SystemSettingsDto s) =>
        !string.IsNullOrWhiteSpace(s.AiFallbackProvider) && s.AiFallbackProvider != "disabled";
}
