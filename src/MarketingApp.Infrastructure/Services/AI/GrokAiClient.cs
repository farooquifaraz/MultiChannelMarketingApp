using System.Net.Http.Headers;
using MarketingApp.Application.Interfaces.AI;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Infrastructure.Services.AI;

/// <summary>
/// xAI Grok client. Wire format is OpenAI-compatible, but kept as a separate class so future
/// Grok-specific features (like reasoning controls) don't require an "openai-compatible" override.
/// </summary>
public class GrokAiClient : BaseHttpAiClient
{
    public GrokAiClient(IHttpClientFactory f, ILogger<GrokAiClient> l) : base(f, l) { }

    public override string Provider => "grok";

    protected override void ConfigureAuth(HttpClient client, AiRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey))
            throw new InvalidOperationException("Grok API key missing — set it in admin AI Settings.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey);
    }

    protected override (string Url, object Payload) BuildRequest(AiRequest request) => (
        "https://api.x.ai/v1/chat/completions",
        OpenAiClient.BuildOpenAiCompatiblePayload(request));

    protected override AiCompletion ParseResponse(string body) =>
        OpenAiClient.ParseOpenAiCompatibleResponse(body, Provider);
}
