using System.Net.Http.Headers;
using System.Text.Json;
using MarketingApp.Application.Interfaces.AI;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Infrastructure.Services.AI;

public class OpenAiClient : BaseHttpAiClient
{
    public OpenAiClient(IHttpClientFactory f, ILogger<OpenAiClient> l) : base(f, l) { }

    public override string Provider => "openai";

    protected override void ConfigureAuth(HttpClient client, AiRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey))
            throw new InvalidOperationException("OpenAI API key missing — set it in admin AI Settings.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey);
    }

    protected override (string Url, object Payload) BuildRequest(AiRequest request) => (
        "https://api.openai.com/v1/chat/completions",
        BuildOpenAiCompatiblePayload(request));

    protected override AiCompletion ParseResponse(string body) => ParseOpenAiCompatibleResponse(body, Provider);

    /// <summary>Shared with OpenAiCompatibleAiClient + GrokAiClient — they all speak the same wire format.
    /// response_format=json_object forces strict JSON output, but ONLY for structured shapes (reply/chat).
    /// PlainText (free-form chat answers) must NOT force json_object or the model wraps prose in JSON.</summary>
    internal static object BuildOpenAiCompatiblePayload(AiRequest request)
    {
        if (request.Shape == AiResponseShape.PlainText)
        {
            var plainMessages = new[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserPrompt }
            };
            return new { model = request.Model, max_tokens = request.MaxTokens, temperature = (double)request.Temperature, messages = plainMessages };
        }

        // OpenAI / Groq HARD requirement: when response_format = json_object is set, the messages MUST
        // contain the literal word "json" somewhere, otherwise the API returns HTTP 400. Our prompts are
        // written provider-neutrally (Gemini uses responseSchema and doesn't need it), so we guarantee the
        // word is present by appending an explicit JSON directive to the system prompt when it's missing.
        var systemContent = request.SystemPrompt ?? "";
        if (!systemContent.Contains("json", StringComparison.OrdinalIgnoreCase))
            systemContent += "\n\nIMPORTANT: Respond ONLY with a single valid JSON object. No prose, no markdown, no code fences.";

        var messages = new[]
        {
            new { role = "system", content = systemContent },
            new { role = "user", content = request.UserPrompt }
        };
        return new
        {
            model = request.Model,
            max_tokens = request.MaxTokens,
            temperature = (double)request.Temperature,
            response_format = new { type = "json_object" },
            messages
        };
    }

    internal static AiCompletion ParseOpenAiCompatibleResponse(string body, string provider)
    {
        using var doc = JsonDocument.Parse(body);
        var text = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";
        var usage = doc.RootElement.TryGetProperty("usage", out var u) ? u : default;
        var inputTokens = usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0;
        var outputTokens = usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : 0;
        return new AiCompletion(text, inputTokens, outputTokens, TimeSpan.Zero, provider);
    }
}
