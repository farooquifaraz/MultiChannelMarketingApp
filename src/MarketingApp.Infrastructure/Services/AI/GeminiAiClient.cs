using System.Text.Json;
using MarketingApp.Application.Interfaces.AI;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Infrastructure.Services.AI;

/// <summary>
/// Google Gemini client. Auth via query-string API key (Gemini quirk). Has a generous free tier.
/// </summary>
public class GeminiAiClient : BaseHttpAiClient
{
    public GeminiAiClient(IHttpClientFactory f, ILogger<GeminiAiClient> l) : base(f, l) { }

    public override string Provider => "gemini";

    protected override void ConfigureAuth(HttpClient client, AiRequest request) { /* uses query-string */ }

    protected override (string Url, object Payload) BuildRequest(AiRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey))
            throw new InvalidOperationException("Gemini API key missing — set it in admin AI Settings.");
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{request.Model}:generateContent?key={request.ApiKey}";

        // Build generationConfig conditionally by response shape.
        // thinkingBudget=0 disables Gemini 2.5's internal chain-of-thought which otherwise eats the
        // output-token budget and truncates the answer to ~20 tokens.
        object generationConfig = request.Shape switch
        {
            AiResponseShape.ReplyJson => new
            {
                thinkingConfig = new { thinkingBudget = 0 },
                maxOutputTokens = Math.Max(request.MaxTokens, 2048),
                temperature = (double)request.Temperature,
                responseMimeType = "application/json",
                responseSchema = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        category = new { type = "STRING", description = "One of: question, interested, complaint, unsubscribe, spam, other" },
                        summary = new { type = "STRING", description = "One-sentence gist of the recipient's reply" },
                        questions = new
                        {
                            type = "ARRAY",
                            description = "Exactly 3 short, email-specific questions the USER might want to ask an AI assistant about this email.",
                            items = new { type = "STRING" }
                        },
                        reply = new { type = "STRING", description = "HTML body of the suggested reply, with <p> tags" }
                    },
                    propertyOrdering = new[] { "category", "summary", "questions", "reply" },
                    required = new[] { "category", "summary", "reply" }
                }
            },
            AiResponseShape.ChatJson => new
            {
                thinkingConfig = new { thinkingBudget = 0 },
                maxOutputTokens = Math.Max(request.MaxTokens, 2048),
                temperature = (double)request.Temperature,
                responseMimeType = "application/json",
                responseSchema = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        answer = new { type = "STRING", description = "The conversational answer to the user's question, in HTML or plain text with line breaks. Do NOT include JSON." },
                        suggestions = new
                        {
                            type = "ARRAY",
                            description = "Exactly 3 SHORT follow-up questions the user might ask NEXT, based on this answer. Different from what was already asked.",
                            items = new { type = "STRING" }
                        }
                    },
                    propertyOrdering = new[] { "answer", "suggestions" },
                    required = new[] { "answer" }
                }
            },
            // PlainText — no JSON forcing.
            _ => new
            {
                thinkingConfig = new { thinkingBudget = 0 },
                maxOutputTokens = Math.Max(request.MaxTokens, 2048),
                temperature = (double)request.Temperature,
            }
        };

        return (url, new
        {
            system_instruction = new { parts = new[] { new { text = request.SystemPrompt } } },
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = request.UserPrompt } } }
            },
            generationConfig
        });
    }

    protected override AiCompletion ParseResponse(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var usage = root.TryGetProperty("usageMetadata", out var u) ? u : default;
        var inputTokens = usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("promptTokenCount", out var pt) ? pt.GetInt32() : 0;
        var outputTokens = usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("candidatesTokenCount", out var ct) ? ct.GetInt32() : 0;

        // Safe navigation — Gemini can return a candidate with NO content.parts when it hits MAX_TOKENS
        // during thinking, or when SAFETY blocks the response. Detect that and throw a clear error
        // instead of crashing with an index-out-of-range.
        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            throw new InvalidOperationException("Gemini returned no candidates. Check API key / model name.");

        var candidate = candidates[0];
        var finishReason = candidate.TryGetProperty("finishReason", out var fr) ? fr.GetString() : null;

        string text = "";
        if (candidate.TryGetProperty("content", out var content)
            && content.TryGetProperty("parts", out var parts)
            && parts.ValueKind == JsonValueKind.Array && parts.GetArrayLength() > 0
            && parts[0].TryGetProperty("text", out var txt))
        {
            text = txt.GetString() ?? "";
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            // Most common reasons: MAX_TOKENS (thinking ate budget), SAFETY block, or RECITATION.
            throw new InvalidOperationException(
                $"Gemini produced no text output (finishReason={finishReason ?? "unknown"}, outputTokens={outputTokens}). " +
                "If finishReason=MAX_TOKENS, increase Max Output Tokens in AI Settings.");
        }

        return new AiCompletion(text, inputTokens, outputTokens, TimeSpan.Zero, Provider);
    }
}
