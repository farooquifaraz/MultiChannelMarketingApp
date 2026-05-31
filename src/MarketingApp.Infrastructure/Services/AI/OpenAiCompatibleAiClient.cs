using System.Net.Http.Headers;
using MarketingApp.Application.Interfaces.AI;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Infrastructure.Services.AI;

/// <summary>
/// Universal adapter for any OpenAI-compatible endpoint (Day 7 G5).
/// Admin configures <see cref="AiRequest.BaseUrl"/> + Model. Use cases:
///   - Ollama (free local): BaseUrl=http://localhost:11434/v1, ApiKey blank
///   - Groq (free cloud):   BaseUrl=https://api.groq.com/openai/v1, ApiKey=groq key
///   - OpenRouter:          BaseUrl=https://openrouter.ai/api/v1, ApiKey=or key
///   - LM Studio / vLLM / LocalAI / custom self-hosted
///
/// ZERO code change needed to support any future OpenAI-compatible provider.
/// </summary>
public class OpenAiCompatibleAiClient : BaseHttpAiClient
{
    public OpenAiCompatibleAiClient(IHttpClientFactory f, ILogger<OpenAiCompatibleAiClient> l) : base(f, l) { }

    public override string Provider => "openai-compatible";

    protected override void ConfigureAuth(HttpClient client, AiRequest request)
    {
        // ApiKey is OPTIONAL — Ollama localhost doesn't require one.
        if (!string.IsNullOrWhiteSpace(request.ApiKey))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey);
    }

    protected override (string Url, object Payload) BuildRequest(AiRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BaseUrl))
            throw new InvalidOperationException("BaseUrl required for openai-compatible provider — set it in admin AI Settings (e.g. http://localhost:11434/v1 for Ollama).");
        var url = request.BaseUrl.TrimEnd('/') + "/chat/completions";
        return (url, OpenAiClient.BuildOpenAiCompatiblePayload(request));
    }

    protected override AiCompletion ParseResponse(string body) =>
        OpenAiClient.ParseOpenAiCompatibleResponse(body, Provider);
}
