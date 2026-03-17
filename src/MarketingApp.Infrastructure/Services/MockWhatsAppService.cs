using System.Text;
using System.Text.Json;
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

    public async Task<bool> SendWithUserSettingsAsync(string phoneNumber, string message, UserSmtpSettings settings, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(settings.WhatsAppApiKey) || string.IsNullOrEmpty(settings.WhatsAppPhoneNumberId))
        {
            _logger.LogWarning("WhatsApp API credentials not configured, using mock. To: {Phone}", phoneNumber);
            return await SendAsync(phoneNumber, message, ct);
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {settings.WhatsAppApiKey}");

            // Format phone number: remove + and spaces
            var formattedPhone = phoneNumber.Replace("+", "").Replace(" ", "").Replace("-", "");

            var payload = new
            {
                messaging_product = "whatsapp",
                to = formattedPhone,
                type = "text",
                text = new { body = message }
            };

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
                _logger.LogInformation("[META WHATSAPP] Sent to: {Phone} | Response: {Response}",
                    phoneNumber, responseBody);
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
}
