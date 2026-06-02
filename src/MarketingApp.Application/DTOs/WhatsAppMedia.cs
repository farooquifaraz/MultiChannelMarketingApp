namespace MarketingApp.Application.DTOs;

/// <summary>
/// An optional media attachment for a WhatsApp message (L1). When present, the message is sent as
/// an image/document/video via the Meta Cloud API with the text used as the caption.
/// </summary>
public sealed record WhatsAppMedia
{
    /// <summary>"image" | "document" | "video". Anything else falls back to image.</summary>
    public string Type { get; init; } = "image";

    /// <summary>Publicly reachable https URL — Meta fetches the media from here.</summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>Optional caption. Falls back to the message text when not set.</summary>
    public string? Caption { get; init; }

    /// <summary>Shown to the recipient for documents (e.g. "Brochure.pdf").</summary>
    public string? FileName { get; init; }
}
