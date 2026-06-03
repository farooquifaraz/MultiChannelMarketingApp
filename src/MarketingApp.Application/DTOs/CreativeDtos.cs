namespace MarketingApp.Application.DTOs;

/// <summary>Request to generate an image (Phase 3).</summary>
public class GenerateImageDto
{
    public string Prompt { get; set; } = string.Empty;
    /// <summary>Size token, e.g. "1024x1024". Must be one of the allowed options.</summary>
    public string Size { get; set; } = "1024x1024";
    /// <summary>Optional brand kit to apply (P3.2).</summary>
    public Guid? BrandKitId { get; set; }
}

/// <summary>A generated image asset (read shape).</summary>
public class GeneratedAssetDto
{
    public Guid Id { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public string Provider { get; set; } = "mock";
    public string Size { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public string Status { get; set; } = "pending";
    public string? ImageUrl { get; set; }
    public int CreditCost { get; set; }
    public Guid? BrandKitId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>A selectable image size for the UI picker.</summary>
public class ImageSizeOptionDto
{
    public string Token { get; set; } = string.Empty;   // "1024x1024"
    public int Width { get; set; }
    public int Height { get; set; }
    public string Label { get; set; } = string.Empty;   // "Square (Instagram post)"
}
