using MarketingApp.Application.Interfaces;
using MarketingApp.Domain.Constants;
using MarketingApp.Domain.Entities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace MarketingApp.Application.Jobs;

public class CampaignJobService : ICampaignJobService
{
    private readonly ICampaignRepository _campaignRepo;
    private readonly IEmailService _emailService;
    private readonly IWhatsAppService _whatsAppService;
    private readonly ISmsService _smsService;
    private readonly INotificationService _notificationService;
    private readonly IGenericRepository<UserSmtpSettings> _smtpSettingsRepo;
    private readonly ILogger<CampaignJobService> _logger;

    public CampaignJobService(
        ICampaignRepository campaignRepo,
        IEmailService emailService,
        IWhatsAppService whatsAppService,
        ISmsService smsService,
        INotificationService notificationService,
        IGenericRepository<UserSmtpSettings> smtpSettingsRepo,
        ILogger<CampaignJobService> logger)
    {
        _campaignRepo = campaignRepo;
        _emailService = emailService;
        _whatsAppService = whatsAppService;
        _smsService = smsService;
        _notificationService = notificationService;
        _smtpSettingsRepo = smtpSettingsRepo;
        _logger = logger;
    }

    public async Task ProcessCampaignAsync(Guid campaignId)
    {
        _logger.LogInformation("Starting campaign processing: {CampaignId}", campaignId);

        var campaign = await _campaignRepo.GetWithTemplateAsync(campaignId, default);
        if (campaign is null)
        {
            _logger.LogError("Campaign {CampaignId} not found", campaignId);
            return;
        }

        // Load user's SMTP settings
        UserSmtpSettings? userSmtpSettings = null;
        try
        {
            var settings = await _smtpSettingsRepo.FindAsync(s => s.UserId == campaign.UserId);
            userSmtpSettings = settings.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load SMTP settings for user {UserId}, using mock", campaign.UserId);
        }

        campaign.Status = "running";
        campaign.StartedAt = DateTime.UtcNow;
        await _campaignRepo.UpdateAsync(campaign);

        // Notify user that campaign started
        await _notificationService.CreateNotificationAsync(
            campaign.UserId,
            $"Campaign \"{campaign.Name}\" Started",
            $"Your {campaign.Channel.ToUpper()} campaign is now sending to {campaign.TotalContacts} contacts.",
            "info",
            "campaign",
            campaign.Id);

        var sentCount = 0;
        var failedCount = 0;

        try
        {
            var pendingMessages = await _campaignRepo.GetPendingMessagesAsync(campaignId, default);
            var batches = pendingMessages
                .Select((msg, idx) => new { msg, idx })
                .GroupBy(x => x.idx / AppConstants.BatchSize)
                .Select(g => g.Select(x => x.msg).ToList())
                .ToList();

            _logger.LogInformation("Processing {BatchCount} batches for campaign {CampaignId}", batches.Count, campaignId);

            var batchNumber = 0;
            foreach (var batch in batches)
            {
                batchNumber++;
                _logger.LogInformation("Processing batch {BatchNum}/{TotalBatches} for campaign {CampaignId}",
                    batchNumber, batches.Count, campaignId);

                foreach (var message in batch)
                {
                    var success = await ProcessSingleMessageAsync(message, campaign, userSmtpSettings);
                    if (success) sentCount++;
                    else failedCount++;

                    await _campaignRepo.UpdateMessageAsync(message, default);
                }

                if (batchNumber < batches.Count)
                    await Task.Delay(AppConstants.DelayBetweenBatchesMs);
            }

            campaign.Status = "completed";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Campaign {CampaignId} processing failed", campaignId);
            campaign.Status = "failed";
        }

        campaign.CompletedAt = DateTime.UtcNow;
        campaign.SentCount = sentCount;
        campaign.FailedCount = failedCount;
        await _campaignRepo.UpdateAsync(campaign);

        // Send completion notification
        await _notificationService.SendCampaignCompletionNotificationAsync(
            campaign.UserId,
            campaign.Name,
            sentCount,
            failedCount,
            campaign.Channel);

        // If user has notification email configured, send email summary
        if (userSmtpSettings?.NotifyOnCampaignComplete == true && !string.IsNullOrEmpty(userSmtpSettings.NotificationEmail))
        {
            try
            {
                var totalCount = sentCount + failedCount;
                var successRate = totalCount > 0 ? (sentCount * 100.0 / totalCount).ToString("F1") : "0";
                var emailBody = $@"
                    <h2>Campaign Completed: {campaign.Name}</h2>
                    <table style='border-collapse:collapse;width:100%;max-width:500px'>
                        <tr><td style='padding:8px;border:1px solid #ddd;font-weight:bold'>Channel</td><td style='padding:8px;border:1px solid #ddd'>{campaign.Channel.ToUpper()}</td></tr>
                        <tr><td style='padding:8px;border:1px solid #ddd;font-weight:bold'>Total Messages</td><td style='padding:8px;border:1px solid #ddd'>{totalCount}</td></tr>
                        <tr><td style='padding:8px;border:1px solid #ddd;font-weight:bold'>Sent</td><td style='padding:8px;border:1px solid #ddd;color:green'>{sentCount}</td></tr>
                        <tr><td style='padding:8px;border:1px solid #ddd;font-weight:bold'>Failed</td><td style='padding:8px;border:1px solid #ddd;color:red'>{failedCount}</td></tr>
                        <tr><td style='padding:8px;border:1px solid #ddd;font-weight:bold'>Success Rate</td><td style='padding:8px;border:1px solid #ddd'>{successRate}%</td></tr>
                    </table>
                    <p style='margin-top:20px;color:#666'>This notification was sent from MarketPro Marketing Platform.</p>";

                await _emailService.SendWithUserSettingsAsync(
                    userSmtpSettings.NotificationEmail,
                    $"Campaign Completed: {campaign.Name} - {successRate}% Success",
                    emailBody,
                    userSmtpSettings);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send campaign completion email notification for campaign {CampaignId}", campaignId);
            }
        }

        _logger.LogInformation("Campaign {CampaignId} completed: Sent={Sent}, Failed={Failed}", campaignId, sentCount, failedCount);
    }

    private async Task<bool> ProcessSingleMessageAsync(CampaignMessage message, Campaign campaign, UserSmtpSettings? smtpSettings)
    {
        try
        {
            var contact = message.Contact;
            var template = campaign.Template;

            if (template is null || contact is null)
            {
                message.Status = "failed";
                message.ErrorMessage = "Template or contact not found";
                return false;
            }

            var body = PersonalizeTemplate(template.Body, contact);
            var subject = template.Subject is not null ? PersonalizeTemplate(template.Subject, contact) : campaign.Name;

            bool success;

            switch (campaign.Channel.ToLower())
            {
                case "email":
                    if (string.IsNullOrEmpty(contact.Email))
                    {
                        message.Status = "failed";
                        message.ErrorMessage = "Contact has no email address";
                        return false;
                    }
                    // Use user's SMTP settings if available
                    success = smtpSettings is not null
                        ? await _emailService.SendWithUserSettingsAsync(contact.Email, subject, body, smtpSettings)
                        : await _emailService.SendAsync(contact.Email, subject, body);
                    break;

                case "whatsapp":
                    if (string.IsNullOrEmpty(contact.WhatsAppNumber))
                    {
                        message.Status = "failed";
                        message.ErrorMessage = "Contact has no WhatsApp number";
                        return false;
                    }
                    success = smtpSettings is not null
                        ? await _whatsAppService.SendWithUserSettingsAsync(contact.WhatsAppNumber, body, smtpSettings)
                        : await _whatsAppService.SendAsync(contact.WhatsAppNumber, body);
                    break;

                case "sms":
                    if (string.IsNullOrEmpty(contact.Phone))
                    {
                        message.Status = "failed";
                        message.ErrorMessage = "Contact has no phone number";
                        return false;
                    }
                    success = smtpSettings is not null
                        ? await _smsService.SendWithUserSettingsAsync(contact.Phone, body, smtpSettings)
                        : await _smsService.SendAsync(contact.Phone, body);
                    break;

                default:
                    message.Status = "failed";
                    message.ErrorMessage = $"Unsupported channel: {campaign.Channel}";
                    return false;
            }

            message.Status = success ? "sent" : "failed";
            message.SentAt = success ? DateTime.UtcNow : null;
            if (!success) message.ErrorMessage = "Service returned failure";

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message {MessageId}", message.Id);
            message.Status = "failed";
            message.ErrorMessage = ex.Message;
            return false;
        }
    }

    private static string PersonalizeTemplate(string template, Contact contact)
    {
        var result = template
            .Replace("{{name}}", contact.FullName ?? "")
            .Replace("{{email}}", contact.Email ?? "")
            .Replace("{{phone}}", contact.Phone ?? "");

        if (!string.IsNullOrEmpty(contact.CustomFields))
        {
            try
            {
                var customFields = JsonConvert.DeserializeObject<Dictionary<string, string>>(contact.CustomFields);
                if (customFields is not null)
                {
                    foreach (var field in customFields)
                    {
                        result = result.Replace($"{{{{{field.Key}}}}}", field.Value);
                    }
                }
            }
            catch
            {
                // Ignore custom field deserialization errors
            }
        }

        return result;
    }
}
