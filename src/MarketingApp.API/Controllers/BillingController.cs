using System.Security.Claims;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketingApp.API.Controllers;

/// <summary>
/// Plans + the current user's subscription/usage (Phase 2 / P2.1). Read-only view of pricing +
/// live usage; plan change here is the manual/admin path (real card payment lands with Stripe).
/// </summary>
[ApiController]
[Route("api/v1/billing")]
[Authorize]
public class BillingController : ControllerBase
{
    private readonly IBillingService _billing;
    private readonly ICheckoutService _checkout;
    public BillingController(IBillingService billing, ICheckoutService checkout)
    {
        _billing = billing;
        _checkout = checkout;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public record ChangePlanDto(string PlanCode);

    /// <summary>Start a checkout to upgrade to a paid plan. Mock provider activates immediately;
    /// real providers return a hosted checkout URL to redirect to.</summary>
    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout([FromBody] StartCheckoutDto dto, CancellationToken ct)
    {
        var result = await _checkout.StartCheckoutAsync(GetUserId(), dto, ct);
        return Ok(ApiResponse<CheckoutResultDto>.Ok(result));
    }

    /// <summary>All active pricing tiers.</summary>
    [HttpGet("plans")]
    public async Task<IActionResult> Plans(CancellationToken ct)
    {
        var plans = await _billing.ListPlansAsync(ct);
        return Ok(ApiResponse<IEnumerable<PlanDto>>.Ok(plans));
    }

    /// <summary>The current user's subscription + live usage (auto-provisions Free).</summary>
    [HttpGet("subscription")]
    public async Task<IActionResult> Subscription(CancellationToken ct)
    {
        var sub = await _billing.GetMySubscriptionAsync(GetUserId(), ct);
        return Ok(ApiResponse<SubscriptionDto>.Ok(sub));
    }

    /// <summary>Switch plan (manual/admin path — no card charge yet).</summary>
    [HttpPost("subscription/change")]
    public async Task<IActionResult> ChangePlan([FromBody] ChangePlanDto dto, CancellationToken ct)
    {
        var sub = await _billing.ChangePlanAsync(GetUserId(), dto.PlanCode, ct);
        return Ok(ApiResponse<SubscriptionDto>.Ok(sub, $"Plan changed to {sub.PlanName}."));
    }
}
