using System.Text.Json;
using MarketingApp.Application.Interfaces;
using MarketingApp.Application.Interfaces.AI;
using MarketingApp.Application.Interfaces.Inbox;
using MarketingApp.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Application.Services.AI;

/// <summary>
/// Orchestrates AI suggestion generation for inbox messages (Day 7 G6, Day 8 thread-aware).
/// Failure-tolerant: any exception is logged + saved on the InboxMessage so the UI can show "Retry";
/// the inbox listing itself is never blocked by AI failures.
/// </summary>
public class AiReplyService : IAiReplyService
{
    private readonly IGenericRepository<InboxMessage> _inboxRepo;
    private readonly IGenericRepository<CampaignMessage> _msgRepo;
    private readonly IGenericRepository<Campaign> _campaignRepo;
    private readonly IGenericRepository<MessageTemplate> _templateRepo;
    private readonly IGenericRepository<OutboundReply> _outboundRepo;
    private readonly IAiExecutor _aiExecutor;
    private readonly ISystemSettingsService _systemSettings;
    private readonly IInboxRealtimeNotifier _realtime;
    private readonly IAuditService _audit;
    private readonly ILogger<AiReplyService> _logger;

    public AiReplyService(
        IGenericRepository<InboxMessage> inboxRepo,
        IGenericRepository<CampaignMessage> msgRepo,
        IGenericRepository<Campaign> campaignRepo,
        IGenericRepository<MessageTemplate> templateRepo,
        IGenericRepository<OutboundReply> outboundRepo,
        IAiExecutor aiExecutor,
        ISystemSettingsService systemSettings,
        IInboxRealtimeNotifier realtime,
        IAuditService audit,
        ILogger<AiReplyService> logger)
    {
        _inboxRepo = inboxRepo;
        _msgRepo = msgRepo;
        _campaignRepo = campaignRepo;
        _templateRepo = templateRepo;
        _outboundRepo = outboundRepo;
        _aiExecutor = aiExecutor;
        _systemSettings = systemSettings;
        _realtime = realtime;
        _audit = audit;
        _logger = logger;
    }

    public async Task ProcessAsync(Guid inboxMessageId, CancellationToken ct)
    {
        var message = await _inboxRepo.GetByIdAsync(inboxMessageId, ct);
        if (message is null) return;

        var settings = await _systemSettings.GetAsync(ct);
        _logger.LogInformation("[AI] ProcessAsync starting for inbox {Id} | provider='{Provider}' | model='{Model}' | hasKey={HasKey}",
            inboxMessageId, settings.AiProvider, settings.AiModel, !string.IsNullOrEmpty(settings.AiApiKeyMasked));

        if (string.IsNullOrWhiteSpace(settings.AiProvider) || settings.AiProvider == "disabled")
        {
            const string msg = "AI is not configured. Go to Settings → AI Assistant → pick a provider + paste API key → click Save Settings (top-right).";
            _logger.LogWarning("[AI] {Msg} (inbox {Id})", msg, inboxMessageId);
            message.AiGenerationError = msg;
            message.UpdatedAt = DateTime.UtcNow;
            try { await _inboxRepo.UpdateAsync(message, ct); } catch { /* swallow */ }
            return;
        }

        // Build context — original campaign subject + first 500 chars of body, if we matched one.
        string? originalSubject = null;
        string? originalSnippet = null;
        if (message.MatchedCampaignMessageId.HasValue)
        {
            var cm = await _msgRepo.GetByIdAsync(message.MatchedCampaignMessageId.Value, ct);
            if (cm is not null)
            {
                var campaign = await _campaignRepo.GetByIdAsync(cm.CampaignId, ct);
                if (campaign is not null)
                {
                    var template = await _templateRepo.GetByIdAsync(campaign.TemplateId, ct);
                    originalSubject = template?.Subject ?? campaign.Name;
                    originalSnippet = StripAndTruncate(template?.Body ?? "", 500);
                }
            }
        }

        var replyBody = StripAndTruncate(message.TextBody ?? message.HtmlBody ?? "", 2000);

        // === Day 8: build full-thread transcript so the AI reply is coherent across multi-turn conversations ===
        var transcript = await BuildThreadTranscriptAsync(message, settings.AiAllowSendRecipientPii, ct);
        var userPrompt = BuildUserPrompt(message, originalSubject, originalSnippet, replyBody, settings.AiAllowSendRecipientPii, transcript);

        // If admin saved blank prompt (or never saved), fall back to the canonical default so the model
        // still gets sensible instructions instead of an empty system prompt.
        var systemPrompt = string.IsNullOrWhiteSpace(settings.AiSystemPrompt)
            ? MarketingApp.Domain.Entities.SystemSettings.DefaultAiSystemPrompt
            : settings.AiSystemPrompt;

        try
        {
            // Executor handles provider resolution + auto-fallback on quota (Day 10).
            var completion = await _aiExecutor.GenerateAsync(systemPrompt, userPrompt, AiResponseShape.ReplyJson, ct);
            // Truncated raw log helps debug malformed JSON without flooding logs with huge bodies.
            _logger.LogInformation("[AI] Raw response from {Provider} ({InTok} in / {OutTok} out, {Latency}ms): {Preview}…",
                completion.Provider, completion.InputTokens, completion.OutputTokens, (int)completion.Latency.TotalMilliseconds,
                Truncate(completion.RawText, 300));
            var (category, summary, reply, strategy) = ParseAiOutputWithDiagnostics(completion.RawText);
            _logger.LogInformation("[AI] Parsed via strategy '{Strategy}': category={Cat} summaryLen={SLen} replyLen={RLen}",
                strategy, category, summary.Length, reply.Length);

            // Day 9: extract the 3 suggested questions (best-effort) + always store SOMETHING usable.
            var questions = ExtractQuestions(completion.RawText);
            message.AiSuggestedQuestions = JsonSerializer.Serialize(questions);

            message.AiCategory = category;
            message.AiSummary = summary;
            message.AiSuggestedReply = reply;
            // Clear any stale user-edited draft so the freshly regenerated AI suggestion
            // takes precedence in the editor. User can still pick "Reset to AI" if they
            // had hand-crafted changes — but the assumption here is: a fresh AI generation
            // supersedes the old draft (which was based on the previous AI output).
            message.UserEditedReply = null;
            message.DraftSavedAt = null;
            message.AiGeneratedAt = DateTime.UtcNow;
            message.AiGenerationError = null;
            message.AiProviderUsed = completion.Provider;
            message.AiInputTokens = completion.InputTokens;
            message.AiOutputTokens = completion.OutputTokens;
            message.UpdatedAt = DateTime.UtcNow;
            await _inboxRepo.UpdateAsync(message, ct);

            await _audit.LogAsync(null, "AiReplyGenerated", "InboxMessage", inboxMessageId,
                details: new { provider = completion.Provider, completion.InputTokens, completion.OutputTokens, latencyMs = (int)completion.Latency.TotalMilliseconds },
                ct: ct);

            // Day 8: push the fresh draft to the owner's open clients so the editor updates live.
            try { await _realtime.NotifyAiReadyAsync(message.OwnerUserId, message.ThreadId, message.Id); }
            catch (Exception ex) { _logger.LogWarning(ex, "[AI] Realtime ai-ready notify failed for {Id}", inboxMessageId); }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AI] Generation failed for inbox {Id}", inboxMessageId);
            message.AiGenerationError = Truncate(ex.Message, 1000);
            message.UpdatedAt = DateTime.UtcNow;
            try { await _inboxRepo.UpdateAsync(message, ct); } catch { /* swallow — already in error path */ }
            // Notify so the UI can show the error state without waiting for the next poll.
            try { await _realtime.NotifyAiReadyAsync(message.OwnerUserId, message.ThreadId, message.Id); } catch { }
        }
    }

    /// <summary>
    /// Build a chronological transcript of the whole conversation (inbound + our outbound replies)
    /// so the AI understands multi-turn context, not just the latest message.
    /// </summary>
    private async Task<string> BuildThreadTranscriptAsync(InboxMessage latest, bool includePii, CancellationToken ct)
    {
        var inbound = (await _inboxRepo.FindAsync(m => m.ThreadId == latest.ThreadId, ct)).ToList();
        var outbound = (await _outboundRepo.FindAsync(o => o.ThreadId == latest.ThreadId && o.SendSucceeded, ct)).ToList();
        if (inbound.Count <= 1 && outbound.Count == 0) return ""; // single message — no history to add

        var entries = new List<(DateTime at, string line)>();
        foreach (var m in inbound)
        {
            var who = includePii ? (m.FromName ?? m.FromEmail) : "Recipient";
            entries.Add((m.ReceivedAt, $"[{m.ReceivedAt:yyyy-MM-dd HH:mm}] {who}: {StripAndTruncate(m.TextBody ?? m.HtmlBody ?? "", 800)}"));
        }
        foreach (var o in outbound)
        {
            entries.Add((o.SentAt, $"[{o.SentAt:yyyy-MM-dd HH:mm}] You: {StripAndTruncate(o.BodyHtml ?? "", 800)}"));
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("--- CONVERSATION HISTORY (oldest first) ---");
        foreach (var e in entries.OrderBy(e => e.at)) sb.AppendLine(e.line);
        return sb.ToString();
    }

    private Task<string?> GetRawApiKeyAsync(CancellationToken ct) =>
        _systemSettings.GetRawAiApiKeyAsync(ct);

    /// <summary>3 sensible static questions used when the AI didn't return any (or returned junk).</summary>
    public static readonly string[] DefaultSuggestedQuestions =
    {
        "Summarize this email",
        "What is the sender asking for?",
        "List action items / next steps",
    };

    /// <summary>
    /// Best-effort extraction of the "questions" array from the AI's raw JSON. Tolerates the same
    /// malformed-JSON cases as the main parser. Always returns exactly 3 non-empty questions
    /// (pads with defaults) so the UI chip row is never empty.
    /// </summary>
    private static List<string> ExtractQuestions(string raw)
    {
        var result = new List<string>();
        try
        {
            var cleaned = StripCodeFences(raw.Trim());
            // Try strict parse first; fall back to control-char escaping.
            JsonDocument? doc = null;
            try { doc = JsonDocument.Parse(cleaned); }
            catch { try { doc = JsonDocument.Parse(EscapeUnescapedControlChars(cleaned)); } catch { /* give up */ } }

            if (doc is not null)
            {
                using (doc)
                {
                    if (doc.RootElement.ValueKind == JsonValueKind.Object
                        && doc.RootElement.TryGetProperty("questions", out var q)
                        && q.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in q.EnumerateArray())
                        {
                            var s = item.GetString();
                            if (!string.IsNullOrWhiteSpace(s)) result.Add(s.Trim());
                        }
                    }
                }
            }
        }
        catch { /* ignore — fall through to defaults */ }

        // Pad / trim to exactly 3.
        foreach (var d in DefaultSuggestedQuestions)
        {
            if (result.Count >= 3) break;
            if (!result.Contains(d)) result.Add(d);
        }
        return result.Take(3).ToList();
    }

    private static string BuildUserPrompt(InboxMessage msg, string? originalSubject, string? originalSnippet, string replyBody, bool includePii, string transcript = "")
    {
        var sender = includePii
            ? $"{msg.FromName ?? msg.FromEmail} <{msg.FromEmail}>"
            : "the recipient";
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"--- ORIGINAL CAMPAIGN ---");
        sb.AppendLine($"Subject: {originalSubject ?? "(unknown)"}");
        sb.AppendLine($"Body excerpt: {originalSnippet ?? "(unknown)"}");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(transcript))
        {
            sb.AppendLine(transcript);
            sb.AppendLine();
        }
        sb.AppendLine($"--- LATEST INCOMING REPLY (reply to THIS) ---");
        sb.AppendLine($"From: {sender}");
        sb.AppendLine($"Subject: {msg.Subject}");
        sb.AppendLine($"Body: {replyBody}");
        sb.AppendLine();
        sb.AppendLine("Considering the full conversation history above, classify the latest reply and write a contextually-aware suggested response. Output STRICT JSON only.");
        return sb.ToString();
    }

    /// <summary>
    /// Bulletproof parser for AI-generated JSON. Returns the chosen strategy as a string so callers can log it.
    /// Handles five shapes in priority order:
    ///   1. Pure JSON (best case, native JSON mode)
    ///   2. JSON wrapped in ```json …``` markdown code fences
    ///   3. JSON with literal newlines/tabs INSIDE string values (Gemini sometimes outputs this — spec-violation)
    ///   4. Truncated JSON (response cut off by max_tokens) — regex extracts reply even without closing quote
    ///   5. Free-form text with extractable key=value pairs (regex rescue)
    /// Last resort: return raw text as the reply so the user still sees SOMETHING they can edit/send.
    /// </summary>
    private static (string category, string summary, string reply, string strategy) ParseAiOutputWithDiagnostics(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return ("other", "AI returned an empty response.", "", "empty");

        var cleaned = StripCodeFences(raw.Trim());

        // ── Attempt 1: direct JSON parse ──
        if (TryParseJson(cleaned, out var r1)) return (r1.category, r1.summary, r1.reply, "direct-json");

        // ── Attempt 2: escape unescaped control chars inside string values ──
        var fixedJson = EscapeUnescapedControlChars(cleaned);
        if (TryParseJson(fixedJson, out var r2)) return (r2.category, r2.summary, r2.reply, "control-char-escaped");

        // ── Attempt 3: regex extract — robust against truncated / malformed JSON ──
        if (TryRegexExtractRobust(cleaned, out var r3)) return (r3.category, r3.summary, r3.reply, "regex-extracted");

        // ── Last resort: convert raw text to HTML paragraphs so editor renders nicely ──
        var fallbackReply = ConvertPlainTextToHtml(raw);
        return ("other", "AI returned unstructured output — using raw text as draft.", fallbackReply, "raw-fallback");
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max];

    private static string StripCodeFences(string s)
    {
        if (!s.StartsWith("```")) return s;
        var firstNewline = s.IndexOf('\n');
        if (firstNewline > 0) s = s[(firstNewline + 1)..];
        var lastFence = s.LastIndexOf("```", StringComparison.Ordinal);
        if (lastFence > 0) s = s[..lastFence];
        return s.Trim();
    }

    private static bool TryParseJson(string s, out (string category, string summary, string reply) result)
    {
        result = ("", "", "");
        try
        {
            using var doc = JsonDocument.Parse(s);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            var category = root.TryGetProperty("category", out var c) ? c.GetString() ?? "other" : "other";
            var summary = root.TryGetProperty("summary", out var sm) ? sm.GetString() ?? "" : "";
            var reply = root.TryGetProperty("reply", out var rp) ? rp.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(reply)) return false; // useless if reply is empty
            result = (category, summary, reply);
            return true;
        }
        catch { return false; }
    }

    /// <summary>Escape literal control characters that appear INSIDE JSON string tokens.</summary>
    private static string EscapeUnescapedControlChars(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length + 64);
        bool inString = false;
        bool escapeNext = false;
        foreach (var ch in s)
        {
            if (escapeNext) { sb.Append(ch); escapeNext = false; continue; }
            if (ch == '\\') { sb.Append(ch); escapeNext = true; continue; }
            if (ch == '"') { sb.Append(ch); inString = !inString; continue; }
            if (inString)
            {
                switch (ch)
                {
                    case '\n': sb.Append("\\n"); continue;
                    case '\r': sb.Append("\\r"); continue;
                    case '\t': sb.Append("\\t"); continue;
                    case '\f': sb.Append("\\f"); continue;
                    case '\b': sb.Append("\\b"); continue;
                }
            }
            sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Aggressive regex-based field extraction. Survives:
    ///   - Literal newlines inside string values
    ///   - Truncated JSON (no closing quote or brace)
    ///   - JSON with extra prose before/after the object
    /// </summary>
    private static bool TryRegexExtractRobust(string s, out (string category, string summary, string reply) result)
    {
        result = ("", "", "");
        try
        {
            // category + summary use simple non-greedy quote matching (these fields rarely contain newlines)
            var catM = System.Text.RegularExpressions.Regex.Match(s, "\"category\"\\s*:\\s*\"([^\"]*)\"");
            var sumM = System.Text.RegularExpressions.Regex.Match(s, "\"summary\"\\s*:\\s*\"([^\"]*)\"");

            // For reply: find the opening "reply": " then grab everything until either:
            //   (a) the closing "} pattern at end of object (well-formed case)
            //   (b) end of string (truncated case)
            var replyStart = System.Text.RegularExpressions.Regex.Match(s, "\"reply\"\\s*:\\s*\"");
            if (!replyStart.Success) return false;

            var startIndex = replyStart.Index + replyStart.Length;
            var remaining = s[startIndex..];

            // Try well-formed close: "} or " followed by EOL
            string reply;
            var closeMatch = System.Text.RegularExpressions.Regex.Match(remaining, "\"\\s*}?\\s*\\Z",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            if (closeMatch.Success && closeMatch.Index > 0)
            {
                reply = remaining[..closeMatch.Index];
            }
            else
            {
                // Truncated — take everything, then trim trailing garbage
                reply = remaining.TrimEnd('"', '}', ' ', '\n', '\r', '\t', ',');
            }

            if (string.IsNullOrWhiteSpace(reply)) return false;

            var category = catM.Success ? catM.Groups[1].Value : "other";
            var summary = sumM.Success ? sumM.Groups[1].Value : "";

            // Unescape standard JSON escapes that survived as literal text in the extracted blob
            reply = UnescapeJsonString(reply);
            summary = UnescapeJsonString(summary);

            // Convert paragraphs into HTML so the rich-text editor renders nicely
            reply = ConvertPlainTextToHtml(reply);

            result = (category, summary, reply);
            return true;
        }
        catch { return false; }
    }

    private static string UnescapeJsonString(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s
            .Replace("\\n", "\n")
            .Replace("\\r", "\r")
            .Replace("\\t", "\t")
            .Replace("\\\"", "\"")
            .Replace("\\/", "/")
            .Replace("\\\\", "\\");
    }

    private static string ConvertPlainTextToHtml(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        if (text.Contains("<p>", StringComparison.OrdinalIgnoreCase) || text.Contains("<br", StringComparison.OrdinalIgnoreCase))
            return text;
        var paragraphs = System.Text.RegularExpressions.Regex.Split(text, "\\n\\s*\\n");
        var sb = new System.Text.StringBuilder();
        foreach (var p in paragraphs)
        {
            var trimmed = p.Trim().Replace("\n", "<br>");
            if (trimmed.Length > 0) sb.Append("<p>").Append(trimmed).Append("</p>");
        }
        return sb.Length > 0 ? sb.ToString() : text;
    }

    private static string StripAndTruncate(string html, int max)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        // Preserve links so the AI can see URLs (e.g. the company website) — "TEXT (URL)".
        var withLinks = System.Text.RegularExpressions.Regex.Replace(
            html,
            "<a\\b[^>]*?href\\s*=\\s*[\"']([^\"']+)[\"'][^>]*>(.*?)</a>",
            m =>
            {
                var url = m.Groups[1].Value.Trim();
                var text = System.Text.RegularExpressions.Regex.Replace(m.Groups[2].Value, "<[^>]+>", "").Trim();
                return string.IsNullOrEmpty(text) ? url : $"{text} ({url})";
            },
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
        var stripped = System.Text.RegularExpressions.Regex.Replace(withLinks, "<[^>]+>", " ");
        stripped = System.Net.WebUtility.HtmlDecode(stripped);
        stripped = System.Text.RegularExpressions.Regex.Replace(stripped, "\\s+", " ").Trim();
        return stripped.Length > max ? stripped[..max] : stripped;
    }
}
