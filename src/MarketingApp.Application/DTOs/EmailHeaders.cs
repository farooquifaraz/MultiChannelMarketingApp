namespace MarketingApp.Application.DTOs;

/// <summary>
/// Headers attached to an outgoing email so we can correlate webhooks + inbox replies back to a CampaignMessage.
/// Passed through IEmailService so every provider can apply them in its own way:
///   - SMTP    -&gt; MailMessage.Headers["Message-Id"] / ["X-Campaign-Message-Id"]
///   - SendGrid-&gt; custom_args + headers array
///   - Brevo   -&gt; "headers" JSON object
///   - Mailgun -&gt; h:Message-Id / h:X-Campaign-Message-Id form fields
///
/// All fields nullable so callers can pass a partial set without compile churn.
/// </summary>
public sealed record EmailHeaders
{
    /// <summary>Full RFC 5322 Message-Id including angle brackets, e.g. "&lt;abc123@yourdomain.com&gt;".</summary>
    public string? MessageId { get; init; }

    /// <summary>CampaignMessage.Id as a string — set on X-Campaign-Message-Id custom header so webhooks can resolve it without parsing Message-Id.</summary>
    public string? CampaignMessageId { get; init; }

    /// <summary>When this email is a reply to a known inbound message, set its Message-Id to thread properly in Gmail/Outlook.</summary>
    public string? InReplyTo { get; init; }

    /// <summary>RFC 5322 References header chain (for thread depth in long conversations).</summary>
    public string? References { get; init; }

    /// <summary>Escape hatch for any other provider-specific custom headers. Keys are header names without trailing colon.</summary>
    public IReadOnlyDictionary<string, string>? Extra { get; init; }

    public static readonly EmailHeaders Empty = new();
}
