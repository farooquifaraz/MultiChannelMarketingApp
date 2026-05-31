using System.Text.Json;
using MarketingApp.Application.Interfaces.AI;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Infrastructure.Services.AI;

public class AnthropicAiClient : BaseHttpAiClient
{
    public AnthropicAiClient(IHttpClientFactory f, ILogger<AnthropicAiClient> l) : base(f, l) { }

    public override string Provider => "anthropic";

    protected override void ConfigureAuth(HttpClient client, AiRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey))
            throw new InvalidOperationException("Anthropic API key missing — set it in admin AI Settings.");
        client.DefaultRequestHeaders.Add("x-api-key", request.ApiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    protected override (string Url, object Payload) BuildRequest(AiRequest request) => (
        "https://api.anthropic.com/v1/messages",
        new
        {
            model = request.Model,
            max_tokens = request.MaxTokens,
            temperature = (double)request.Temperature,
            system = request.SystemPrompt,
            messages = new[] { new { role = "user", content = request.UserPrompt } }
        });

    protected override AiCompletion ParseResponse(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
        var usage = doc.RootElement.TryGetProperty("usage", out var u) ? u : default;
        var inputTokens = usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("input_tokens", out var it) ? it.GetInt32() : 0;
        var outputTokens = usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("output_tokens", out var ot) ? ot.GetInt32() : 0;
        return new AiCompletion(text, inputTokens, outputTokens, TimeSpan.Zero, Provider);
    }
}
