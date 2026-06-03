namespace MarketingApp.Application.DTOs;

/// <summary>A pricing tier shown on the pricing page (Phase 2). Limit -1 = unlimited.</summary>
public class PlanDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal PriceAedMonthly { get; set; }
    public int MaxContacts { get; set; }
    public int MaxEmailsPerMonth { get; set; }
    public int MaxWhatsAppPerMonth { get; set; }
    public int MaxAiPerMonth { get; set; }
    public int MaxUsers { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>One usage metric: how much used vs the plan limit this period.</summary>
public class UsageMetricDto
{
    public int Used { get; set; }
    public int Limit { get; set; }            // -1 = unlimited
    public bool Unlimited => Limit < 0;
    public int Remaining => Unlimited ? int.MaxValue : Math.Max(0, Limit - Used);
    public double Percent => Unlimited || Limit == 0 ? 0 : Math.Round(Math.Min(100.0, Used * 100.0 / Limit), 1);
    public bool OverLimit => !Unlimited && Used > Limit;
}

/// <summary>The user's current plan + live usage for this billing period (Phase 2).</summary>
public class SubscriptionDto
{
    public string PlanCode { get; set; } = "free";
    public string PlanName { get; set; } = "Free";
    public decimal PriceAedMonthly { get; set; }
    public string Status { get; set; } = "active";
    public DateTime CurrentPeriodStart { get; set; }
    public DateTime CurrentPeriodEnd { get; set; }
    public bool QuotasEnforced { get; set; }

    public UsageMetricDto Contacts { get; set; } = new();
    public UsageMetricDto Emails { get; set; } = new();
    public UsageMetricDto WhatsApp { get; set; } = new();
    public UsageMetricDto Ai { get; set; } = new();
}
