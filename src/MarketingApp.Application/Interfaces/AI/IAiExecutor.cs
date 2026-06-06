namespace MarketingApp.Application.Interfaces.AI;

/// <summary>
/// Central AI execution with automatic fallback (Day 10). Resolves the admin-configured PRIMARY provider,
/// runs the request, and — if the primary hits a quota / rate-limit (429) error — automatically retries the
/// SAME request through the configured FALLBACK provider (typically a free tier like Groq/Ollama).
/// Both the reply-draft service and the chat service call this instead of resolving clients themselves.
/// </summary>
public interface IAiExecutor
{
    /// <summary>
    /// Generate a completion using the primary provider, auto-failing over to the fallback on quota errors.
    /// Reads model / key / temperature / timeout from SystemSettings. Throws if no provider is configured
    /// (provider == "disabled") or if BOTH primary and fallback fail.
    /// </summary>
    /// <param name="maxTokens">Override max output tokens (0 = use SystemSettings). Larger requests like
    /// multi-channel marketing copy need more so the JSON isn't truncated.</param>
    Task<AiCompletion> GenerateAsync(string systemPrompt, string userPrompt, AiResponseShape shape, CancellationToken ct, int maxTokens = 0);
}
