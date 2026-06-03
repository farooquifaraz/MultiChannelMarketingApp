namespace MarketingApp.Application.DTOs;

/// <summary>A brand identity kit (P3.2).</summary>
public class BrandKitDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string PrimaryColor { get; set; } = "#4f46e5";
    public string? SecondaryColor { get; set; }
    public string? AccentColor { get; set; }
    public string FontFamily { get; set; } = "Inter";
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateBrandKitDto
{
    public string Name { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string PrimaryColor { get; set; } = "#4f46e5";
    public string? SecondaryColor { get; set; }
    public string? AccentColor { get; set; }
    public string FontFamily { get; set; } = "Inter";
    public bool IsDefault { get; set; }
}

public class UpdateBrandKitDto
{
    public string Name { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string PrimaryColor { get; set; } = "#4f46e5";
    public string? SecondaryColor { get; set; }
    public string? AccentColor { get; set; }
    public string FontFamily { get; set; } = "Inter";
    public bool IsDefault { get; set; }
}
