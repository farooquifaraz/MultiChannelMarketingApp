using System.Security.Claims;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Application.Interfaces.AI;
using MarketingApp.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketingApp.API.Controllers;

[ApiController]
[Route("api/v1/admin/ai")]
[Authorize]
public class AdminAiController : ControllerBase
{
    private readonly IAiClientFactory _factory;
    private readonly ISystemSettingsService _systemSettings;

    public AdminAiController(IAiClientFactory factory, ISystemSettingsService systemSettings)
    {
        _factory = factory;
        _systemSettings = systemSettings;
    }

    private bool IsAdmin() => string.Equals(User.FindFirstValue(ClaimTypes.Role), "admin", StringComparison.OrdinalIgnoreCase);

    private IActionResult? GuardAdmin() =>
        IsAdmin() ? null : StatusCode(403, ApiResponse<object>.Fail("Admin role required."));

    /// <summary>Backend-side canonical default prompt — frontend "Reset to default" fetches this.</summary>
    [HttpGet("default-prompt")]
    public IActionResult DefaultPrompt() =>
        Ok(ApiResponse<object>.Ok(new { prompt = SystemSettings.DefaultAiSystemPrompt }));

    /// <summary>Test the configured AI provider with a hardcoded sample payload — returns the parsed completion.</summary>
    [HttpPost("test")]
    public async Task<IActionResult> Test([FromBody] AiTestRequest? body, CancellationToken ct)
    {
        if (GuardAdmin() is { } forbidden) return forbidden;

        var settings = await _systemSettings.GetAsync(ct);
        // Provider resolution: body wins (live form selection), else saved DB value.
        var providerName = !string.IsNullOrWhiteSpace(body?.Provider) ? body!.Provider : settings.AiProvider;
        if (string.IsNullOrWhiteSpace(providerName) || providerName == "disabled")
            return BadRequest(ApiResponse<object>.Fail("AI provider is disabled. Pick a provider before testing."));

        var client = _factory.Resolve(providerName);
        if (client is null) return BadRequest(ApiResponse<object>.Fail($"Unknown provider '{providerName}'."));

        try
        {
            // API key resolution priority:
            //   1. body.ApiKey  — admin is currently typing/changing the key (live test before saving)
            //   2. Saved DB key — admin saved earlier; "Test" works without re-pasting
            //   3. null         — let the provider client throw a clear "missing key" error
            var apiKey = !string.IsNullOrWhiteSpace(body?.ApiKey)
                ? body!.ApiKey
                : await _systemSettings.GetRawAiApiKeyAsync(ct);

            var completion = await client.GenerateAsync(new AiRequest(
                SystemPrompt: body?.SystemPrompt ?? settings.AiSystemPrompt,
                UserPrompt: body?.UserPrompt ?? "A recipient just replied: 'Sounds great, how much for 100 licenses?'",
                Model: body?.Model ?? settings.AiModel,
                MaxTokens: body?.MaxTokens ?? settings.AiMaxTokens,
                Temperature: body?.Temperature ?? settings.AiTemperature,
                ApiKey: apiKey,
                BaseUrl: body?.BaseUrl ?? settings.AiBaseUrl,
                TimeoutSeconds: settings.AiTimeoutSeconds
            ), ct);

            return Ok(ApiResponse<object>.Ok(new
            {
                provider = completion.Provider,
                rawText = completion.RawText,
                inputTokens = completion.InputTokens,
                outputTokens = completion.OutputTokens,
                latencyMs = (int)completion.Latency.TotalMilliseconds,
            }));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail($"Test failed: {ex.Message}"));
        }
    }

}

public record AiTestRequest
{
    public string? Provider { get; init; }
    public string? ApiKey { get; init; }
    public string? BaseUrl { get; init; }
    public string? Model { get; init; }
    public string? SystemPrompt { get; init; }
    public string? UserPrompt { get; init; }
    public int? MaxTokens { get; init; }
    public decimal? Temperature { get; init; }
}
