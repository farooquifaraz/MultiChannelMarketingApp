namespace MarketingApp.Domain.Entities;

/// <summary>
/// An AI-generated image / banner (Phase 3). Created via the IImageGenerationClient strategy
/// (mock provider works with zero keys; DALL·E / Stability activate when configured). The image is
/// stored as a URL — a data-URI for the mock provider, or a hosted/remote URL for real providers.
/// </summary>
public class GeneratedAsset
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>The text prompt the user supplied.</summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>Provider key that produced it: mock | dalle | stability | ideogram.</summary>
    public string Provider { get; set; } = "mock";

    /// <summary>Requested size token, e.g. "1024x1024".</summary>
    public string Size { get; set; } = "1024x1024";
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>pending | completed | failed.</summary>
    public string Status { get; set; } = "pending";

    /// <summary>Data-URI (mock) or remote/hosted URL (real providers). Null until completed.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Credits this generation cost (1 = 1 generation). Tracked now, billed later.</summary>
    public int CreditCost { get; set; } = 1;

    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
