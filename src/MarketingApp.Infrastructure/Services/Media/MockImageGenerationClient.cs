using System.Net;
using System.Text;
using MarketingApp.Application.Interfaces.Media;

namespace MarketingApp.Infrastructure.Services.Media;

/// <summary>
/// Offline image generator (Phase 3). Produces a deterministic SVG placeholder embedding the prompt
/// + dimensions, returned as a base64 data-URI so it renders directly in the browser with NO API key
/// and NO external storage. This is the default provider so the Banner Studio works end-to-end in
/// dev / demo and before real image-gen keys are configured.
/// </summary>
public class MockImageGenerationClient : IImageGenerationClient
{
    public string Provider => "mock";

    public Task<ImageGenerationResult> GenerateAsync(ImageGenerationRequest request, CancellationToken ct)
    {
        var svg = BuildPlaceholderSvg(request.Prompt, request.Width, request.Height,
            request.BrandPrimary, request.BrandSecondary, request.BrandName);
        var dataUri = "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
        return Task.FromResult(new ImageGenerationResult(
            IsSuccess: true,
            ImageUrl: dataUri,
            Width: request.Width,
            Height: request.Height,
            Provider: Provider,
            Latency: TimeSpan.Zero,
            Error: null));
    }

    /// <summary>
    /// Builds a self-contained SVG placeholder. Pure + deterministic (no time/random) so it is
    /// unit-testable. The prompt is XML-escaped and wrapped across lines to fit the canvas.
    /// </summary>
    internal static string BuildPlaceholderSvg(string prompt, int width, int height,
        string? brandPrimary = null, string? brandSecondary = null, string? brandName = null)
    {
        // Brand colors when provided, else the default indigo→purple gradient.
        var c1 = SafeColor(brandPrimary, "#6366f1");
        var c2 = SafeColor(brandSecondary, brandPrimary is null ? "#a855f7" : c1);
        var heading = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(brandName) ? "AI Banner" : brandName!.Trim());

        var safePrompt = WebUtility.HtmlEncode(prompt?.Trim() ?? string.Empty);
        var lines = WrapText(safePrompt, Math.Max(12, width / 14));
        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\">");
        sb.Append("<defs><linearGradient id=\"g\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"1\">");
        sb.Append($"<stop offset=\"0\" stop-color=\"{c1}\"/><stop offset=\"1\" stop-color=\"{c2}\"/></linearGradient></defs>");
        sb.Append($"<rect width=\"{width}\" height=\"{height}\" fill=\"url(#g)\"/>");
        var startY = height / 2 - (lines.Count * 18);
        sb.Append($"<text x=\"50%\" y=\"{startY}\" fill=\"#ffffff\" font-family=\"Segoe UI, Arial, sans-serif\" font-size=\"30\" font-weight=\"700\" text-anchor=\"middle\">{heading}</text>");
        var y = startY + 44;
        foreach (var line in lines)
        {
            sb.Append($"<text x=\"50%\" y=\"{y}\" fill=\"#f1f5f9\" font-family=\"Segoe UI, Arial, sans-serif\" font-size=\"22\" text-anchor=\"middle\">{line}</text>");
            y += 30;
        }
        sb.Append($"<text x=\"50%\" y=\"{height - 24}\" fill=\"#e0e7ff\" font-family=\"monospace\" font-size=\"16\" text-anchor=\"middle\">{width}x{height} · preview</text>");
        sb.Append("</svg>");
        return sb.ToString();
    }

    /// <summary>Returns the color only if it's a valid #RGB/#RRGGBB hex (prevents SVG injection), else the fallback.</summary>
    internal static string SafeColor(string? value, string fallback) =>
        !string.IsNullOrWhiteSpace(value) &&
        System.Text.RegularExpressions.Regex.IsMatch(value.Trim(), "^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")
            ? value.Trim() : fallback;

    private static List<string> WrapText(string text, int maxCharsPerLine)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) { result.Add("(no prompt)"); return result; }
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var current = new StringBuilder();
        foreach (var w in words)
        {
            if (current.Length > 0 && current.Length + 1 + w.Length > maxCharsPerLine)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            if (current.Length > 0) current.Append(' ');
            current.Append(w);
            if (result.Count >= 6) break;   // cap lines so the SVG stays bounded
        }
        if (current.Length > 0 && result.Count < 7) result.Add(current.ToString());
        return result;
    }
}
