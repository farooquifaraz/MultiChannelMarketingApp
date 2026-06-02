using MarketingApp.Application.DTOs;

namespace MarketingApp.Application.Interfaces;

/// <summary>
/// Syncs + serves WhatsApp approved templates (L2). Templates live in Meta Business Manager;
/// we cache them locally per SmtpGroup so the compose UI can list them and campaigns can send them.
/// </summary>
public interface IWhatsAppTemplateService
{
    /// <summary>
    /// Pulls the latest templates from the Meta Graph API for the group's WhatsApp Business Account
    /// and upserts them into the local cache. Returns counts. Throws if the group has no WhatsApp
    /// credentials / WABA id configured.
    /// </summary>
    Task<WhatsAppTemplateSyncResultDto> SyncFromMetaAsync(Guid smtpGroupId, CancellationToken ct = default);

    /// <summary>Lists cached templates for a group (optionally only APPROVED ones).</summary>
    Task<IEnumerable<WhatsAppTemplateDto>> ListAsync(Guid smtpGroupId, bool approvedOnly = false, CancellationToken ct = default);
}
