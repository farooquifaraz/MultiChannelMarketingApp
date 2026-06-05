using MarketingApp.Application.DTOs;

namespace MarketingApp.Application.Interfaces;

/// <summary>
/// AI marketing-copy generator (P3.6) — turns a raw property/product brief into channel-ready copy
/// (WhatsApp broadcast + status, Instagram caption, email) using the active AI text provider. Same
/// structured-prompt logic as the standalone GenAI hub. Works on free AI text tiers (e.g. Gemini text).
/// </summary>
public interface IMarketingContentService
{
    Task<MarketingContentDto> GenerateAsync(GenerateContentDto dto, CancellationToken ct = default);
}
