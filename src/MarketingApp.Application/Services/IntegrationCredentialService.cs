using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Domain.Entities;
using MarketingApp.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Application.Services;

/// <summary>
/// Per-provider credential vault (P3.5). The active provider per category lives in SystemSettings;
/// this vault remembers every provider's key so switching active never loses another key. Saving or
/// activating a provider syncs its credential into the SystemSettings active fields, so AiExecutor /
/// ImageGenerationService / CheckoutService keep reading from SystemSettings exactly as before.
/// </summary>
public class IntegrationCredentialService : IIntegrationCredentialService
{
    private static readonly string[] Categories = { "ai", "image", "payment" };

    private readonly IGenericRepository<IntegrationCredential> _repo;
    private readonly IGenericRepository<SystemSettings> _settingsRepo;
    private readonly IAuditService _audit;
    private readonly ILogger<IntegrationCredentialService> _logger;

    public IntegrationCredentialService(
        IGenericRepository<IntegrationCredential> repo,
        IGenericRepository<SystemSettings> settingsRepo,
        IAuditService audit,
        ILogger<IntegrationCredentialService> logger)
    {
        _repo = repo;
        _settingsRepo = settingsRepo;
        _audit = audit;
        _logger = logger;
    }

    public async Task<IntegrationCategoryDto> GetAsync(string category, CancellationToken ct = default)
    {
        category = Normalize(category);
        var settings = await GetSettingsAsync(ct);
        var active = ActiveProvider(settings, category);
        var creds = (await _repo.FindAsync(c => c.Category == category, ct))
            .OrderBy(c => c.Provider)
            .Select(c => ToDto(c, active))
            .ToList();
        return new IntegrationCategoryDto { Category = category, ActiveProvider = active, Credentials = creds };
    }

    public async Task<IntegrationCategoryDto> SaveAsync(Guid actorId, string category, string provider, SaveCredentialDto dto, CancellationToken ct = default)
    {
        category = Normalize(category);
        provider = (provider ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(provider)) throw new AppValidationException("Provider is required.");

        var existing = (await _repo.FindAsync(c => c.Category == category && c.Provider == provider, ct)).FirstOrDefault();
        if (existing is null)
        {
            existing = new IntegrationCredential { Category = category, Provider = provider };
            // blank key on create = no key yet (allowed for keyless providers like mock/pollinations)
            existing.ApiKey = string.IsNullOrWhiteSpace(dto.ApiKey) ? null : dto.ApiKey.Trim();
            existing.SecondarySecret = string.IsNullOrWhiteSpace(dto.SecondarySecret) ? null : dto.SecondarySecret.Trim();
            existing.Model = string.IsNullOrWhiteSpace(dto.Model) ? null : dto.Model.Trim();
            existing.BaseUrl = string.IsNullOrWhiteSpace(dto.BaseUrl) ? null : dto.BaseUrl.Trim();
            await _repo.AddAsync(existing, ct);
        }
        else
        {
            // blank key on update = keep existing
            if (!string.IsNullOrWhiteSpace(dto.ApiKey)) existing.ApiKey = dto.ApiKey.Trim();
            if (!string.IsNullOrWhiteSpace(dto.SecondarySecret)) existing.SecondarySecret = dto.SecondarySecret.Trim();
            existing.Model = string.IsNullOrWhiteSpace(dto.Model) ? existing.Model : dto.Model.Trim();
            existing.BaseUrl = string.IsNullOrWhiteSpace(dto.BaseUrl) ? null : dto.BaseUrl.Trim();
            existing.UpdatedAt = DateTime.UtcNow;
            await _repo.UpdateAsync(existing, ct);
        }

        // If this provider is the active one, push its values into the live settings.
        var settings = await GetSettingsAsync(ct);
        if (string.Equals(ActiveProvider(settings, category), provider, StringComparison.OrdinalIgnoreCase))
        {
            ApplyToSettings(settings, category, existing);
            await _settingsRepo.UpdateAsync(settings, ct);
        }
        await _audit.LogAsync(actorId, "IntegrationCredentialSaved", "IntegrationCredential", existing.Id, new { category, provider }, ct: ct);
        return await GetAsync(category, ct);
    }

    public async Task<IntegrationCategoryDto> SetActiveAsync(Guid actorId, string category, string provider, CancellationToken ct = default)
    {
        category = Normalize(category);
        provider = (provider ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(provider)) throw new AppValidationException("Provider is required.");

        var settings = await GetSettingsAsync(ct);
        var cred = (await _repo.FindAsync(c => c.Category == category && c.Provider == provider, ct)).FirstOrDefault();

        // Guard: a provider that REQUIRES a key cannot be enabled without one — otherwise the feature
        // silently fails later ("No provider enabled"). Keyless providers can always be activated.
        var keyless = new[] { "disabled", "mock", "pollinations" };
        if (!keyless.Contains(provider))
        {
            if (cred is null || string.IsNullOrWhiteSpace(cred.ApiKey))
                throw new AppValidationException($"Add an API key for '{provider}' first, then enable it.");
            if (provider == "openai-compatible" && string.IsNullOrWhiteSpace(cred.BaseUrl))
                throw new AppValidationException("OpenAI-compatible needs a Base URL (e.g. Groq: https://api.groq.com/openai/v1). Add it with the key.");
        }

        SetActiveProvider(settings, category, provider);
        if (cred is not null) ApplyToSettings(settings, category, cred);
        else ClearActiveSecrets(settings, category);
        await _settingsRepo.UpdateAsync(settings, ct);
        await _audit.LogAsync(actorId, "IntegrationProviderActivated", "SystemSettings", null, new { category, provider }, ct: ct);
        _logger.LogInformation("Active {Category} provider set to {Provider}", category, provider);
        return await GetAsync(category, ct);
    }

    // ===== helpers =====

    private static string Normalize(string category)
    {
        category = (category ?? string.Empty).Trim().ToLowerInvariant();
        if (!Categories.Contains(category)) throw new AppValidationException($"Unknown category '{category}'.");
        return category;
    }

    private async Task<SystemSettings> GetSettingsAsync(CancellationToken ct) =>
        (await _settingsRepo.GetAllAsync(ct)).FirstOrDefault()
        ?? throw new InvalidOperationException("System settings not initialised.");

    private static string ActiveProvider(SystemSettings s, string category) => category switch
    {
        "ai" => s.AiProvider,
        "image" => s.ImageProvider,
        "payment" => s.PaymentProvider,
        _ => "disabled",
    };

    private static void SetActiveProvider(SystemSettings s, string category, string provider)
    {
        switch (category)
        {
            case "ai": s.AiProvider = provider; break;
            case "image": s.ImageProvider = provider; break;
            case "payment": s.PaymentProvider = provider; break;
        }
        s.UpdatedAt = DateTime.UtcNow;
    }

    private static void ApplyToSettings(SystemSettings s, string category, IntegrationCredential c)
    {
        switch (category)
        {
            case "ai":
                s.AiApiKey = c.ApiKey;
                if (!string.IsNullOrWhiteSpace(c.Model)) s.AiModel = c.Model;
                s.AiBaseUrl = c.BaseUrl;
                break;
            case "image":
                s.ImageApiKey = c.ApiKey;
                if (!string.IsNullOrWhiteSpace(c.Model)) s.ImageModel = c.Model;
                s.ImageBaseUrl = c.BaseUrl;
                break;
            case "payment":
                s.PaymentApiKey = c.ApiKey;
                s.PaymentWebhookSecret = c.SecondarySecret;
                s.PaymentBaseUrl = c.BaseUrl;
                break;
        }
        s.UpdatedAt = DateTime.UtcNow;
    }

    private static void ClearActiveSecrets(SystemSettings s, string category)
    {
        switch (category)
        {
            case "ai": s.AiApiKey = null; break;
            case "image": s.ImageApiKey = null; break;
            case "payment": s.PaymentApiKey = null; s.PaymentWebhookSecret = null; break;
        }
    }

    private static IntegrationCredentialDto ToDto(IntegrationCredential c, string activeProvider) => new()
    {
        Provider = c.Provider,
        HasKey = !string.IsNullOrEmpty(c.ApiKey),
        KeyMasked = Mask(c.ApiKey),
        Model = c.Model,
        BaseUrl = c.BaseUrl,
        SecondarySecretSet = !string.IsNullOrEmpty(c.SecondarySecret),
        IsActive = string.Equals(c.Provider, activeProvider, StringComparison.OrdinalIgnoreCase),
    };

    /// <summary>
    /// Shows the first 6 and last 6 characters so the admin can identify which key is saved, e.g.
    /// "AIzaSy…gjGJ2I". Short keys are fully masked.
    /// </summary>
    internal static string? Mask(string? key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (key.Length <= 12) return "••••";
        return key[..6] + "…" + key[^6..];
    }
}
