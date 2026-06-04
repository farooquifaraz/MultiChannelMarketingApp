namespace MarketingApp.Domain.Entities;

/// <summary>
/// A saved API credential for one provider in one category (P3.5). This is a "vault" so every
/// provider's key is remembered independently — switching the active provider never loses another
/// provider's key. The *active* provider per category still lives in SystemSettings (AiProvider /
/// ImageProvider / PaymentProvider); selecting active copies the chosen credential into those fields,
/// so all existing code paths keep working unchanged.
/// </summary>
public class IntegrationCredential
{
    public Guid Id { get; set; }

    /// <summary>Category: ai | image | payment.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Provider key within the category, e.g. "openai", "gemini", "stripe".</summary>
    public string Provider { get; set; } = string.Empty;

    public string? ApiKey { get; set; }
    public string? Model { get; set; }
    public string? BaseUrl { get; set; }

    /// <summary>Optional second secret (e.g. Stripe webhook signing secret).</summary>
    public string? SecondarySecret { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
