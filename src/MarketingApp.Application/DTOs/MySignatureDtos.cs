namespace MarketingApp.Application.DTOs;

/// <summary>
/// The user's resolved signature — personal fields merged with org-level SmtpGroup fallback.
/// This is what gets rendered into templates at preview + send time.
/// </summary>
public record MySignatureDto
{
    // Personal overrides (set per-user via PUT /me/signature)
    public string FullName { get; init; } = string.Empty;
    public string? SignatureDesignation { get; init; }
    public string? SignaturePhone { get; init; }
    public string? SignatureImageUrl { get; init; }

    // Effective (after merging with assigned SmtpGroup)
    public string? EffectiveDesignation { get; init; }
    public string? EffectivePhone { get; init; }
    public string? EffectiveImageUrl { get; init; }

    // Org-level (read-only — from the assigned SmtpGroup, set by admin)
    public string? OrgFromEmail { get; init; }
    public string? OrgCompanyName { get; init; }
    public string? OrgCompanyWebsite { get; init; }
    public string? OrgSmtpGroupName { get; init; }
}

public record UpdateMySignatureDto
{
    public string? SignatureDesignation { get; init; }
    public string? SignaturePhone { get; init; }
    public string? SignatureImageUrl { get; init; }
}
