using MarketingApp.Application.Configuration;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MarketingApp.Application.Services;

public class SystemSettingsService : ISystemSettingsService
{
    private readonly IGenericRepository<SystemSettings> _repo;
    private readonly CampaignSettings _appsettingsFallback;
    private readonly ILogger<SystemSettingsService> _logger;

    public SystemSettingsService(
        IGenericRepository<SystemSettings> repo,
        IOptions<CampaignSettings> appsettingsFallback,
        ILogger<SystemSettingsService> logger)
    {
        _repo = repo;
        _appsettingsFallback = appsettingsFallback.Value;
        _logger = logger;
    }

    /// <summary>Get-or-create the singleton settings row. First call seeds from appsettings.json.</summary>
    private async Task<SystemSettings> GetOrSeedAsync(CancellationToken ct)
    {
        // SystemSettings has int PK, so we can't use the Guid-based GetByIdAsync — use FindAsync instead.
        var existing = (await _repo.FindAsync(s => s.Id == 1, ct)).FirstOrDefault();
        if (existing is not null) return existing;

        var seeded = new SystemSettings
        {
            Id = 1,
            BatchSize = _appsettingsFallback.BatchSize,
            DelayBetweenBatchesMs = _appsettingsFallback.DelayBetweenBatchesMs,
            DelayBetweenMessagesMs = _appsettingsFallback.DelayBetweenMessagesMs,
            MaxMessagesPerMinute = _appsettingsFallback.MaxMessagesPerMinute,
            UpdatedAt = DateTime.UtcNow
        };
        await _repo.AddAsync(seeded, ct);
        _logger.LogInformation("Seeded SystemSettings from appsettings.json");
        return seeded;
    }

    public async Task<SystemSettingsDto> GetAsync(CancellationToken ct = default)
    {
        var s = await GetOrSeedAsync(ct);
        return ToDto(s);
    }

    public async Task<SystemSettingsDto> UpdateAsync(Guid updatedByUserId, UpdateSystemSettingsDto dto, CancellationToken ct = default)
    {
        var s = await GetOrSeedAsync(ct);
        s.BatchSize = dto.BatchSize;
        s.DelayBetweenBatchesMs = dto.DelayBetweenBatchesMs;
        s.DelayBetweenMessagesMs = dto.DelayBetweenMessagesMs;
        s.MaxMessagesPerMinute = dto.MaxMessagesPerMinute;
        s.AllowUsersToSeeSharedTemplates = dto.AllowUsersToSeeSharedTemplates;
        s.AllowUsersToSeeSharedContacts = dto.AllowUsersToSeeSharedContacts;

        if (!string.IsNullOrWhiteSpace(dto.PlatformName)) s.PlatformName = dto.PlatformName;
        s.LogoUrl = string.IsNullOrWhiteSpace(dto.LogoUrl) ? null : dto.LogoUrl;
        if (!string.IsNullOrWhiteSpace(dto.PrimaryColor)) s.PrimaryColor = dto.PrimaryColor;
        if (!string.IsNullOrWhiteSpace(dto.AvatarServiceUrl)) s.AvatarServiceUrl = dto.AvatarServiceUrl;
        if (!string.IsNullOrWhiteSpace(dto.DefaultLocale)) s.DefaultLocale = dto.DefaultLocale;
        if (!string.IsNullOrWhiteSpace(dto.DefaultDateFormat)) s.DefaultDateFormat = dto.DefaultDateFormat;
        if (dto.PasswordMinLength >= 4 && dto.PasswordMinLength <= 64) s.PasswordMinLength = dto.PasswordMinLength;
        if (dto.MaxFileUploadSizeMb >= 1 && dto.MaxFileUploadSizeMb <= 1024) s.MaxFileUploadSizeMb = dto.MaxFileUploadSizeMb;

        // === Day 7 G3 + G5 ===
        if (!string.IsNullOrWhiteSpace(dto.InboxPollingCron)) s.InboxPollingCron = dto.InboxPollingCron;
        if (!string.IsNullOrWhiteSpace(dto.AiProvider)) s.AiProvider = dto.AiProvider;
        // Treat null AI key as "no change" (so frontend can submit form without re-entering key).
        // Empty string explicitly clears it.
        if (dto.AiApiKey is not null) s.AiApiKey = string.IsNullOrEmpty(dto.AiApiKey) ? null : dto.AiApiKey;
        s.AiBaseUrl = string.IsNullOrWhiteSpace(dto.AiBaseUrl) ? null : dto.AiBaseUrl;
        if (!string.IsNullOrWhiteSpace(dto.AiModel)) s.AiModel = dto.AiModel;
        if (!string.IsNullOrWhiteSpace(dto.AiSystemPrompt)) s.AiSystemPrompt = dto.AiSystemPrompt;
        if (dto.AiMaxTokens > 0 && dto.AiMaxTokens <= 8000) s.AiMaxTokens = dto.AiMaxTokens;
        if (dto.AiTemperature >= 0 && dto.AiTemperature <= 2) s.AiTemperature = dto.AiTemperature;
        if (dto.AiTimeoutSeconds >= 5 && dto.AiTimeoutSeconds <= 300) s.AiTimeoutSeconds = dto.AiTimeoutSeconds;
        s.AiAllowSendRecipientPii = dto.AiAllowSendRecipientPii;
        if (!string.IsNullOrWhiteSpace(dto.AiAllowedCategoriesCsv)) s.AiAllowedCategoriesCsv = dto.AiAllowedCategoriesCsv;

        // === Day 10: fallback provider ===
        if (!string.IsNullOrWhiteSpace(dto.AiFallbackProvider)) s.AiFallbackProvider = dto.AiFallbackProvider;
        if (dto.AiFallbackApiKey is not null) s.AiFallbackApiKey = string.IsNullOrEmpty(dto.AiFallbackApiKey) ? null : dto.AiFallbackApiKey;
        s.AiFallbackBaseUrl = string.IsNullOrWhiteSpace(dto.AiFallbackBaseUrl) ? null : dto.AiFallbackBaseUrl;
        if (!string.IsNullOrWhiteSpace(dto.AiFallbackModel)) s.AiFallbackModel = dto.AiFallbackModel;

        s.UpdatedByUserId = updatedByUserId;
        s.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(s, ct);
        _logger.LogInformation("System settings updated by user {UserId}", updatedByUserId);
        return ToDto(s);
    }

    public async Task<string?> GetRawAiApiKeyAsync(CancellationToken ct = default)
    {
        var s = await GetOrSeedAsync(ct);
        return s.AiApiKey;
    }

    public async Task<string?> GetRawAiFallbackApiKeyAsync(CancellationToken ct = default)
    {
        var s = await GetOrSeedAsync(ct);
        return s.AiFallbackApiKey;
    }

    public async Task<CampaignSettings> GetCampaignSettingsAsync(CancellationToken ct = default)
    {
        var s = await GetOrSeedAsync(ct);
        return new CampaignSettings
        {
            BatchSize = s.BatchSize,
            DelayBetweenBatchesMs = s.DelayBetweenBatchesMs,
            DelayBetweenMessagesMs = s.DelayBetweenMessagesMs,
            MaxMessagesPerMinute = s.MaxMessagesPerMinute,
        };
    }

    private static SystemSettingsDto ToDto(SystemSettings s) => new()
    {
        BatchSize = s.BatchSize,
        DelayBetweenBatchesMs = s.DelayBetweenBatchesMs,
        DelayBetweenMessagesMs = s.DelayBetweenMessagesMs,
        MaxMessagesPerMinute = s.MaxMessagesPerMinute,
        AllowUsersToSeeSharedTemplates = s.AllowUsersToSeeSharedTemplates,
        AllowUsersToSeeSharedContacts = s.AllowUsersToSeeSharedContacts,
        PlatformName = s.PlatformName,
        LogoUrl = s.LogoUrl,
        PrimaryColor = s.PrimaryColor,
        AvatarServiceUrl = s.AvatarServiceUrl,
        DefaultLocale = s.DefaultLocale,
        DefaultDateFormat = s.DefaultDateFormat,
        PasswordMinLength = s.PasswordMinLength,
        MaxFileUploadSizeMb = s.MaxFileUploadSizeMb,
        // Day 7 G3 + G5
        InboxPollingCron = s.InboxPollingCron,
        AiProvider = s.AiProvider,
        AiApiKeyMasked = MaskKey(s.AiApiKey),
        AiBaseUrl = s.AiBaseUrl,
        AiModel = s.AiModel,
        AiSystemPrompt = s.AiSystemPrompt,
        AiMaxTokens = s.AiMaxTokens,
        AiTemperature = s.AiTemperature,
        AiTimeoutSeconds = s.AiTimeoutSeconds,
        AiAllowSendRecipientPii = s.AiAllowSendRecipientPii,
        AiAllowedCategoriesCsv = s.AiAllowedCategoriesCsv,
        AiFallbackProvider = s.AiFallbackProvider,
        AiFallbackApiKeyMasked = MaskKey(s.AiFallbackApiKey),
        AiFallbackBaseUrl = s.AiFallbackBaseUrl,
        AiFallbackModel = s.AiFallbackModel,
        UpdatedAt = s.UpdatedAt,
    };

    /// <summary>Mask all but the last 4 characters of an AI API key so the UI can show "•••••3kJ2" without leaking the secret.</summary>
    private static string? MaskKey(string? key) =>
        string.IsNullOrEmpty(key) ? null :
        key.Length <= 4 ? new string('•', key.Length) :
        new string('•', 8) + key[^4..];
}
