namespace MarketingApp.Application.Interfaces.AI;

/// <summary>
/// Strategy contract for AI text generation (Day 7 G5).
/// Implementations:
///   - AnthropicAiClient    (Claude)
///   - OpenAiClient         (GPT)
///   - GeminiAiClient       (Google)
///   - GrokAiClient         (xAI)
///   - OpenAiCompatibleAiClient (universal adapter for Ollama / Groq / OpenRouter / LM Studio / custom)
/// Adding a new provider = drop one IAiClient + 1 DI line. No factory change required.
/// </summary>
public interface IAiClient
{
    /// <summary>Provider key — matched against SystemSettings.AiProvider. e.g. "anthropic", "openai".</summary>
    string Provider { get; }

    Task<AiCompletion> GenerateAsync(AiRequest request, CancellationToken ct);
}

/// <summary>
/// Controls the AI provider's output format. Each use-case picks its shape so a single client
/// can serve both the structured reply-generator AND the free-form chat agent.
///   PlainText — no JSON forcing; conversational text (chat answers).
///   ReplyJson — strict {category, summary, questions, reply} schema (inbox reply drafts).
///   ChatJson  — strict {answer, suggestions[]} schema (chat answer + dynamic follow-up chips).
/// </summary>
public enum AiResponseShape { PlainText, ReplyJson, ChatJson }

public sealed record AiRequest(
    string SystemPrompt,
    string UserPrompt,
    string Model,
    int MaxTokens,
    decimal Temperature,
    string? ApiKey,
    string? BaseUrl,
    int TimeoutSeconds,
    AiResponseShape Shape = AiResponseShape.PlainText);

public sealed record AiCompletion(
    string RawText,
    int InputTokens,
    int OutputTokens,
    TimeSpan Latency,
    string Provider);

public interface IAiClientFactory
{
    /// <summary>Returns the IAiClient registered for the given provider key, or null if none found / disabled.</summary>
    IAiClient? Resolve(string provider);
}
