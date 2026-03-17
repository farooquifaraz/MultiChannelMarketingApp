using System.Text;
using System.Text.Json;
using MarketingApp.Application.Interfaces;
using MarketingApp.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Infrastructure.Services;

public class SmsGatewayService : ISmsService
{
    private readonly ILogger<SmsGatewayService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public SmsGatewayService(ILogger<SmsGatewayService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<bool> SendAsync(string phoneNumber, string message, CancellationToken ct = default)
    {
        // Mock mode fallback
        _logger.LogInformation("[MOCK SMS] To: {Phone} | Message length: {Length}", phoneNumber, message.Length);
        await Task.Delay(50, ct);
        return true;
    }

    public async Task<bool> SendWithUserSettingsAsync(string phoneNumber, string message, UserSmtpSettings settings, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(settings.SmsApiKey) || string.IsNullOrEmpty(settings.SmsApiSecret))
        {
            _logger.LogWarning("SMS API credentials not configured, using mock. To: {Phone}", phoneNumber);
            return await SendAsync(phoneNumber, message, ct);
        }

        try
        {
            // Twilio-compatible SMS API
            var client = _httpClientFactory.CreateClient();
            var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{settings.SmsApiKey}:{settings.SmsApiSecret}"));
            client.DefaultRequestHeaders.Add("Authorization", $"Basic {authValue}");

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("From", settings.SmsSenderNumber ?? ""),
                new KeyValuePair<string, string>("To", phoneNumber),
                new KeyValuePair<string, string>("Body", message)
            });

            var response = await client.PostAsync(
                $"https://api.twilio.com/2010-04-01/Accounts/{settings.SmsApiKey}/Messages.json",
                formContent,
                ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[TWILIO SMS] Sent to: {Phone}", phoneNumber);
                return true;
            }

            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("[TWILIO SMS] Failed to send to: {Phone} | Status: {Status} | Error: {Error}",
                phoneNumber, response.StatusCode, errorBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SMS] Exception sending to: {Phone}", phoneNumber);
            return false;
        }
    }
}
