using System.Net.Http.Headers;
using MarketingApp.Application.Interfaces.AI;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Infrastructure.Services.AI;

/// <summary>
/// Groq (console.groq.com) — a FREE, very fast OpenAI-compatible inference provider. Dedicated client
/// so users don't confuse it with xAI's "Grok". Base URL defaults to Groq's API (overridable); needs a
/// free <c>gsk_</c> key. Default model: llama-3.3-70b-versatile. Reuses the OpenAI wire format.
/// </summary>
public class GroqAiClient : BaseHttpAiClient
{
    private const string DefaultBaseUrl = "https://api.groq.com/openai/v1";

    public GroqAiClient(IHttpClientFactory f, ILogger<GroqAiClient> l) : base(f, l) { }

    public override string Provider => "groq";

    protected override void ConfigureAuth(HttpClient client, AiRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey))
            throw new InvalidOperationException("Groq API key missing — get a free key at console.groq.com and add it in Integrations.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey);
    }

    protected override (string Url, object Payload) BuildRequest(AiRequest request)
    {
        var baseUrl = string.IsNullOrWhiteSpace(request.BaseUrl) ? DefaultBaseUrl : request.BaseUrl.TrimEnd('/');
        // Groq uses a default model if none configured.
        var req = string.IsNullOrWhiteSpace(request.Model) ? request with { Model = "llama-3.3-70b-versatile" } : request;
        return (baseUrl + "/chat/completions", OpenAiClient.BuildOpenAiCompatiblePayload(req));
    }

    protected override AiCompletion ParseResponse(string body) =>
        OpenAiClient.ParseOpenAiCompatibleResponse(body, Provider);
}
