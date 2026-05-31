using System.Text.Json;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Application.Interfaces.AI;
using MarketingApp.Domain.Entities;
using MarketingApp.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Application.Services.AI;

/// <summary>
/// "Ask AI" chat agent for inbox threads (Day 9). The user asks questions ABOUT the conversation
/// (summaries, action items, "what should I do?") — this does NOT send anything to the recipient.
/// Multi-turn + persisted in inbox_ai_chats. Reuses the same admin-configured AI provider.
/// </summary>
public class AiChatService : IAiChatService
{
    private readonly IGenericRepository<InboxMessage> _inboxRepo;
    private readonly IGenericRepository<OutboundReply> _outboundRepo;
    private readonly IGenericRepository<InboxAiChat> _chatRepo;
    private readonly IGenericRepository<CampaignMessage> _msgRepo;
    private readonly IGenericRepository<Campaign> _campaignRepo;
    private readonly IGenericRepository<MessageTemplate> _templateRepo;
    private readonly IAiExecutor _aiExecutor;
    private readonly ISystemSettingsService _systemSettings;
    private readonly ILogger<AiChatService> _logger;

    private const int MaxPriorTurns = 10;

    private const string ChatSystemPrompt =
        "You are an AI assistant helping the USER understand and act on an email conversation in their inbox. " +
        "Answer the user's questions about this conversation concisely and helpfully, in the user's language. " +
        "You are NOT writing a reply to the recipient — you are advising the user (summaries, action items, intent, suggestions). " +
        "If the user asks you to draft a reply, provide it as guidance they can copy. " +
        "Use short paragraphs and bullet points where helpful. Keep answers focused and under 250 words unless asked for more.\n\n" +
        "OUTPUT FORMAT — respond with EXACTLY this JSON object and nothing else:\n" +
        "{\n" +
        "  \"answer\": \"<your full answer to the user, as readable text; use \\n for line breaks and '- ' for bullet points>\",\n" +
        "  \"suggestions\": [\"<short follow-up question 1>\", \"<short follow-up question 2>\", \"<short follow-up question 3>\"]\n" +
        "}\n" +
        "Put your ENTIRE response inside the \"answer\" field as plain readable text — do NOT invent other top-level keys " +
        "(no \"summary\", no \"key_points\", etc.). \"suggestions\" must be 3 short, relevant next questions the user might ask.";

    public AiChatService(
        IGenericRepository<InboxMessage> inboxRepo,
        IGenericRepository<OutboundReply> outboundRepo,
        IGenericRepository<InboxAiChat> chatRepo,
        IGenericRepository<CampaignMessage> msgRepo,
        IGenericRepository<Campaign> campaignRepo,
        IGenericRepository<MessageTemplate> templateRepo,
        IAiExecutor aiExecutor,
        ISystemSettingsService systemSettings,
        ILogger<AiChatService> logger)
    {
        _inboxRepo = inboxRepo;
        _outboundRepo = outboundRepo;
        _chatRepo = chatRepo;
        _msgRepo = msgRepo;
        _campaignRepo = campaignRepo;
        _templateRepo = templateRepo;
        _aiExecutor = aiExecutor;
        _systemSettings = systemSettings;
        _logger = logger;
    }

    public async Task<AiChatTurnDto> AskAsync(Guid threadId, string question, Guid requesterUserId, bool isAdmin, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(question))
            throw new AppValidationException(new List<string> { "Question is required." });

        var inbound = await EnsureThreadAccessAsync(threadId, requesterUserId, isAdmin, ct);

        var ownerUserId = inbound.First().OwnerUserId;

        // Build context BEFORE persisting anything — prior turns + email transcript.
        var transcript = await BuildContextAsync(threadId, inbound, ct);
        var priorTurns = (await _chatRepo.FindAsync(c => c.ThreadId == threadId, ct))
            .OrderBy(c => c.CreatedAt)
            .TakeLast(MaxPriorTurns)
            .ToList();

        var userPrompt = BuildChatPrompt(transcript, priorTurns, question.Trim());

        AiCompletion completion;
        try
        {
            // Executor handles provider resolution + auto-fallback on quota (Day 10).
            completion = await _aiExecutor.GenerateAsync(ChatSystemPrompt, userPrompt, AiResponseShape.ChatJson, ct);
        }
        catch (Exception ex)
        {
            // Don't persist ANYTHING on failure — keep the chat clean. The frontend shows the error as
            // a toast and keeps the user's typed question so they can retry once quota/limits clear.
            _logger.LogWarning(ex, "[AiChat] Ask failed for thread {ThreadId}", threadId);
            throw new InvalidOperationException(ex.Message, ex);
        }

        // ChatJson shape returns { answer, suggestions[] }. Parse robustly; fall back gracefully.
        var (answer, suggestions) = ParseChatJson(completion.RawText);
        if (string.IsNullOrWhiteSpace(answer)) answer = "(The AI returned an empty response. Try rephrasing your question.)";

        // Drop suggestions the user has already asked (case-insensitive), so chips stay fresh + non-repeating.
        var asked = priorTurns.Where(t => t.Role == "user").Select(t => t.Content.Trim().ToLowerInvariant()).ToHashSet();
        asked.Add(question.Trim().ToLowerInvariant());
        suggestions = suggestions
            .Where(s => !string.IsNullOrWhiteSpace(s) && !asked.Contains(s.Trim().ToLowerInvariant()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3).ToList();

        // Success — persist BOTH turns now (atomic-ish: user then assistant).
        await _chatRepo.AddAsync(new InboxAiChat
        {
            ThreadId = threadId, OwnerUserId = ownerUserId, Role = "user", Content = question.Trim(),
        }, ct);

        var assistantTurn = new InboxAiChat
        {
            ThreadId = threadId, OwnerUserId = ownerUserId, Role = "assistant", Content = answer,
            InputTokens = completion.InputTokens, OutputTokens = completion.OutputTokens, ProviderUsed = completion.Provider,
            Suggestions = suggestions.Count > 0 ? JsonSerializer.Serialize(suggestions) : null,
        };
        await _chatRepo.AddAsync(assistantTurn, ct);

        return ToDto(assistantTurn);
    }

    public async Task<IReadOnlyList<AiChatTurnDto>> GetHistoryAsync(Guid threadId, Guid requesterUserId, bool isAdmin, CancellationToken ct)
    {
        await EnsureThreadAccessAsync(threadId, requesterUserId, isAdmin, ct);
        var turns = (await _chatRepo.FindAsync(c => c.ThreadId == threadId, ct))
            .OrderBy(c => c.CreatedAt)
            .Select(ToDto)
            .ToList();
        return turns;
    }

    public async Task ClearHistoryAsync(Guid threadId, Guid requesterUserId, bool isAdmin, CancellationToken ct)
    {
        await EnsureThreadAccessAsync(threadId, requesterUserId, isAdmin, ct);
        var turns = (await _chatRepo.FindAsync(c => c.ThreadId == threadId, ct)).ToList();
        foreach (var t in turns) await _chatRepo.DeleteAsync(t, ct);
    }

    // ---- helpers ----

    private async Task<List<InboxMessage>> EnsureThreadAccessAsync(Guid threadId, Guid requesterUserId, bool isAdmin, CancellationToken ct)
    {
        var inbound = (await _inboxRepo.FindAsync(m => m.ThreadId == threadId, ct)).ToList();
        if (inbound.Count == 0) throw new NotFoundException("Thread", threadId);
        if (!isAdmin && inbound.All(m => m.OwnerUserId != requesterUserId))
            throw new ForbiddenException();
        return inbound;
    }

    private async Task<string> BuildContextAsync(Guid threadId, List<InboxMessage> inbound, CancellationToken ct)
    {
        var outbound = (await _outboundRepo.FindAsync(o => o.ThreadId == threadId && o.SendSucceeded, ct)).ToList();
        var entries = new List<(DateTime at, string line)>();
        foreach (var m in inbound)
            entries.Add((m.ReceivedAt, $"[{m.ReceivedAt:yyyy-MM-dd HH:mm}] {m.FromName ?? m.FromEmail}: {Strip(m.TextBody ?? m.HtmlBody ?? "", 1500)}"));
        foreach (var o in outbound)
            entries.Add((o.SentAt, $"[{o.SentAt:yyyy-MM-dd HH:mm}] You: {Strip(o.BodyHtml ?? "", 1500)}"));

        var sb = new System.Text.StringBuilder();
        var subject = inbound.OrderBy(m => m.ReceivedAt).First().Subject;
        sb.AppendLine($"EMAIL SUBJECT: {subject}");

        // Include the ORIGINAL campaign email body — the website / offer details (with links) usually
        // live here, not in the recipient's short reply. Links are preserved as "text (url)".
        var matched = inbound.FirstOrDefault(m => m.MatchedCampaignMessageId.HasValue);
        if (matched?.MatchedCampaignMessageId is Guid cmId)
        {
            var cm = await _msgRepo.GetByIdAsync(cmId, ct);
            if (cm is not null)
            {
                var campaign = await _campaignRepo.GetByIdAsync(cm.CampaignId, ct);
                if (campaign is not null)
                {
                    var template = await _templateRepo.GetByIdAsync(campaign.TemplateId, ct);
                    if (template is not null)
                    {
                        sb.AppendLine("ORIGINAL CAMPAIGN EMAIL (what you sent — contains links/offer details):");
                        if (!string.IsNullOrWhiteSpace(template.Subject)) sb.AppendLine($"  Subject: {template.Subject}");
                        sb.AppendLine($"  Body: {Strip(template.Body ?? "", 2000)}");
                        sb.AppendLine();
                    }
                }
            }
        }

        sb.AppendLine("CONVERSATION (oldest first):");
        foreach (var e in entries.OrderBy(e => e.at)) sb.AppendLine(e.line);
        return sb.ToString();
    }

    private static string BuildChatPrompt(string transcript, List<InboxAiChat> priorTurns, string question)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(transcript);
        sb.AppendLine();
        if (priorTurns.Count > 0)
        {
            sb.AppendLine("--- PRIOR Q&A IN THIS CHAT ---");
            foreach (var t in priorTurns)
                sb.AppendLine($"{(t.Role == "user" ? "User" : "Assistant")}: {Strip(t.Content, 800)}");
            sb.AppendLine();
        }
        sb.AppendLine($"USER'S QUESTION: {question}");
        return sb.ToString();
    }

    private static AiChatTurnDto ToDto(InboxAiChat c) => new()
    {
        Id = c.Id, Role = c.Role, Content = c.Content, CreatedAt = c.CreatedAt,
        ProviderUsed = c.ProviderUsed, InputTokens = c.InputTokens, OutputTokens = c.OutputTokens,
        Suggestions = ParseSuggestions(c.Suggestions),
    };

    private static List<string> ParseSuggestions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json);
            return list?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? new();
        }
        catch { return new(); }
    }

    /// <summary>
    /// Parse the ChatJson response { answer, suggestions[] }. Tolerates code fences + minor malformation.
    /// If parsing fails entirely, treats the whole raw text as the answer (so the user still sees something).
    /// </summary>
    private static (string answer, List<string> suggestions) ParseChatJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return ("", new());
        var cleaned = raw.Trim();
        // strip ```json fences
        if (cleaned.StartsWith("```"))
        {
            var nl = cleaned.IndexOf('\n');
            if (nl > 0) cleaned = cleaned[(nl + 1)..];
            var last = cleaned.LastIndexOf("```", StringComparison.Ordinal);
            if (last > 0) cleaned = cleaned[..last];
            cleaned = cleaned.Trim();
        }
        try
        {
            using var doc = JsonDocument.Parse(cleaned);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                var sugg = new List<string>();
                if (root.TryGetProperty("suggestions", out var s) && s.ValueKind == JsonValueKind.Array)
                    foreach (var item in s.EnumerateArray())
                    {
                        var v = item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString();
                        if (!string.IsNullOrWhiteSpace(v)) sugg.Add(v.Trim());
                    }

                // Preferred contract: { answer, suggestions }.
                if (root.TryGetProperty("answer", out var a) && a.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(a.GetString()))
                    return (a.GetString()!.Trim(), sugg);

                // Defensive: the model used a DIFFERENT shape (e.g. {summary, key_points}). NEVER dump raw
                // JSON to the user — flatten every value into readable text so the answer always looks clean.
                var flattened = FlattenJsonObject(root);
                if (!string.IsNullOrWhiteSpace(flattened)) return (flattened.Trim(), sugg);
            }
        }
        catch { /* fall through */ }
        // Couldn't parse as JSON at all — the model returned prose. Use raw text as the answer.
        return (raw.Trim(), new());
    }

    /// <summary>Converts an arbitrary JSON object into clean human-readable text. Used when the model ignores
    /// the { answer, suggestions } contract and returns its own keys — guarantees the user never sees raw JSON.</summary>
    private static string FlattenJsonObject(JsonElement obj)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var prop in obj.EnumerateObject())
        {
            // Skip the suggestions array — it's surfaced as chips, not body text.
            if (prop.NameEquals("suggestions")) continue;
            AppendJsonValue(sb, prop.Value, 0);
        }
        return sb.ToString().Trim();
    }

    private static void AppendJsonValue(System.Text.StringBuilder sb, JsonElement value, int depth)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                sb.AppendLine(value.GetString());
                break;
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                sb.AppendLine(value.ToString());
                break;
            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String) sb.AppendLine($"- {item.GetString()}");
                    else if (item.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False) sb.AppendLine($"- {item}");
                    else AppendJsonValue(sb, item, depth + 1);
                }
                break;
            case JsonValueKind.Object:
                foreach (var p in value.EnumerateObject()) AppendJsonValue(sb, p.Value, depth + 1);
                break;
        }
    }

    private static string Strip(string s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        // PRESERVE links: convert <a href="URL">TEXT</a> → "TEXT (URL)" so the AI can SEE URLs
        // (e.g. the company website) instead of losing them when tags are stripped.
        var withLinks = System.Text.RegularExpressions.Regex.Replace(
            s,
            "<a\\b[^>]*?href\\s*=\\s*[\"']([^\"']+)[\"'][^>]*>(.*?)</a>",
            m =>
            {
                var url = m.Groups[1].Value.Trim();
                var text = System.Text.RegularExpressions.Regex.Replace(m.Groups[2].Value, "<[^>]+>", "").Trim();
                // Skip our own tracking-redirect URLs — surface the human-meaningful text + raw url.
                return string.IsNullOrEmpty(text) ? url : $"{text} ({url})";
            },
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

        var stripped = System.Text.RegularExpressions.Regex.Replace(withLinks, "<[^>]+>", " ");
        stripped = System.Net.WebUtility.HtmlDecode(stripped);
        stripped = System.Text.RegularExpressions.Regex.Replace(stripped, "\\s+", " ").Trim();
        return stripped.Length > max ? stripped[..max] : stripped;
    }
}
