namespace MarketingApp.Application.Interfaces;

/// <summary>
/// Ingests inbound WhatsApp messages delivered by the Meta Cloud API webhook into the unified inbox (L3).
/// </summary>
public interface IWhatsAppInboundService
{
    /// <summary>
    /// Parses a Meta webhook payload and persists any inbound messages as InboxMessage rows
    /// (channel = "whatsapp"), routing each to the owner of the SmtpGroup that owns the receiving
    /// phone number, deduping on the Meta message id, then enqueuing AI + pushing realtime.
    /// Returns the number of new messages ingested. Never throws on a single bad message.
    /// </summary>
    Task<int> IngestAsync(string rawJson, CancellationToken ct = default);
}
