using MarketingApp.Application.DTOs;

namespace MarketingApp.Application.Interfaces;

/// <summary>
/// Per-provider credential vault (P3.5). Stores each provider's key independently and tracks which
/// provider is active per category. Selecting active syncs the chosen credential into SystemSettings
/// so existing AI/image/payment code keeps working unchanged.
/// Categories: "ai" | "image" | "payment".
/// </summary>
public interface IIntegrationCredentialService
{
    Task<IntegrationCategoryDto> GetAsync(string category, CancellationToken ct = default);

    /// <summary>Upsert one provider's credential. Blank ApiKey/SecondarySecret = keep existing.</summary>
    Task<IntegrationCategoryDto> SaveAsync(Guid actorId, string category, string provider, SaveCredentialDto dto, CancellationToken ct = default);

    /// <summary>Make a provider the active one for its category (syncs into SystemSettings).</summary>
    Task<IntegrationCategoryDto> SetActiveAsync(Guid actorId, string category, string provider, CancellationToken ct = default);
}
