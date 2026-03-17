using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using MarketingApp.Application.Interfaces;
using MarketingApp.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Infrastructure.Services;

public class SmtpEmailService : IEmailService
{
    private readonly ILogger<SmtpEmailService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public SmtpEmailService(ILogger<SmtpEmailService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<bool> SendAsync(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        _logger.LogInformation("[MOCK EMAIL] To: {Email} | Subject: {Subject} | Body length: {Length}",
            toEmail, subject, body.Length);
        await Task.Delay(100, ct);
        return true;
    }

    public async Task<bool> SendWithUserSettingsAsync(string toEmail, string subject, string body, UserSmtpSettings settings, CancellationToken ct = default)
    {
        var provider = (settings.EmailProvider ?? "smtp").ToLower();

        return provider switch
        {
            "sendgrid" => await SendViaSendGridAsync(toEmail, subject, body, settings, ct),
            "brevo" => await SendViaBrevoAsync(toEmail, subject, body, settings, ct),
            "mailgun" => await SendViaMailgunAsync(toEmail, subject, body, settings, ct),
            _ => await SendViaSmtpAsync(toEmail, subject, body, settings, ct)
        };
    }

    // ==================== SMTP (Gmail, Hostinger, any SMTP server) ====================
    private async Task<bool> SendViaSmtpAsync(string toEmail, string subject, string body, UserSmtpSettings settings, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(settings.SmtpHost) || string.IsNullOrEmpty(settings.SmtpUsername))
        {
            _logger.LogError("[SMTP] Settings incomplete! Host: '{Host}', Username: '{Username}'", settings.SmtpHost, settings.SmtpUsername);
            throw new InvalidOperationException($"SMTP settings incomplete. Host: '{settings.SmtpHost}', Username: '{settings.SmtpUsername}'. Please configure your SMTP settings properly.");
        }

        _logger.LogInformation("[SMTP] Attempting to send email via {Host}:{Port} | From: {From} | To: {To} | SSL: {SSL}",
            settings.SmtpHost, settings.SmtpPort, settings.SmtpFromEmail ?? settings.SmtpUsername, toEmail, settings.SmtpEnableSsl);

        try
        {
            using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
            {
                EnableSsl = settings.SmtpEnableSsl,
                UseDefaultCredentials = false,
                Timeout = settings.SmtpTimeout > 0 ? settings.SmtpTimeout : 30000,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            // Always set credentials explicitly for auth
            client.Credentials = new NetworkCredential(settings.SmtpUsername, settings.SmtpPassword);

            var fromEmail = settings.SmtpFromEmail ?? settings.SmtpUsername!;
            var fromName = settings.SmtpFromName ?? "MarketPro";
            var fromAddress = new MailAddress(fromEmail, fromName);

            using var message = new MailMessage(fromAddress, new MailAddress(toEmail))
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            // Add Reply-To header
            message.ReplyToList.Add(new MailAddress(fromEmail, fromName));

            await client.SendMailAsync(message, ct);
            _logger.LogInformation("[SMTP] ✅ Successfully sent to: {Email} | From: {From} via {Host}", toEmail, fromEmail, settings.SmtpHost);
            return true;
        }
        catch (SmtpException ex)
        {
            _logger.LogError(ex, "[SMTP] ❌ SMTP Error sending to: {Email} | StatusCode: {StatusCode} | Error: {Error}",
                toEmail, ex.StatusCode, ex.Message);
            throw new InvalidOperationException($"SMTP Error: {ex.Message}. Status: {ex.StatusCode}. Check your SMTP credentials and server settings.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SMTP] ❌ Failed to send to: {Email} | Error: {Error}", toEmail, ex.Message);
            throw new InvalidOperationException($"Email send failed: {ex.Message}. Verify your SMTP Host, Port, Username and Password.", ex);
        }
    }

    // ==================== SendGrid API ====================
    private async Task<bool> SendViaSendGridAsync(string toEmail, string subject, string body, UserSmtpSettings settings, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(settings.SendGridApiKey))
        {
            _logger.LogWarning("SendGrid API key not set, falling back to mock. To: {Email}", toEmail);
            return await SendAsync(toEmail, subject, body, ct);
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {settings.SendGridApiKey}");

            var payload = new
            {
                personalizations = new[] { new { to = new[] { new { email = toEmail } } } },
                from = new { email = settings.SmtpFromEmail ?? settings.SmtpUsername, name = settings.SmtpFromName ?? "MarketPro" },
                subject,
                content = new[] { new { type = "text/html", value = body } }
            };

            var response = await client.PostAsync(
                "https://api.sendgrid.com/v3/mail/send",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
                ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[SendGrid] Sent to: {Email}", toEmail);
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("[SendGrid] Failed: {Status} - {Body}", response.StatusCode, responseBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SendGrid] Failed to send to: {Email}", toEmail);
            return false;
        }
    }

    // ==================== Brevo (Sendinblue) API ====================
    private async Task<bool> SendViaBrevoAsync(string toEmail, string subject, string body, UserSmtpSettings settings, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(settings.BrevoApiKey))
        {
            _logger.LogWarning("Brevo API key not set, falling back to mock. To: {Email}", toEmail);
            return await SendAsync(toEmail, subject, body, ct);
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("api-key", settings.BrevoApiKey);

            var payload = new
            {
                sender = new { email = settings.SmtpFromEmail ?? settings.SmtpUsername, name = settings.SmtpFromName ?? "MarketPro" },
                to = new[] { new { email = toEmail } },
                subject,
                htmlContent = body
            };

            var response = await client.PostAsync(
                "https://api.brevo.com/v3/smtp/email",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
                ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[Brevo] Sent to: {Email}", toEmail);
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("[Brevo] Failed: {Status} - {Body}", response.StatusCode, responseBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Brevo] Failed to send to: {Email}", toEmail);
            return false;
        }
    }

    // ==================== Mailgun API ====================
    private async Task<bool> SendViaMailgunAsync(string toEmail, string subject, string body, UserSmtpSettings settings, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(settings.MailgunApiKey) || string.IsNullOrEmpty(settings.MailgunDomain))
        {
            _logger.LogWarning("Mailgun settings incomplete, falling back to mock. To: {Email}", toEmail);
            return await SendAsync(toEmail, subject, body, ct);
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"api:{settings.MailgunApiKey}"));
            client.DefaultRequestHeaders.Add("Authorization", $"Basic {authValue}");

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("from", $"{settings.SmtpFromName ?? "MarketPro"} <{settings.SmtpFromEmail ?? settings.SmtpUsername}>"),
                new KeyValuePair<string, string>("to", toEmail),
                new KeyValuePair<string, string>("subject", subject),
                new KeyValuePair<string, string>("html", body)
            });

            var response = await client.PostAsync(
                $"https://api.mailgun.net/v3/{settings.MailgunDomain}/messages",
                formContent,
                ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[Mailgun] Sent to: {Email}", toEmail);
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("[Mailgun] Failed: {Status} - {Body}", response.StatusCode, responseBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Mailgun] Failed to send to: {Email}", toEmail);
            return false;
        }
    }
}
