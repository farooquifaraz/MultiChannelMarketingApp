using MarketingApp.Application.Configuration;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Application.Services;
using MarketingApp.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    private readonly IGenericRepository<User> _userRepo;
    private readonly ISystemSettingsService _systemSettings;
    private readonly ISmtpGroupService _smtpGroups;
    private readonly ILogger<CampaignJobService> _logger;
    private readonly CampaignSettings _appsettingsFallback;
    private readonly IConfiguration _config;
    private readonly IGenericRepository<Contact> _contactRepoForBounce;

    public CampaignJobService(
        ICampaignRepository campaignRepo,
        IEmailService emailService,
        IWhatsAppService whatsAppService,
        ISmsService smsService,
        INotificationService notificationService,
        IGenericRepository<UserSmtpSettings> smtpSettingsRepo,
        IGenericRepository<User> userRepo,
        ISystemSettingsService systemSettings,
        ISmtpGroupService smtpGroups,
        ILogger<CampaignJobService> logger,
        IOptions<CampaignSettings> settings,
        IConfiguration config,
        IGenericRepository<Contact> contactRepoForBounce)
    {
        _campaignRepo = campaignRepo;
        _emailService = emailService;
        _whatsAppService = whatsAppService;
        _smsService = smsService;
        _notificationService = notificationService;
        _smtpSettingsRepo = smtpSettingsRepo;
        _userRepo = userRepo;
        _systemSettings = systemSettings;
        _smtpGroups = smtpGroups;
        _logger = logger;
        _appsettingsFallback = settings.Value;
        _config = config;
        _contactRepoForBounce = contactRepoForBounce;
    }

    /// <summary>
    /// Detect SMTP hard-bounce signals in an exception message so we can permanently disable
    /// the contact. We only flag CLEAR hard-bounces — soft failures (rate limit, throttle,
    /// timeout, server error, mailbox full, greylisting) are retried elsewhere and MUST NOT
    /// taint the contact's `IsBounced` flag.
    ///
    /// Resolution order (first match wins):
    ///   1. Soft-bounce / rate-limit keywords → IsHardBounce=false (even if a hard pattern
    ///      would otherwise match later in the message). This is the most important rule —
    ///      Zoho's "Mailbox unavailable. 5.4.6 Unusual sending activity detected" looks like
    ///      a hard bounce but is actually a temporary throttle.
    ///   2. Definitively permanent SMTP enhanced status codes: 5.1.x (addressing), 5.5.x
    ///      (protocol), 5.6.x (content), 5.7.x (security/policy). NOT 5.2.x (mailbox-status
    ///      includes "mailbox full" which is soft), NOT 5.3.x (system status — often soft),
    ///      NOT 5.4.x (network/routing — Zoho rate-limit returns 5.4.6).
    ///   3. Bare 3-digit hard codes: 550 / 551 / 553 (user unknown / not local / invalid).
    ///   4. Provider-agnostic hard-bounce phrases — tightened to remove ambiguous matches
    ///      ("mailbox unavailable" / "does not exist" alone are NOT enough).
    /// </summary>
    internal static (bool IsHardBounce, string? Reason) DetectHardBounce(Exception ex)
    {
        var msg = ex.ToString().ToLowerInvariant();

        // Rule 1 — soft-bounce keywords ALWAYS win. Treat as transient; retry later, do
        // NOT flag the contact. This list grew from the 2026-06-01 prod incident where
        // 48 Zoho-throttled deliveries got wrongly hard-bounced.
        string[] softBouncePhrases =
        {
            "rate limit",
            "rate-limit",
            "rate exceeded",
            "throttle",
            "throttled",
            "too many",
            "try again later",
            "try after",
            "try after sometime",
            "unusual sending activity",
            "quota exceeded",
            "exceeded sending rate",
            "exceeded the allowed limit",
            "temporary failure",
            "temporarily unavailable",
            "service unavailable",
            "service not available",
            "greylisted",
            "greylisting",
            "deferred",
            "mailbox full",                  // 5.2.2 — soft, can free up
            "over quota",
            "is full",
            "exceeded storage allocation",
            "connection refused",            // transient network
            "connection timed out",
            "timeout",
            "temporary local problem",
        };
        foreach (var phrase in softBouncePhrases)
        {
            if (msg.Contains(phrase)) return (false, null);
        }

        // Rule 2 — definitively permanent enhanced status codes.
        // 5.1.x (addressing), 5.5.x (protocol), 5.6.x (content), 5.7.x (security/policy).
        if (System.Text.RegularExpressions.Regex.IsMatch(msg, @"\b5\.[1567]\.\d\b"))
            return (true, ExtractFirstLine(ex.Message));

        // Rule 3 — bare 3-digit permanent-failure codes.
        //   550 = user unknown / mailbox unavailable (permanent — distinct from soft 451)
        //   551 = user not local
        //   553 = invalid recipient address
        // Excluded: 552 (mailbox over quota — soft), 554 (transaction failed — ambiguous).
        if (System.Text.RegularExpressions.Regex.IsMatch(msg, @"\b55[013]\b"))
            return (true, ExtractFirstLine(ex.Message));

        // Rule 4 — provider-agnostic hard-bounce phrases.
        // NOTE: ambiguous phrases ("mailbox unavailable", "does not exist") REMOVED — they
        // appear in legitimate transient errors too (e.g. Zoho rate-limit message).
        string[] hardBouncePhrases =
        {
            "user unknown",
            "no such user",
            "no such recipient",
            "no such mailbox",
            "no mailbox here by that name",
            "address rejected",
            "recipient address rejected",
            "recipient rejected",
            "domain does not exist",
            "domain not found",
            "no relay access",
            "invalid recipient",
            "invalid mailbox",
            "invalid address",
            "user not found",
            "mailbox is disabled",
            "mailbox has been disabled",
            "account has been disabled",
            "account is disabled",
            "account does not exist",
            "this account is locked",
            "blacklisted by recipient",
        };
        foreach (var phrase in hardBouncePhrases)
        {
            if (msg.Contains(phrase)) return (true, ExtractFirstLine(ex.Message));
        }
        return (false, null);
    }

    private static string ExtractFirstLine(string s)
        => string.IsNullOrEmpty(s) ? "Hard bounce" : s.Split('\n')[0].Trim();

    private static bool LooksLikeValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        try
        {
            var addr = new System.Net.Mail.MailAddress(email.Trim());
            return addr.Address.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    /// <summary>Build a 1x1 open-tracking pixel for the given message — appended to email HTML body.</summary>
    private string? BuildTrackingPixel(Guid messageId)
    {
        var baseUrl = _config["App:PublicBaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl)) return null;
        var url = $"{baseUrl}/track/open/{messageId}.gif";
        // Defensive styling so Gmail/Outlook don't try to render or summarize this.
        return $"<img src=\"{url}\" width=\"1\" height=\"1\" alt=\"\" border=\"0\" style=\"display:block;width:1px;height:1px;border:0;outline:none;\" />";
    }

    /// <summary>
    /// Rewrite every absolute http(s) href in the body to redirect through /track/click,
    /// so we can record clicks per message. Skips mailto:, tel:, anchor-only (#), and the
    /// tracking pixel itself. Idempotent — if a URL is already a tracking URL we leave it.
    /// </summary>
    private string RewriteLinksForClickTracking(string body, Guid messageId)
    {
        var baseUrl = _config["App:PublicBaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrEmpty(body)) return body;

        var trackPrefix = $"{baseUrl}/track/";

        // === Step 1: auto-link bare (plain-text) URLs so they get tracked too ===
        // Many emails are typed as plain text — "https://site.com" is NOT an <a> tag, so the href-rewrite
        // below would miss it and the click would go straight to the destination (untracked). Here we wrap
        // bare http(s) URLs that appear in TEXT into proper anchors first. Existing <a>…</a> are protected
        // (placeholdered) so we never double-wrap or nest anchors, and attribute URLs (preceded by " ' =)
        // are skipped via the lookbehind.
        var anchors = new List<string>();
        var protectedBody = System.Text.RegularExpressions.Regex.Replace(
            body, @"<a\b[^>]*>.*?</a>",
            m => { anchors.Add(m.Value); return $"A{anchors.Count - 1}"; },
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

        protectedBody = System.Text.RegularExpressions.Regex.Replace(
            protectedBody, @"(?<![""'=])\bhttps?://[^\s<>""']+",
            m =>
            {
                var raw = m.Value;
                // Strip trailing sentence punctuation so it isn't swallowed into the link.
                var url = raw.TrimEnd('.', ',', ';', ':', '!', '?', ')', ']', '}', '"', '\'');
                var trail = raw.Substring(url.Length);
                return $"<a href=\"{url}\">{url}</a>{trail}";
            },
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Restore the protected original anchors.
        if (anchors.Count > 0)
            protectedBody = System.Text.RegularExpressions.Regex.Replace(
                protectedBody, "A(\\d+)",
                m => { var ix = int.Parse(m.Groups[1].Value); return ix >= 0 && ix < anchors.Count ? anchors[ix] : m.Value; });

        // === Step 2: rewrite EVERY href (original + the ones we just created) through /track/click ===
        // Matches the URL inside href="..." or href='...'. Captures the quote so we put it back.
        var pattern = new System.Text.RegularExpressions.Regex(
            @"href\s*=\s*(""|')([^""']+)\1",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return pattern.Replace(protectedBody, match =>
        {
            var quote = match.Groups[1].Value;
            var url = match.Groups[2].Value;

            // Skip non-http(s), in-page anchors, and already-tracked URLs
            if (string.IsNullOrWhiteSpace(url)) return match.Value;
            if (url.StartsWith("#") || url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("tel:", StringComparison.OrdinalIgnoreCase))
                return match.Value;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed)
                || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
                return match.Value;
            if (url.StartsWith(trackPrefix, StringComparison.OrdinalIgnoreCase))
                return match.Value;

            var encoded = Uri.EscapeDataString(url);
            var tracked = $"{baseUrl}/track/click/{messageId}?u={encoded}";
            return $"href={quote}{tracked}{quote}";
        });
    }

    public async Task ProcessCampaignAsync(Guid campaignId)
    {
        _logger.LogInformation("Starting campaign processing: {CampaignId}", campaignId);

        // Pull live admin-controlled settings from DB (with fallback to appsettings.json).
        // The per-SmtpGroup overrides (if any) are applied LATER, once we resolve the group.
        CampaignSettings _settings;
        try { _settings = await _systemSettings.GetCampaignSettingsAsync(); }
        catch { _settings = _appsettingsFallback; }

        var campaign = await _campaignRepo.GetWithTemplateAsync(campaignId, default);
        if (campaign is null)
        {
            _logger.LogError("Campaign {CampaignId} not found", campaignId);
            return;
        }

        // Defensive: if the campaign was cancelled (status reverted to draft) after being scheduled,
        // skip processing. This prevents a "cancelled" campaign from firing when its scheduled time arrives.
        if (campaign.Status != "queued")
        {
            _logger.LogInformation("Campaign {CampaignId} in status '{Status}' — scheduled job is a no-op (likely cancelled).", campaignId, campaign.Status);
            return;
        }

        // === SMTP Group resolution ===
        // Each user is assigned to an SmtpGroup (or falls back to the platform default group).
        // The group's provider credentials + signature get used to send the campaign.
        // BUT — the From Name is overridden with the actual sender's full name so recipients see
        // "Iqra Tariq <ahsan@samdigital.ae>" rather than the static org name.
        UserSmtpSettings? userSmtpSettings = null;
        User? sender = null;
        try
        {
            sender = await _userRepo.GetByIdAsync(campaign.UserId, default);
            var resolvedGroup = await _smtpGroups.ResolveForUserAsync(campaign.UserId, default);
            if (resolvedGroup is not null)
            {
                userSmtpSettings = SmtpGroupService.ToUserSmtpSettings(resolvedGroup);
                // Override From Name with the actual sender — keeps the From Email as the org address.
                if (sender is not null && !string.IsNullOrWhiteSpace(sender.FullName))
                    userSmtpSettings.SmtpFromName = sender.FullName;

                // === Per-SmtpGroup rate limit overrides ===
                // If the group has its own limits set, they take precedence over the global SystemSettings.
                // Useful when one group sends via Gmail (slow, capped) and another via SendGrid (fast).
                var overrides = new List<string>();
                if (resolvedGroup.DelayBetweenMessagesMs.HasValue)
                {
                    _settings = new CampaignSettings
                    {
                        BatchSize = _settings.BatchSize,
                        DelayBetweenBatchesMs = _settings.DelayBetweenBatchesMs,
                        DelayBetweenMessagesMs = resolvedGroup.DelayBetweenMessagesMs.Value,
                        MaxMessagesPerMinute = _settings.MaxMessagesPerMinute,
                    };
                    overrides.Add($"delay={resolvedGroup.DelayBetweenMessagesMs}ms");
                }
                if (resolvedGroup.MaxMessagesPerMinute.HasValue)
                {
                    _settings = new CampaignSettings
                    {
                        BatchSize = _settings.BatchSize,
                        DelayBetweenBatchesMs = _settings.DelayBetweenBatchesMs,
                        DelayBetweenMessagesMs = _settings.DelayBetweenMessagesMs,
                        MaxMessagesPerMinute = resolvedGroup.MaxMessagesPerMinute.Value,
                    };
                    overrides.Add($"maxPerMin={resolvedGroup.MaxMessagesPerMinute}");
                }
                if (overrides.Count > 0)
                    _logger.LogInformation("Applied per-group rate-limit overrides: {Overrides}", string.Join(", ", overrides));

                _logger.LogInformation(
                    "Resolved SMTP group '{GroupName}' (provider={Provider}, from={FromName} <{FromEmail}>) for campaign owner {UserId}",
                    resolvedGroup.Name, resolvedGroup.EmailProvider, userSmtpSettings.SmtpFromName, resolvedGroup.FromEmail, campaign.UserId);
            }
            else
            {
                _logger.LogWarning(
                    "No SMTP group resolved for user {UserId} — no assignment and no default group. Falling back to mock send.",
                    campaign.UserId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not resolve SMTP group for user {UserId}, using mock", campaign.UserId);
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
                .GroupBy(x => x.idx / _settings.BatchSize)
                .Select(g => g.Select(x => x.msg).ToList())
                .ToList();

            _logger.LogInformation(
                "Processing {BatchCount} batches for campaign {CampaignId} | BatchSize={BatchSize}, BatchDelay={BatchDelay}ms, MsgDelay={MsgDelay}ms, MaxPerMin={MaxPerMin}",
                batches.Count, campaignId, _settings.BatchSize, _settings.DelayBetweenBatchesMs,
                _settings.DelayBetweenMessagesMs, _settings.MaxMessagesPerMinute);

            // Rate limiter state (for MaxMessagesPerMinute)
            var minuteWindowStart = DateTime.UtcNow;
            var messagesInCurrentMinute = 0;

            var batchNumber = 0;
            foreach (var batch in batches)
            {
                batchNumber++;
                _logger.LogInformation("Processing batch {BatchNum}/{TotalBatches} for campaign {CampaignId}",
                    batchNumber, batches.Count, campaignId);

                for (var i = 0; i < batch.Count; i++)
                {
                    var message = batch[i];

                    // === Rate limiting: max messages per minute ===
                    if (_settings.MaxMessagesPerMinute > 0)
                    {
                        var elapsed = DateTime.UtcNow - minuteWindowStart;
                        if (elapsed.TotalSeconds >= 60)
                        {
                            minuteWindowStart = DateTime.UtcNow;
                            messagesInCurrentMinute = 0;
                        }
                        else if (messagesInCurrentMinute >= _settings.MaxMessagesPerMinute)
                        {
                            var waitMs = (int)Math.Ceiling((60 - elapsed.TotalSeconds) * 1000);
                            _logger.LogInformation(
                                "Rate limit hit ({Max}/min). Pausing {WaitMs}ms for campaign {CampaignId}",
                                _settings.MaxMessagesPerMinute, waitMs, campaignId);
                            await Task.Delay(waitMs);
                            minuteWindowStart = DateTime.UtcNow;
                            messagesInCurrentMinute = 0;
                        }
                    }

                    var success = await ProcessSingleMessageAsync(message, campaign, userSmtpSettings, sender);
                    if (success) sentCount++;
                    else failedCount++;

                    await _campaignRepo.UpdateMessageAsync(message, default);
                    messagesInCurrentMinute++;

                    // === Per-message delay (helps avoid spam filters) ===
                    var isLastInBatch = i == batch.Count - 1;
                    var isLastBatch = batchNumber == batches.Count;
                    if (_settings.DelayBetweenMessagesMs > 0 && !(isLastInBatch && isLastBatch))
                    {
                        await Task.Delay(_settings.DelayBetweenMessagesMs);
                    }
                }

                if (batchNumber < batches.Count)
                    await Task.Delay(_settings.DelayBetweenBatchesMs);
            }

            campaign.Status = "completed";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Campaign {CampaignId} processing failed", campaignId);
            campaign.Status = "failed";
        }

        campaign.CompletedAt = DateTime.UtcNow;
        // M6 — recompute the snapshot tallies from ALL of this campaign's messages, not just the
        // counters accumulated in THIS run. On a "Retry failed" pass `sentCount` only reflects the
        // re-sent messages, so writing it directly used to clobber the original success count
        // (e.g. a 95-recipient campaign showed "Sent 43" after retrying 43). Counting the rows keeps
        // it cumulative and correct.
        campaign.SentCount = await _campaignRepo.CountMessagesByStatusAsync(
            campaignId, new[] { "sent", "delivered", "opened", "clicked" }, default);
        campaign.FailedCount = await _campaignRepo.CountMessagesByStatusAsync(
            campaignId, new[] { "failed", "bounced" }, default);
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

    private async Task<bool> ProcessSingleMessageAsync(CampaignMessage message, Campaign campaign, UserSmtpSettings? smtpSettings, User? sender)
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

            // Pull avatar URL + locale from admin-configured SystemSettings (with safe fallbacks).
            string avatarUrl = "https://ui-avatars.com/api/?name={name}&size={size}&background={bg}&color={fg}&bold=true&rounded=true";
            string dateFormat = "MMMM dd, yyyy";
            System.Globalization.CultureInfo culture = System.Globalization.CultureInfo.InvariantCulture;
            try
            {
                var sys = await _systemSettings.GetAsync();
                if (!string.IsNullOrWhiteSpace(sys.AvatarServiceUrl)) avatarUrl = sys.AvatarServiceUrl;
                if (!string.IsNullOrWhiteSpace(sys.DefaultDateFormat)) dateFormat = sys.DefaultDateFormat;
                if (!string.IsNullOrWhiteSpace(sys.DefaultLocale))
                {
                    try { culture = System.Globalization.CultureInfo.GetCultureInfo(sys.DefaultLocale); } catch { /* fall back */ }
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not load system settings for personalization"); }

            var body = PersonalizeTemplate(template.Body, contact, sender, smtpSettings, avatarUrl, dateFormat, culture);
            var subject = template.Subject is not null
                ? PersonalizeTemplate(template.Subject, contact, sender, smtpSettings, avatarUrl, dateFormat, culture)
                : campaign.Name;

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
                    // Pre-flight: syntactically invalid email is treated as a hard bounce so we don't waste
                    // sender reputation re-trying it on every campaign.
                    if (!LooksLikeValidEmail(contact.Email))
                    {
                        message.Status = "bounced";
                        message.ErrorMessage = "Invalid email address syntax";
                        await MarkContactBouncedAsync(contact.Id, "Invalid email address syntax");
                        return false;
                    }

                    // === Click + Open tracking ===
                    // 1. Rewrite every absolute http(s) link to redirect through /track/click/{messageId}
                    //    so we record clicks. Skips mailto:, tel:, anchors, and the tracking pixel itself.
                    // 2. Append a 1x1 open-tracking pixel.
                    // Both no-op when App:PublicBaseUrl isn't configured.
                    var rewritten = RewriteLinksForClickTracking(body, message.Id);
                    var pixel = BuildTrackingPixel(message.Id);
                    var bodyToSend = pixel is null ? rewritten : rewritten + "\n" + pixel;

                    // === Day 7 Message-Id generation (Feature 1 + 2 foundation) ===
                    // Generate a stable Message-Id per CampaignMessage. Provider webhooks (Feature 1) and
                    // inbox polling (Feature 2) both look this up to correlate events / replies back to the
                    // originating CampaignMessage. Persist on the entity BEFORE send so even on transient
                    // failure we don't lose the correlation key.
                    var fromAddr = smtpSettings?.SmtpFromEmail ?? smtpSettings?.SmtpUsername ?? "noreply@localhost";
                    var fromDomain = fromAddr.Contains('@') ? fromAddr[(fromAddr.IndexOf('@') + 1)..] : "localhost";
                    var smtpMessageId = $"<{message.Id:N}@{fromDomain}>";
                    message.SmtpMessageId = smtpMessageId;

                    var headers = new EmailHeaders
                    {
                        MessageId = smtpMessageId,
                        CampaignMessageId = message.Id.ToString(),
                    };

                    // Only use real SMTP if credentials are actually filled. Otherwise fall back to mock
                    // so that signature/personalization can still be tested without SMTP setup.
                    var hasUsableEmailProvider = smtpSettings is not null && (
                        (string.Equals(smtpSettings.EmailProvider, "smtp", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(smtpSettings.SmtpHost) && !string.IsNullOrWhiteSpace(smtpSettings.SmtpUsername)) ||
                        (string.Equals(smtpSettings.EmailProvider, "sendgrid", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(smtpSettings.SendGridApiKey)) ||
                        (string.Equals(smtpSettings.EmailProvider, "brevo", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(smtpSettings.BrevoApiKey)) ||
                        (string.Equals(smtpSettings.EmailProvider, "mailgun", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(smtpSettings.MailgunApiKey) && !string.IsNullOrWhiteSpace(smtpSettings.MailgunDomain))
                    );
                    success = hasUsableEmailProvider
                        ? await _emailService.SendWithUserSettingsAsync(contact.Email, subject, bodyToSend, smtpSettings!, headers, default)
                        : await _emailService.SendAsync(contact.Email, subject, bodyToSend);

                    // === Smart SMTP delivery fallback ===
                    // Pure SMTP providers (Gmail/Hostinger/Outlook/etc.) don't give us delivery webhooks,
                    // so a successful send is the strongest signal we have. Mark delivered now as
                    // "best-effort"; webhook-capable providers (SendGrid/Brevo/Mailgun) will UPGRADE
                    // this to "webhook" later when their event fires.
                    if (success && hasUsableEmailProvider
                        && string.Equals(smtpSettings!.EmailProvider, "smtp", StringComparison.OrdinalIgnoreCase))
                    {
                        message.DeliveredAt = DateTime.UtcNow;
                        message.DeliveryConfirmationKind = "best-effort";
                    }
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

            // If a best-effort/webhook delivery was recorded above (SMTP send succeeded), reflect it in the
            // status so the Delivery Report's "Delivered" count is accurate — not stuck on "sent".
            message.Status = success ? (message.DeliveredAt is not null ? "delivered" : "sent") : "failed";
            message.SentAt = success ? DateTime.UtcNow : null;
            if (!success) message.ErrorMessage = "Service returned failure";

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message {MessageId}", message.Id);

            // Post-flight: if the SMTP error pattern matches a hard bounce, flag the contact so
            // future campaigns skip them. Soft errors (timeout, throttle) are NOT bounced.
            var bounce = DetectHardBounce(ex);
            if (bounce.IsHardBounce && message.ContactId != Guid.Empty)
            {
                message.Status = "bounced";
                message.ErrorMessage = bounce.Reason ?? ex.Message;
                await MarkContactBouncedAsync(message.ContactId, bounce.Reason ?? "Hard bounce detected");
            }
            else
            {
                message.Status = "failed";
                message.ErrorMessage = ex.Message;
            }
            return false;
        }
    }

    /// <summary>Persist a hard-bounce flag on the contact (idempotent — safe to call repeatedly).</summary>
    private async Task MarkContactBouncedAsync(Guid contactId, string reason)
    {
        try
        {
            var contact = await _contactRepoForBounce.GetByIdAsync(contactId, default);
            if (contact is null || contact.IsBounced) return; // already flagged
            contact.IsBounced = true;
            contact.BouncedAt = DateTime.UtcNow;
            contact.BounceReason = reason.Length > 500 ? reason[..500] : reason;
            await _contactRepoForBounce.UpdateAsync(contact, default);
            _logger.LogWarning("Contact {ContactId} auto-flagged as bounced: {Reason}", contactId, reason);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to flag contact {ContactId} as bounced", contactId);
        }
    }

    private static string PersonalizeTemplate(string template, Contact contact, User? sender, UserSmtpSettings? smtpSettings,
        string avatarServiceUrl = "https://ui-avatars.com/api/?name={name}&size={size}&background={bg}&color={fg}&bold=true&rounded=true",
        string dateFormat = "MMMM dd, yyyy",
        System.Globalization.CultureInfo? culture = null)
    {
        // === Merged signature resolution ===
        //   sender_name        = the actual sender's full name (User.FullName)
        //   sender_email       = org email from SmtpGroup (so recipients reply to the org address)
        //   sender_designation = user.SignatureDesignation OVERRIDES smtpGroup.SignatureDesignation
        //   sender_phone       = user.SignaturePhone       OVERRIDES smtpGroup.SignaturePhone
        //   signature_image    = user.SignatureImageUrl    OVERRIDES smtpGroup.SignatureImageUrl
        //   company_name       = smtpGroup.FromName (org brand)
        //   company_website    = smtpGroup.CompanyWebsite (org)
        var senderName = sender?.FullName ?? smtpSettings?.SmtpFromName ?? "";
        var senderEmail = smtpSettings?.SmtpFromEmail ?? sender?.Email ?? "";
        var companyName = smtpSettings?.SmtpFromName ?? sender?.FullName ?? "";

        var senderDesignation = !string.IsNullOrWhiteSpace(sender?.SignatureDesignation)
            ? sender!.SignatureDesignation!
            : (smtpSettings?.SignatureDesignation ?? "");
        var senderPhone = !string.IsNullOrWhiteSpace(sender?.SignaturePhone)
            ? sender!.SignaturePhone!
            : (smtpSettings?.SignaturePhone ?? "");
        var companyWebsite = smtpSettings?.CompanyWebsite ?? "";

        // Image: per-user override → org default → admin-configured avatar service template
        // Template placeholders: {name}, {size}, {bg}, {fg} — admin can swap providers in SystemSettings.
        var defaultAvatarUrl = (avatarServiceUrl ?? "")
            .Replace("{name}", Uri.EscapeDataString(senderName))
            .Replace("{size}", "128")
            .Replace("{bg}", "6366f1")
            .Replace("{fg}", "fff");
        var signatureImage =
            !string.IsNullOrWhiteSpace(sender?.SignatureImageUrl) ? sender!.SignatureImageUrl! :
            !string.IsNullOrWhiteSpace(smtpSettings?.SignatureImageUrl) ? smtpSettings!.SignatureImageUrl! :
            defaultAvatarUrl;

        var result = template
            // --- Contact (recipient) placeholders ---
            .Replace("{{name}}", contact.FullName ?? "")
            .Replace("{{first_name}}", (contact.FullName ?? "").Split(' ').FirstOrDefault() ?? "")
            .Replace("{{email}}", contact.Email ?? "")
            .Replace("{{phone}}", contact.Phone ?? "")
            // --- Sender (signature) placeholders ---
            .Replace("{{sender_name}}", senderName)
            .Replace("{{sender_email}}", senderEmail)
            .Replace("{{sender_designation}}", senderDesignation)
            .Replace("{{sender_phone}}", senderPhone)
            .Replace("{{signature_image}}", signatureImage)
            .Replace("{{company_name}}", companyName)
            .Replace("{{company_website}}", companyWebsite)
            // --- Date placeholders (locale-aware via admin SystemSettings) ---
            .Replace("{{current_year}}", DateTime.UtcNow.Year.ToString())
            .Replace("{{current_date}}", DateTime.UtcNow.ToString(dateFormat ?? "MMMM dd, yyyy", culture ?? System.Globalization.CultureInfo.InvariantCulture));

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
