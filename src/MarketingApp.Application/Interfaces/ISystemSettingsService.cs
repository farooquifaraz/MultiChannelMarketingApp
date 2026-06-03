using MarketingApp.Application.DTOs;
using MarketingApp.Application.Configuration;

namespace MarketingApp.Application.Interfaces;

public interface ISystemSettingsService
{
    Task<SystemSettingsDto> GetAsync(CancellationToken ct = default);
    Task<SystemSettingsDto> UpdateAsync(Guid updatedByUserId, UpdateSystemSettingsDto dto, CancellationToken ct = default);
    /// <summary>Returns a live CampaignSettings snapshot (used by the campaign job).</summary>
    Task<CampaignSettings> GetCampaignSettingsAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns the RAW, unmasked AI API key for use by the AI reply service.
    /// Separate from <see cref="GetAsync"/> so the masked DTO can't be confused with the raw key.
    /// </summary>
    Task<string?> GetRawAiApiKeyAsync(CancellationToken ct = default);

    /// <summary>Raw (unmasked) fallback-provider API key for the auto-failover path (Day 10).</summary>
    Task<string?> GetRawAiFallbackApiKeyAsync(CancellationToken ct = default);

    /// <summary>Raw (unmasked) image-generation API key (Phase 3).</summary>
    Task<string?> GetRawImageApiKeyAsync(CancellationToken ct = default);
}
