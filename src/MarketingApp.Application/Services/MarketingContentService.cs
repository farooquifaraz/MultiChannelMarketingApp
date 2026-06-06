using System.Text.Json;
using System.Text.RegularExpressions;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Application.Interfaces.AI;
using MarketingApp.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Application.Services;

/// <summary>
/// AI marketing-copy generator (P3.6). Sends a structured prompt (same framework as the standalone
/// GenAI hub) to the active AI text provider via IAiExecutor, then parses the JSON into channel-ready
/// copy. Uses the AI *text* path (free tiers like Gemini text have quota), not the image path.
/// </summary>
public class MarketingContentService : IMarketingContentService
{
    private readonly IAiExecutor _ai;
    private readonly ILogger<MarketingContentService> _logger;

    private const int MaxBrief = 10_000;
    private const int MinBrief = 20;

    public MarketingContentService(IAiExecutor ai, ILogger<MarketingContentService> logger)
    {
        _ai = ai;
        _logger = logger;
    }

    public async Task<MarketingContentDto> GenerateAsync(GenerateContentDto dto, CancellationToken ct = default)
    {
        var brief = (dto.Brief ?? string.Empty).Trim();
        if (brief.Length < MinBrief)
            throw new AppValidationException($"Please provide at least {MinBrief} characters describing the property/offer.");
        if (brief.Length > MaxBrief)
            throw new AppValidationException($"Brief must be {MaxBrief} characters or fewer.");

        var channels = NormalizeChannels(dto.Channels, dto.Channel);

        AiCompletion completion;
        try
        {
            // Multi-channel JSON can be long — give it room so the JSON isn't truncated
            // (truncation = unparseable = raw JSON shown to the user).
            completion = await _ai.GenerateAsync(BuildSystemPrompt(channels), brief, AiResponseShape.PlainText, ct, maxTokens: 4000);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Marketing content generation failed: {Message}", ex.Message);
            var m = ex.Message ?? "";
            // Truly-not-configured → guide to Integrations. Otherwise surface the provider's real reason
            // (e.g. "Incorrect API key", "quota exceeded") so the user knows exactly what to fix.
            if (m.Contains("not configured", StringComparison.OrdinalIgnoreCase)
                || m.Contains("No AI client registered", StringComparison.OrdinalIgnoreCase))
                throw new AppValidationException("No AI provider is enabled. Go to Integrations → AI Text and enable a provider with a valid key.");
            throw new AppValidationException($"AI provider error: {m}");
        }

        var result = ParseContent(completion.RawText);
        result.Provider = completion.Provider;
        return result;
    }

    /// <summary>All channels we know how to write. Order is the canonical "lead" order for shared groups.</summary>
    internal static readonly string[] AllChannels = { "whatsapp", "instagram", "facebook", "email" };

    /// <summary>
    /// Resolve which channels to generate. Accepts the new <c>Channels</c> list or the legacy single
    /// <c>Channel</c> string ("all" / "whatsapp" / ...). Unknown values are ignored; empty → all.
    /// </summary>
    internal static IReadOnlyList<string> NormalizeChannels(List<string>? channels, string? legacy)
    {
        var requested = new List<string>();
        if (channels != null) requested.AddRange(channels);
        if (!string.IsNullOrWhiteSpace(legacy)) requested.Add(legacy!);

        var picked = requested
            .Select(c => c?.Trim().ToLowerInvariant())
            .Where(c => !string.IsNullOrEmpty(c))
            .ToList();

        if (picked.Count == 0 || picked.Contains("all"))
            return AllChannels;

        // Preserve canonical order, de-duplicate, keep only known channels.
        var ordered = AllChannels.Where(picked.Contains!).ToArray();
        return ordered.Length > 0 ? ordered : AllChannels;
    }

    private static readonly Dictionary<string, string> ChannelBlocks = new()
    {
        ["whatsapp"] = """
  "whatsapp": {
    "broadcast": "Full WhatsApp broadcast. Structure: bold header line; blank; 2-line emotional hook; blank; price line; blank; key specs with emojis (one per line); blank; 3 ✅ highlights; blank; ⚡ urgency line; blank; 📞 CTA; 🔗 'Reply YES for details'. 350-500 chars. Use \n between sections.",
    "status_text": "1-line WhatsApp status with 1-2 emojis, max 140 chars"
  }
""",
        ["instagram"] = """
  "instagram": {
    "caption": "Full caption: scroll-stopping first line; blank; 3-line storytelling; blank; highlights with emojis; blank; strong CTA; blank; 8-12 strategic hashtags",
    "reels_hook": "1-line scroll-stopping Reels hook, max 80 chars",
    "story_cta": "1-line Instagram Story CTA, max 100 chars"
  }
""",
        ["facebook"] = """
  "facebook": {
    "post": "Full Facebook post: attention-grabbing first line; blank; 2-3 short value-driven sentences; blank; key features as • bullet lines; blank; a clear CTA with phone/link; blank; 3-5 relevant hashtags. 500-900 chars, friendly-professional, native to Facebook feed. Use \n between sections.",
    "headline": "Short bold link/headline title, max 60 chars",
    "cta": "Button-style CTA label, max 20 chars (e.g. 'Book a viewing')"
  }
""",
        ["email"] = """
  "email": {
    "subject": "Email subject creating urgency, max 60 chars",
    "preview": "Inbox preview text, max 90 chars",
    "body": "Full HTML email body with inline styles, <b> and <br>, 200-300 words, warm + authoritative"
  }
""",
    };

    private const string ImagePromptBlock = """
  "image_prompt": "A rich, production-grade text-to-image prompt (60-90 words) that recreates the EXACT subject from the brief so the result looks like a real professional photograph, not generic stock. Write it as ONE flowing descriptive paragraph and include, in this order: (1) the precise subject pulled from the brief (e.g. exact property type, number of bedrooms, location/community, the 2-3 standout features actually mentioned); (2) the setting and time of day; (3) photographic style + camera and lens (e.g. 'shot on a full-frame DSLR, 24mm wide-angle, f/8'); (4) lighting (e.g. warm golden-hour glow, soft natural light); (5) composition and mood; (6) a color palette that matches the brand feel; (7) ultra-realism keywords (photorealistic, hyper-detailed, 8k, sharp focus, professional real-estate photography, natural materials, realistic reflections). End the paragraph with exactly: 'No text, no watermark, no logos, no distorted shapes, no people unless essential.'"
""";

    /// <summary>Build the structured marketing prompt for ONLY the requested channels (+ image prompt).</summary>
    internal static string BuildSystemPrompt(IReadOnlyList<string> channels)
    {
        var blocks = channels.Where(ChannelBlocks.ContainsKey).Select(c => ChannelBlocks[c].Trim()).ToList();
        blocks.Add(ImagePromptBlock.Trim());
        var body = string.Join(",\n", blocks);
        return $$"""
You are a senior marketing director who writes campaigns that SELL — not just describe.
Given the brief, return ONLY a valid JSON object (no markdown fences, no commentary) with EXACTLY these keys:
{
{{body}}
}
Tone: professional, persuasive, channel-appropriate. Every emoji must serve a purpose.
The image_prompt must be SPECIFIC to this brief — never generic. Pull concrete nouns and adjectives from the brief so a designer reading it could picture the exact scene.
""";
    }

    /// <summary>
    /// Parses the AI's JSON (tolerating ``` fences / surrounding prose) into the DTO. Pure → testable.
    /// </summary>
    internal static MarketingContentDto ParseContent(string raw)
    {
        var json = ExtractJson(raw);
        var dto = new MarketingContentDto();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("whatsapp", out var wa))
            {
                dto.WhatsApp.Broadcast = Str(wa, "broadcast");
                dto.WhatsApp.StatusText = Str(wa, "status_text");
            }
            if (root.TryGetProperty("instagram", out var ig))
            {
                dto.Instagram.Caption = Str(ig, "caption");
                dto.Instagram.ReelsHook = Str(ig, "reels_hook");
                dto.Instagram.StoryCta = Str(ig, "story_cta");
            }
            if (root.TryGetProperty("facebook", out var fb))
            {
                dto.Facebook.Post = Str(fb, "post");
                dto.Facebook.Headline = Str(fb, "headline");
                dto.Facebook.Cta = Str(fb, "cta");
            }
            if (root.TryGetProperty("email", out var em))
            {
                dto.Email.Subject = Str(em, "subject");
                dto.Email.Preview = Str(em, "preview");
                dto.Email.Body = Str(em, "body");
            }
            dto.ImagePrompt = Str(root, "image_prompt");
        }
        catch (JsonException)
        {
            // Couldn't parse structured JSON — fall back to using the raw text as the WhatsApp copy
            // so the user still gets something usable rather than an error.
            dto.WhatsApp.Broadcast = raw.Trim();
        }
        return dto;
    }

    private static string ExtractJson(string raw)
    {
        var cleaned = (raw ?? string.Empty).Trim();
        cleaned = Regex.Replace(cleaned, "^```(?:json)?\\s*", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, "\\s*```$", "");
        if (cleaned.StartsWith("{")) return cleaned;
        var m = Regex.Match(cleaned, "\\{.*\\}", RegexOptions.Singleline);
        return m.Success ? m.Value : cleaned;
    }

    private static string Str(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";
}
