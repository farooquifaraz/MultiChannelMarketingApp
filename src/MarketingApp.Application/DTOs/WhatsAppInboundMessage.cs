namespace MarketingApp.Application.DTOs;

/// <summary>A single inbound WhatsApp message parsed out of a Meta Cloud API webhook (L3).</summary>
public sealed record WhatsAppInboundMessage
{
    /// <summary>The business phone-number id the message was sent TO (maps to our SmtpGroup).</summary>
    public string PhoneNumberId { get; init; } = string.Empty;

    /// <summary>Sender's WhatsApp number (digits, e.g. "971501234567").</summary>
    public string From { get; init; } = string.Empty;

    /// <summary>Sender's WhatsApp profile name, if Meta included it.</summary>
    public string? FromName { get; init; }

    /// <summary>Meta's globally-unique message id (wamid.…) — used for dedup.</summary>
    public string MessageId { get; init; } = string.Empty;

    /// <summary>Text body. For non-text messages (image/doc) this is a short placeholder.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Message type from Meta: text | image | document | audio | video | …</summary>
    public string Type { get; init; } = "text";

    /// <summary>When the sender sent it (UTC).</summary>
    public DateTime ReceivedAt { get; init; }
}
