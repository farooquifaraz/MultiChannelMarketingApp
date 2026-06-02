using System.Text.Json;
using FluentAssertions;
using MarketingApp.Application.DTOs;
using MarketingApp.Infrastructure.Services;

namespace MarketingApp.Tests.Services;

/// <summary>
/// L1 — WhatsApp media payload shaping. Guards that each media type produces the exact Meta Cloud API
/// body structure (type discriminator + media object with link/caption/filename), and that the
/// text-only path is unchanged. Pure builder, no network.
/// </summary>
public class WhatsAppPayloadTests
{
    private static JsonElement Json(Dictionary<string, object?> payload)
    {
        var s = JsonSerializer.Serialize(payload);
        return JsonDocument.Parse(s).RootElement;
    }

    [Fact(DisplayName = "No media → text payload (unchanged behavior)")]
    public void TextOnly()
    {
        var p = Json(WhatsAppCloudService.BuildSendPayload("971500000000", "Hello there", null));
        p.GetProperty("messaging_product").GetString().Should().Be("whatsapp");
        p.GetProperty("to").GetString().Should().Be("971500000000");
        p.GetProperty("type").GetString().Should().Be("text");
        p.GetProperty("text").GetProperty("body").GetString().Should().Be("Hello there");
    }

    [Fact(DisplayName = "Image media → image object with link + caption (from message)")]
    public void Image()
    {
        var media = new WhatsAppMedia { Type = "image", Url = "https://x.test/p.jpg" };
        var p = Json(WhatsAppCloudService.BuildSendPayload("971500000000", "Property photo", media));
        p.GetProperty("type").GetString().Should().Be("image");
        var img = p.GetProperty("image");
        img.GetProperty("link").GetString().Should().Be("https://x.test/p.jpg");
        img.GetProperty("caption").GetString().Should().Be("Property photo");
        p.TryGetProperty("text", out _).Should().BeFalse();
    }

    [Fact(DisplayName = "Explicit caption wins over message text")]
    public void ExplicitCaption()
    {
        var media = new WhatsAppMedia { Type = "image", Url = "https://x.test/p.jpg", Caption = "Custom caption" };
        var p = Json(WhatsAppCloudService.BuildSendPayload("971500000000", "ignored body", media));
        p.GetProperty("image").GetProperty("caption").GetString().Should().Be("Custom caption");
    }

    [Fact(DisplayName = "Document media → document object with filename + caption")]
    public void Document()
    {
        var media = new WhatsAppMedia { Type = "document", Url = "https://x.test/b.pdf", FileName = "Brochure.pdf" };
        var p = Json(WhatsAppCloudService.BuildSendPayload("971500000000", "Our brochure", media));
        p.GetProperty("type").GetString().Should().Be("document");
        var doc = p.GetProperty("document");
        doc.GetProperty("link").GetString().Should().Be("https://x.test/b.pdf");
        doc.GetProperty("caption").GetString().Should().Be("Our brochure");
        doc.GetProperty("filename").GetString().Should().Be("Brochure.pdf");
    }

    [Fact(DisplayName = "Document without filename defaults to 'document'")]
    public void DocumentDefaultFilename()
    {
        var media = new WhatsAppMedia { Type = "document", Url = "https://x.test/b.pdf" };
        var p = Json(WhatsAppCloudService.BuildSendPayload("971500000000", "x", media));
        p.GetProperty("document").GetProperty("filename").GetString().Should().Be("document");
    }

    [Fact(DisplayName = "Video media → video object with link + caption")]
    public void Video()
    {
        var media = new WhatsAppMedia { Type = "video", Url = "https://x.test/v.mp4" };
        var p = Json(WhatsAppCloudService.BuildSendPayload("971500000000", "Walkthrough", media));
        p.GetProperty("type").GetString().Should().Be("video");
        p.GetProperty("video").GetProperty("link").GetString().Should().Be("https://x.test/v.mp4");
    }

    [Fact(DisplayName = "Unknown media type falls back to image")]
    public void UnknownTypeFallsBackToImage()
    {
        var media = new WhatsAppMedia { Type = "sticker", Url = "https://x.test/s.png" };
        var p = Json(WhatsAppCloudService.BuildSendPayload("971500000000", "x", media));
        p.GetProperty("type").GetString().Should().Be("image");
    }

    [Fact(DisplayName = "Empty media URL → treated as text, not media")]
    public void EmptyUrlIsText()
    {
        var media = new WhatsAppMedia { Type = "image", Url = "" };
        var p = Json(WhatsAppCloudService.BuildSendPayload("971500000000", "Hello", media));
        p.GetProperty("type").GetString().Should().Be("text");
    }

    [Fact(DisplayName = "No caption + empty message → caption omitted (Meta rejects empty)")]
    public void NoCaptionOmitted()
    {
        var media = new WhatsAppMedia { Type = "image", Url = "https://x.test/p.jpg" };
        var p = Json(WhatsAppCloudService.BuildSendPayload("971500000000", "", media));
        p.GetProperty("image").TryGetProperty("caption", out _).Should().BeFalse();
    }
}
