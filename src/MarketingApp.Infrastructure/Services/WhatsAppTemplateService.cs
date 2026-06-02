using System.Text.Json;
using System.Text.RegularExpressions;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Domain.Entities;
using MarketingApp.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Infrastructure.Services;

/// <summary>
/// Syncs WhatsApp approved templates from the Meta Graph API into the local cache, and lists them (L2).
/// </summary>
public class WhatsAppTemplateService : IWhatsAppTemplateService
{
    private readonly IGenericRepository<SmtpGroup> _groupRepo;
    private readonly IGenericRepository<WhatsAppTemplate> _templateRepo;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WhatsAppTemplateService> _logger;
    private const string META_API_URL = "https://graph.facebook.com/v18.0";

    public WhatsAppTemplateService(
        IGenericRepository<SmtpGroup> groupRepo,
        IGenericRepository<WhatsAppTemplate> templateRepo,
        IHttpClientFactory httpClientFactory,
        ILogger<WhatsAppTemplateService> logger)
    {
        _groupRepo = groupRepo;
        _templateRepo = templateRepo;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<WhatsAppTemplateSyncResultDto> SyncFromMetaAsync(Guid smtpGroupId, CancellationToken ct = default)
    {
        var group = await _groupRepo.GetByIdAsync(smtpGroupId, ct)
            ?? throw new NotFoundException("SmtpGroup", smtpGroupId);

        if (string.IsNullOrWhiteSpace(group.WhatsAppApiKey) || string.IsNullOrWhiteSpace(group.WhatsAppBusinessAccountId))
            throw new AppValidationException("This group has no WhatsApp Business Account ID / access token configured. Add them first.");

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {group.WhatsAppApiKey}");

        var url = $"{META_API_URL}/{group.WhatsAppBusinessAccountId}/message_templates?limit=200";
        var response = await client.GetAsync(url, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("[WA TEMPLATES] Sync failed {Status}: {Body}", response.StatusCode, json);
            throw new AppValidationException($"Meta API returned {(int)response.StatusCode}. Check the WhatsApp Business Account ID + access token.");
        }

        var parsed = ParseMetaTemplates(json, smtpGroupId);

        // Upsert by (group, name, language).
        var existing = (await _templateRepo.FindAsync(t => t.SmtpGroupId == smtpGroupId, ct)).ToList();
        var now = DateTime.UtcNow;
        var approved = 0;
        foreach (var t in parsed)
        {
            if (t.Status == "APPROVED") approved++;
            var match = existing.FirstOrDefault(e =>
                e.Name == t.Name && e.Language == t.Language);
            if (match is null)
            {
                t.SyncedAt = now;
                t.CreatedAt = now;
                await _templateRepo.AddAsync(t, ct);
            }
            else
            {
                match.Category = t.Category;
                match.Status = t.Status;
                match.BodyText = t.BodyText;
                match.VariableCount = t.VariableCount;
                match.HeaderType = t.HeaderType;
                match.MetaTemplateId = t.MetaTemplateId;
                match.SyncedAt = now;
                await _templateRepo.UpdateAsync(match, ct);
            }
        }

        _logger.LogInformation("[WA TEMPLATES] Synced {Count} templates ({Approved} approved) for group {Group}",
            parsed.Count, approved, smtpGroupId);
        return new WhatsAppTemplateSyncResultDto
        {
            Synced = parsed.Count,
            Approved = approved,
            Message = $"Synced {parsed.Count} templates ({approved} approved).",
        };
    }

    public async Task<IEnumerable<WhatsAppTemplateDto>> ListAsync(Guid smtpGroupId, bool approvedOnly = false, CancellationToken ct = default)
    {
        var items = await _templateRepo.FindAsync(t => t.SmtpGroupId == smtpGroupId, ct);
        return items
            .Where(t => !approvedOnly || t.Status == "APPROVED")
            .OrderBy(t => t.Name).ThenBy(t => t.Language)
            .Select(ToDto);
    }

    private static WhatsAppTemplateDto ToDto(WhatsAppTemplate t) => new()
    {
        Id = t.Id, SmtpGroupId = t.SmtpGroupId, Name = t.Name, Language = t.Language,
        Category = t.Category, Status = t.Status, BodyText = t.BodyText,
        VariableCount = t.VariableCount, HeaderType = t.HeaderType, SyncedAt = t.SyncedAt,
    };

    private static readonly Regex VarRe = new(@"\{\{\s*(\d+)\s*\}\}", RegexOptions.Compiled);

    /// <summary>
    /// Parses a Meta Graph `message_templates` response into WhatsAppTemplate rows. Pure + testable.
    /// Extracts the BODY component text (and counts its distinct {{n}} variables) and the HEADER format.
    /// </summary>
    internal static List<WhatsAppTemplate> ParseMetaTemplates(string json, Guid smtpGroupId)
    {
        var result = new List<WhatsAppTemplate>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var t in data.EnumerateArray())
        {
            var name = GetStr(t, "name");
            if (string.IsNullOrWhiteSpace(name)) continue;

            var entity = new WhatsAppTemplate
            {
                SmtpGroupId = smtpGroupId,
                Name = name!,
                Language = GetStr(t, "language") ?? "en_US",
                Category = (GetStr(t, "category") ?? "MARKETING").ToUpperInvariant(),
                Status = (GetStr(t, "status") ?? "PENDING").ToUpperInvariant(),
                MetaTemplateId = GetStr(t, "id"),
            };

            if (t.TryGetProperty("components", out var comps) && comps.ValueKind == JsonValueKind.Array)
            {
                foreach (var comp in comps.EnumerateArray())
                {
                    var type = (GetStr(comp, "type") ?? "").ToUpperInvariant();
                    if (type == "BODY")
                    {
                        entity.BodyText = GetStr(comp, "text") ?? "";
                        var nums = new HashSet<int>();
                        foreach (Match m in VarRe.Matches(entity.BodyText))
                            if (int.TryParse(m.Groups[1].Value, out var n)) nums.Add(n);
                        entity.VariableCount = nums.Count;
                    }
                    else if (type == "HEADER")
                    {
                        entity.HeaderType = (GetStr(comp, "format") ?? "TEXT").ToUpperInvariant();
                    }
                }
            }

            result.Add(entity);
        }
        return result;
    }

    private static string? GetStr(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
