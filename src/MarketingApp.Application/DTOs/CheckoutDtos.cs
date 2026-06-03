namespace MarketingApp.Application.DTOs;

/// <summary>Start a checkout to upgrade to a paid plan (P2.2).</summary>
public class StartCheckoutDto
{
    public string PlanCode { get; set; } = string.Empty;
    /// <summary>Where the provider should send the user after success / cancel. Frontend supplies these.</summary>
    public string? SuccessUrl { get; set; }
    public string? CancelUrl { get; set; }
}

/// <summary>Result of starting a checkout.</summary>
public class CheckoutResultDto
{
    public string Provider { get; set; } = "mock";
    public string PlanCode { get; set; } = string.Empty;
    /// <summary>True when the plan was activated immediately (mock) — no redirect needed.</summary>
    public bool Activated { get; set; }
    /// <summary>For real providers: the hosted checkout URL to redirect to. For mock: the success URL.</summary>
    public string? CheckoutUrl { get; set; }
}
