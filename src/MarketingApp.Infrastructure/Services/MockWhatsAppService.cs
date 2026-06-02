using System.Text;
using System.Text.Json;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Infrastructure.Services;

public class WhatsAppCloudService : IWhatsAppService
{
    private readonly ILogger<WhatsAppCloudService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private const string META_API_URL = "https://graph.facebook.com/v18.0";

    public WhatsAppCloudService(ILogger<WhatsAppCloudService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<bool> SendAsync(string phoneNumber, string message, CancellationToken ct = default)
    {
        // Mock mode fallback
        _logger.LogInformation("[MOCK WHATSAPP] To: {Phone} | Message length: {Length}", phoneNumber, message.Length);
        await Task.Delay(100, ct);
        return true;
    }

    public async Task<bool> SendWithUserSettingsAsync(string phoneNumber, string message, UserSmtpSettings settings, WhatsAppMedia? media = null, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(settings.WhatsAppApiKey) || string.IsNullOrEmpty(settings.WhatsAppPhoneNumberId))
        {
            _logger.LogWarning("WhatsApp API credentials not configured, using mock. To: {Phone}", phoneNumber);
            if (media is not null)
                _logger.LogInformation("[MOCK WHATSAPP] Would send {Type} media: {Url}", media.Type, media.Url);
            return await SendAsync(phoneNumber, message, ct);
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {settings.WhatsAppApiKey}");

            // Format phone number: remove + and spaces
            var formattedPhone = phoneNumber.Replace("+", "").Replace(" ", "").Replace("-", "");

            var payload = BuildSendPayload(formattedPhone, message, media);

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync(
                $"{META_API_URL}/{settings.WhatsAppPhoneNumberId}/messages",
                jsonContent,
                ct);

            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogInformation("[META WHATSAPP] Sent {Type} to: {Phone} | Response: {Response}",
                    media?.Type ?? "text", phoneNumber, responseBody);
                return true;
            }

            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("[META WHATSAPP] Failed to send to: {Phone} | Status: {Status} | Error: {Error}",
                phoneNumber, response.StatusCode, errorBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[META WHATSAPP] Exception sending to: {Phone} | Error: {Error}",
                phoneNumber, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Builds the Meta Cloud API request body. Text-only when no media; otherwise an
    /// image/document/video object carrying the public link + caption (and filename for docs).
    /// Pure + deterministic so it can be unit-tested without hitting the network.
    /// </summary>
    internal static Dictionary<string, object?> BuildSendPayload(string to, string message, WhatsAppMedia? media)
    {
        var payload = new Dictionary<string, object?>
        {
            ["messaging_product"] = "whatsapp",
            ["to"] = to,
        };

        if (media is null || string.IsNullOrWhiteSpace(media.Url))
        {
            payload["type"] = "text";
            payload["text"] = new Dictionary<string, object?> { ["body"] = message };
            return payload;
        }

        var type = (media.Type ?? "").ToLowerInvariant() switch
        {
            "video" => "video",
            "document" => "document",
            _ => "image",
        };

        // Caption: explicit caption wins, else the message text, else omitted (Meta rejects empty captions).
        var caption = !string.IsNullOrWhiteSpace(media.Caption) ? media.Caption
                    : !string.IsNullOrWhiteSpace(message) ? message
                    : null;

        var mediaObj = new Dictionary<string, object?> { ["link"] = media.Url };
        if (caption is not null) mediaObj["caption"] = caption;
        if (type == "document")
            mediaObj["filename"] = string.IsNullOrWhiteSpace(media.FileName) ? "document" : media.FileName;

        payload["type"] = type;
        payload[type] = mediaObj;
        return payload;
    }
}
