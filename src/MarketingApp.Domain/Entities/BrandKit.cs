namespace MarketingApp.Domain.Entities;

/// <summary>
/// A reusable brand identity (Phase 3 / P3.2) — logo + colors + font — that can be applied to AI
/// banner generation so output matches the user's brand. Scoped to a user (Agency Model); one kit
/// per user can be the default. Applied additively: generation works fine with no kit selected.
/// </summary>
public class BrandKit
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Optional logo URL (data-URI or hosted).</summary>
    public string? LogoUrl { get; set; }

    /// <summary>Hex colors (e.g. "#4f46e5"). Primary required; others optional.</summary>
    public string PrimaryColor { get; set; } = "#4f46e5";
    public string? SecondaryColor { get; set; }
    public string? AccentColor { get; set; }

    /// <summary>Font family name applied in generated output (e.g. "Inter", "Georgia").</summary>
    public string FontFamily { get; set; } = "Inter";

    /// <summary>Exactly one kit per user may be the default (enforced in the service).</summary>
    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
