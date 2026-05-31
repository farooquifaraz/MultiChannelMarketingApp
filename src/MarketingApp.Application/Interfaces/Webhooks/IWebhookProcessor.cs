using MarketingApp.Application.DTOs;

namespace MarketingApp.Application.Interfaces.Webhooks;

/// <summary>
/// Applies a normalized list of webhook events to the database:
///   - dedup via WebhookEventLog (Provider, ProviderEventId)
///   - update CampaignMessage delivery/bounce/open/click state
///   - auto-flag contacts on hard bounce (reuses Day 6 F2 mechanism)
///   - emit audit log entries
/// </summary>
public interface IWebhookProcessor
{
    /// <summary>Returns the number of events actually applied (excluding duplicates).</summary>
    Task<int> ProcessAsync(IReadOnlyList<NormalizedWebhookEvent> events, CancellationToken ct);
}
