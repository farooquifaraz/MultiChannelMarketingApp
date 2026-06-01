using FluentAssertions;
using MarketingApp.Application.Jobs;

namespace MarketingApp.Tests.Services;

/// <summary>
/// Bounce classification — guards against false positives that would permanently
/// flag valid contacts as bounced. Born from the 2026-06-01 prod incident: Zoho
/// throttled 48 deliveries with a "Mailbox unavailable / 5.4.6 Unusual sending
/// activity detected" message, and the old classifier marked all 48 contacts
/// IsBounced=true. They were valid mailboxes; just rate-limited.
/// </summary>
public class BounceClassificationTests
{
    // ---- SOFT bounces (rate-limit / throttle / transient) must NOT be hard ----

    [Fact(DisplayName = "Zoho rate-limit (5.4.6 + Unusual sending activity) is SOFT")]
    public void ZohoRateLimit_IsSoft()
    {
        var ex = new Exception(
            "SMTP Error: Mailbox unavailable. The server response was: " +
            "5.4.6 Unusual sending activity detected. Please try after sometime. " +
            "Status: MailboxUnavailable. Check your SMTP credentials and server settings.");

        var (isHard, _) = CampaignJobService.DetectHardBounce(ex);

        isHard.Should().BeFalse(
            "Zoho throttle returns 5.4.6 + 'Unusual sending activity' — a transient throttle, " +
            "NOT a permanent failure. Contacts here are valid; they must be retried, not flagged.");
    }

    [Fact(DisplayName = "Gmail '421 4.7.0 Try again later' is SOFT")]
    public void GmailRateLimit_IsSoft()
    {
        var ex = new Exception(
            "421-4.7.0 Try again later, closing connection. (EHLO mta.example.com)");
        CampaignJobService.DetectHardBounce(ex).IsHardBounce.Should().BeFalse();
    }

    [Fact(DisplayName = "Mailbox full (5.2.2 / over quota) is SOFT")]
    public void MailboxFull_IsSoft()
    {
        var ex = new Exception("552 5.2.2 The user's mailbox is full / over quota");
        CampaignJobService.DetectHardBounce(ex).IsHardBounce.Should().BeFalse();
    }

    [Fact(DisplayName = "Greylisting (4.x deferred) is SOFT")]
    public void Greylisting_IsSoft()
    {
        var ex = new Exception("450 4.2.1 Greylisted, please try again in 5 minutes");
        CampaignJobService.DetectHardBounce(ex).IsHardBounce.Should().BeFalse();
    }

    [Fact(DisplayName = "Generic connection timeout is SOFT")]
    public void ConnectionTimeout_IsSoft()
    {
        var ex = new Exception("Network error: connection timed out after 30 seconds");
        CampaignJobService.DetectHardBounce(ex).IsHardBounce.Should().BeFalse();
    }

    [Fact(DisplayName = "Provider-issued 'quota exceeded' is SOFT")]
    public void QuotaExceeded_IsSoft()
    {
        var ex = new Exception("550 5.7.1 Quota exceeded for this sender today, try tomorrow.");
        // Even though 5.7.x is normally permanent (security/policy), the explicit
        // "quota exceeded" keyword wins — it's a sender-side throttle, not a
        // permanent rejection of the recipient.
        CampaignJobService.DetectHardBounce(ex).IsHardBounce.Should().BeFalse();
    }

    // ---- HARD bounces (real permanent failures) MUST be flagged ----

    [Fact(DisplayName = "User unknown is HARD")]
    public void UserUnknown_IsHard()
    {
        var ex = new Exception("550 5.1.1 The email account that you tried to reach does not exist. User unknown.");
        CampaignJobService.DetectHardBounce(ex).IsHardBounce.Should().BeTrue();
    }

    [Fact(DisplayName = "No such recipient (5.1.1) is HARD")]
    public void NoSuchRecipient_IsHard()
    {
        var ex = new Exception("550 5.1.1 No such recipient");
        CampaignJobService.DetectHardBounce(ex).IsHardBounce.Should().BeTrue();
    }

    [Fact(DisplayName = "Domain does not exist is HARD")]
    public void DomainDoesNotExist_IsHard()
    {
        var ex = new Exception("Recipient domain does not exist");
        CampaignJobService.DetectHardBounce(ex).IsHardBounce.Should().BeTrue();
    }

    [Fact(DisplayName = "Security policy rejection (5.7.x) is HARD")]
    public void SecurityPolicyRejection_IsHard()
    {
        var ex = new Exception("550 5.7.1 Message rejected by content policy");
        // 5.7.x is permanent; no soft keywords here.
        CampaignJobService.DetectHardBounce(ex).IsHardBounce.Should().BeTrue();
    }

    [Fact(DisplayName = "Recipient address rejected is HARD")]
    public void RecipientAddressRejected_IsHard()
    {
        var ex = new Exception("550 Recipient address rejected: User unknown in local recipient table");
        CampaignJobService.DetectHardBounce(ex).IsHardBounce.Should().BeTrue();
    }

    // ---- Edge cases ----

    [Fact(DisplayName = "Random network blip is SOFT (returned false)")]
    public void RandomErrorIsSoft()
    {
        var ex = new Exception("Server error: please try again");
        // Falls through all hard patterns; default is soft (transient).
        CampaignJobService.DetectHardBounce(ex).IsHardBounce.Should().BeFalse();
    }

    [Fact(DisplayName = "Soft keyword inside an otherwise-hard message still wins")]
    public void SoftKeywordWinsOverHardPatterns()
    {
        var ex = new Exception(
            "550 5.1.1 User unknown — but actually rate limit hit, please try again later");
        // 'rate limit' + 'try again later' present → soft wins despite 5.1.1.
        // This protects against providers that misuse status codes.
        CampaignJobService.DetectHardBounce(ex).IsHardBounce.Should().BeFalse();
    }
}
