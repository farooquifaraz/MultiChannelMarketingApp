namespace MarketingApp.Application.DTOs;

/// <summary>One saved provider credential (key masked on read) (P3.5).</summary>
public class IntegrationCredentialDto
{
    public string Provider { get; set; } = string.Empty;
    public bool HasKey { get; set; }
    public string? KeyMasked { get; set; }
    public string? Model { get; set; }
    public string? BaseUrl { get; set; }
    public bool SecondarySecretSet { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>All saved credentials for a category + which provider is active.</summary>
public class IntegrationCategoryDto
{
    public string Category { get; set; } = string.Empty;
    public string ActiveProvider { get; set; } = "disabled";
    public List<IntegrationCredentialDto> Credentials { get; set; } = new();
}

/// <summary>Save/update one provider's credential (blank key = keep existing).</summary>
public class SaveCredentialDto
{
    public string? ApiKey { get; set; }
    public string? Model { get; set; }
    public string? BaseUrl { get; set; }
    public string? SecondarySecret { get; set; }
}
