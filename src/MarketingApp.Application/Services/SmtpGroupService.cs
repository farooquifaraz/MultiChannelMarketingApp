using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Domain.Entities;
using MarketingApp.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Application.Services;

public class SmtpGroupService : ISmtpGroupService
{
    private readonly IGenericRepository<SmtpGroup> _groupRepo;
    private readonly IGenericRepository<User> _userRepo;
    private readonly IEmailService _emailService;
    private readonly ILogger<SmtpGroupService> _logger;

    public SmtpGroupService(
        IGenericRepository<SmtpGroup> groupRepo,
        IGenericRepository<User> userRepo,
        IEmailService emailService,
        ILogger<SmtpGroupService> logger)
    {
        _groupRepo = groupRepo;
        _userRepo = userRepo;
        _emailService = emailService;
        _logger = logger;
    }

    private static string? Mask(string? secret) =>
        string.IsNullOrWhiteSpace(secret) ? null : $"{secret[..Math.Min(4, secret.Length)]}…{(secret.Length > 8 ? "****" : "")}";

    private async Task<int> CountAssignedUsersAsync(Guid groupId, CancellationToken ct) =>
        (await _userRepo.FindAsync(u => u.SmtpGroupId == groupId, ct)).Count();

    private async Task<SmtpGroupDto> ToDtoAsync(SmtpGroup g, CancellationToken ct) => new()
    {
        Id = g.Id,
        Name = g.Name,
        Description = g.Description,
        IsDefault = g.IsDefault,
        IsActive = g.IsActive,
        EmailProvider = g.EmailProvider,
        SmtpHost = g.SmtpHost,
        SmtpPort = g.SmtpPort,
        SmtpUsername = g.SmtpUsername,
        SmtpPasswordSet = !string.IsNullOrWhiteSpace(g.SmtpPassword),
        SmtpEnableSsl = g.SmtpEnableSsl,
        SmtpTimeout = g.SmtpTimeout,
        SendGridApiKeyMasked = Mask(g.SendGridApiKey),
        BrevoApiKeyMasked = Mask(g.BrevoApiKey),
        MailgunApiKeyMasked = Mask(g.MailgunApiKey),
        MailgunDomain = g.MailgunDomain,
        FromEmail = g.FromEmail,
        FromName = g.FromName,
        SignatureDesignation = g.SignatureDesignation,
        SignaturePhone = g.SignaturePhone,
        CompanyWebsite = g.CompanyWebsite,
        SignatureImageUrl = g.SignatureImageUrl,
        WhatsAppPhoneNumberId = g.WhatsAppPhoneNumberId,
        WhatsAppBusinessAccountId = g.WhatsAppBusinessAccountId,
        SmsSenderNumber = g.SmsSenderNumber,
        DelayBetweenMessagesMs = g.DelayBetweenMessagesMs,
        MaxMessagesPerMinute = g.MaxMessagesPerMinute,
        // Day 7 G2
        SendGridWebhookSecretMasked = MaskSecret(g.SendGridWebhookSecret),
        BrevoWebhookSecretMasked = MaskSecret(g.BrevoWebhookSecret),
        MailgunWebhookSecretMasked = MaskSecret(g.MailgunWebhookSecret),
        // Day 7 G3
        EnableInboxPolling = g.EnableInboxPolling,
        ImapHost = g.ImapHost,
        ImapPort = g.ImapPort,
        ImapEnableSsl = g.ImapEnableSsl,
        ImapUsername = g.ImapUsername,
        ImapFolder = g.ImapFolder,
        LastImapUid = g.LastImapUid,
        LastInboxPolledAt = g.LastInboxPolledAt,
        InboxPollingIntervalMinutes = g.InboxPollingIntervalMinutes,
        DefaultInboxOwnerUserId = g.DefaultInboxOwnerUserId,
        AssignedUserCount = await CountAssignedUsersAsync(g.Id, ct),
        CreatedAt = g.CreatedAt,
        UpdatedAt = g.UpdatedAt,
    };

    private static string? MaskSecret(string? s) =>
        string.IsNullOrEmpty(s) ? null :
        s.Length <= 4 ? new string('•', s.Length) :
        new string('•', 8) + s[^4..];

    public async Task<IEnumerable<SmtpGroupDto>> GetAllAsync(CancellationToken ct = default)
    {
        var groups = await _groupRepo.FindAsync(g => true, ct);
        var dtos = new List<SmtpGroupDto>();
        foreach (var g in groups.OrderByDescending(g => g.IsDefault).ThenBy(g => g.Name))
            dtos.Add(await ToDtoAsync(g, ct));
        return dtos;
    }

    public async Task<SmtpGroupDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var g = await _groupRepo.GetByIdAsync(id, ct);
        return g is null ? null : await ToDtoAsync(g, ct);
    }

    public async Task<SmtpGroupDto> CreateAsync(Guid createdByUserId, CreateSmtpGroupDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new AppValidationException(new List<string> { "Name is required." });

        // If this group is marked default, clear the previous default first.
        if (dto.IsDefault)
            await UnsetExistingDefaultAsync(ct);

        var g = MapToEntity(dto, new SmtpGroup { CreatedByUserId = createdByUserId });
        await _groupRepo.AddAsync(g, ct);
        _logger.LogInformation("SMTP group created: {Name} (id={Id}) by user {UserId}", g.Name, g.Id, createdByUserId);
        return await ToDtoAsync(g, ct);
    }

    public async Task<SmtpGroupDto> CloneAsync(Guid sourceId, Guid createdByUserId, CancellationToken ct = default)
    {
        var src = await _groupRepo.GetByIdAsync(sourceId, ct) ?? throw new NotFoundException("SmtpGroup", sourceId);

        // Generate a unique "Copy of …" name so admins can clone repeatedly without collisions.
        var existingNames = (await _groupRepo.FindAsync(g => true, ct)).Select(g => g.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var baseName = $"Copy of {src.Name}";
        var name = baseName;
        var n = 2;
        while (existingNames.Contains(name)) name = $"{baseName} ({n++})";

        var clone = new SmtpGroup
        {
            // New identity — never inherit default flag or user assignments.
            Name = name,
            Description = src.Description,
            IsDefault = false,
            IsActive = src.IsActive,
            EmailProvider = src.EmailProvider,
            // SMTP (incl. password — full duplicate so admin only tweaks From + creds)
            SmtpHost = src.SmtpHost,
            SmtpPort = src.SmtpPort,
            SmtpUsername = src.SmtpUsername,
            SmtpPassword = src.SmtpPassword,
            SmtpEnableSsl = src.SmtpEnableSsl,
            SmtpTimeout = src.SmtpTimeout,
            // API providers
            SendGridApiKey = src.SendGridApiKey,
            BrevoApiKey = src.BrevoApiKey,
            MailgunApiKey = src.MailgunApiKey,
            MailgunDomain = src.MailgunDomain,
            // From + signature
            FromEmail = src.FromEmail,
            FromName = src.FromName,
            SignatureDesignation = src.SignatureDesignation,
            SignaturePhone = src.SignaturePhone,
            CompanyWebsite = src.CompanyWebsite,
            SignatureImageUrl = src.SignatureImageUrl,
            // WhatsApp + SMS
            WhatsAppApiKey = src.WhatsAppApiKey,
            WhatsAppPhoneNumberId = src.WhatsAppPhoneNumberId,
            WhatsAppBusinessAccountId = src.WhatsAppBusinessAccountId,
            SmsApiKey = src.SmsApiKey,
            SmsApiSecret = src.SmsApiSecret,
            SmsSenderNumber = src.SmsSenderNumber,
            // Rate limits + webhook secrets
            DelayBetweenMessagesMs = src.DelayBetweenMessagesMs,
            MaxMessagesPerMinute = src.MaxMessagesPerMinute,
            SendGridWebhookSecret = src.SendGridWebhookSecret,
            BrevoWebhookSecret = src.BrevoWebhookSecret,
            MailgunWebhookSecret = src.MailgunWebhookSecret,
            // IMAP — copy config but RESET the dedup cursor + polling timestamp (fresh mailbox).
            EnableInboxPolling = src.EnableInboxPolling,
            ImapHost = src.ImapHost,
            ImapPort = src.ImapPort,
            ImapEnableSsl = src.ImapEnableSsl,
            ImapUsername = src.ImapUsername,
            ImapPassword = src.ImapPassword,
            ImapFolder = src.ImapFolder,
            LastImapUid = 0,
            LastInboxPolledAt = null,
            InboxPollingIntervalMinutes = src.InboxPollingIntervalMinutes,
            // Catch-all owner intentionally NOT copied — admin should pick the new group's owner explicitly.
            DefaultInboxOwnerUserId = null,
            CreatedByUserId = createdByUserId,
        };
        await _groupRepo.AddAsync(clone, ct);
        _logger.LogInformation("SMTP group cloned: '{Source}' → '{Clone}' (id={Id}) by user {UserId}", src.Name, clone.Name, clone.Id, createdByUserId);
        return await ToDtoAsync(clone, ct);
    }

    public async Task<SmtpGroupDto> UpdateAsync(Guid id, UpdateSmtpGroupDto dto, CancellationToken ct = default)
    {
        var g = await _groupRepo.GetByIdAsync(id, ct) ?? throw new NotFoundException("SmtpGroup", id);
        if (dto.IsDefault && !g.IsDefault)
            await UnsetExistingDefaultAsync(ct);
        MapToEntity(dto, g);
        g.UpdatedAt = DateTime.UtcNow;
        await _groupRepo.UpdateAsync(g, ct);
        return await ToDtoAsync(g, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var g = await _groupRepo.GetByIdAsync(id, ct) ?? throw new NotFoundException("SmtpGroup", id);
        if (g.IsDefault)
            throw new ConflictException("Cannot delete the default SMTP group. Mark another group as default first.");
        var assignedCount = await CountAssignedUsersAsync(id, ct);
        if (assignedCount > 0)
            throw new ConflictException($"Cannot delete: {assignedCount} user(s) are still assigned to this group. Reassign them first.");
        await _groupRepo.DeleteAsync(g, ct);
    }

    public async Task<SmtpGroupDto> SetDefaultAsync(Guid id, CancellationToken ct = default)
    {
        var g = await _groupRepo.GetByIdAsync(id, ct) ?? throw new NotFoundException("SmtpGroup", id);
        await UnsetExistingDefaultAsync(ct);
        g.IsDefault = true;
        g.UpdatedAt = DateTime.UtcNow;
        await _groupRepo.UpdateAsync(g, ct);
        return await ToDtoAsync(g, ct);
    }

    public async Task<int> AssignUsersAsync(Guid groupId, List<Guid> userIds, CancellationToken ct = default)
    {
        var g = await _groupRepo.GetByIdAsync(groupId, ct) ?? throw new NotFoundException("SmtpGroup", groupId);
        // Admins configure groups for others — they shouldn't be assigned as senders themselves.
        // Skip any admin IDs that slip through (defense-in-depth against direct API calls).
        var users = (await _userRepo.FindAsync(u => userIds.Contains(u.Id), ct))
            .Where(u => !string.Equals(u.Role, "admin", StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var u in users)
        {
            u.SmtpGroupId = g.Id;
            await _userRepo.UpdateAsync(u, ct);
        }
        _logger.LogInformation("Assigned {Count} users to group {GroupName}", users.Count, g.Name);
        return users.Count;
    }

    public async Task<int> UnassignUsersAsync(List<Guid> userIds, CancellationToken ct = default)
    {
        var users = (await _userRepo.FindAsync(u => userIds.Contains(u.Id), ct)).ToList();
        foreach (var u in users)
        {
            u.SmtpGroupId = null;
            await _userRepo.UpdateAsync(u, ct);
        }
        return users.Count;
    }

    public async Task<bool> TestConnectionAsync(Guid groupId, string testEmail, CancellationToken ct = default)
    {
        var g = await _groupRepo.GetByIdAsync(groupId, ct) ?? throw new NotFoundException("SmtpGroup", groupId);
        var adapter = ToUserSmtpSettings(g);
        var body = $@"<div style='font-family:Arial,sans-serif;padding:24px;max-width:600px;margin:0 auto'>
            <h2 style='color:#4f46e5'>SMTP Group Test Successful! ✅</h2>
            <p>Settings for <strong>{g.Name}</strong> are working correctly.</p>
            <p>Provider: <strong>{g.EmailProvider.ToUpper()}</strong> · From: <strong>{g.FromEmail}</strong></p>
        </div>";
        var ok = await _emailService.SendWithUserSettingsAsync(testEmail, $"SMTP test: {g.Name}", body, adapter, ct);
        if (!ok) throw new InvalidOperationException("Test email failed. Check the provider credentials.");
        return ok;
    }

    public async Task<IEnumerable<UserAssignmentDto>> GetUserAssignmentsAsync(CancellationToken ct = default)
    {
        // Only return regular (non-admin) users — admins are not assignable senders.
        var users = await _userRepo.FindAsync(
            u => u.IsActive && u.Role != "admin",
            ct);
        var groups = (await _groupRepo.FindAsync(g => true, ct)).ToDictionary(g => g.Id, g => g.Name);
        return users.OrderBy(u => u.Email).Select(u => new UserAssignmentDto
        {
            UserId = u.Id,
            Email = u.Email,
            FullName = u.FullName,
            Role = u.Role,
            SmtpGroupId = u.SmtpGroupId,
            SmtpGroupName = u.SmtpGroupId.HasValue && groups.TryGetValue(u.SmtpGroupId.Value, out var n) ? n : null,
        });
    }

    public async Task<SmtpGroup?> ResolveForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userRepo.GetByIdAsync(userId, ct);
        if (user?.SmtpGroupId is Guid sgid)
        {
            var assigned = await _groupRepo.GetByIdAsync(sgid, ct);
            if (assigned is { IsActive: true }) return assigned;
        }
        // Fallback to platform default
        var defaultGroup = (await _groupRepo.FindAsync(g => g.IsDefault && g.IsActive, ct)).FirstOrDefault();
        return defaultGroup;
    }

    // === Helpers ===

    /// <summary>Map a CreateSmtpGroupDto to an entity (used for both Create and Update).</summary>
    private static SmtpGroup MapToEntity(CreateSmtpGroupDto dto, SmtpGroup target)
    {
        target.Name = dto.Name;
        target.Description = dto.Description;
        target.IsDefault = dto.IsDefault;
        target.IsActive = dto.IsActive;
        target.EmailProvider = dto.EmailProvider;
        target.SmtpHost = dto.SmtpHost;
        target.SmtpPort = dto.SmtpPort;
        target.SmtpUsername = dto.SmtpUsername;
        if (!string.IsNullOrWhiteSpace(dto.SmtpPassword)) target.SmtpPassword = dto.SmtpPassword;
        target.SmtpEnableSsl = dto.SmtpEnableSsl;
        target.SmtpTimeout = dto.SmtpTimeout;
        if (!string.IsNullOrWhiteSpace(dto.SendGridApiKey)) target.SendGridApiKey = dto.SendGridApiKey;
        if (!string.IsNullOrWhiteSpace(dto.BrevoApiKey)) target.BrevoApiKey = dto.BrevoApiKey;
        if (!string.IsNullOrWhiteSpace(dto.MailgunApiKey)) target.MailgunApiKey = dto.MailgunApiKey;
        target.MailgunDomain = dto.MailgunDomain;
        target.FromEmail = dto.FromEmail;
        target.FromName = dto.FromName;
        target.SignatureDesignation = dto.SignatureDesignation;
        target.SignaturePhone = dto.SignaturePhone;
        target.CompanyWebsite = dto.CompanyWebsite;
        target.SignatureImageUrl = dto.SignatureImageUrl;
        if (!string.IsNullOrWhiteSpace(dto.WhatsAppApiKey)) target.WhatsAppApiKey = dto.WhatsAppApiKey;
        target.WhatsAppPhoneNumberId = dto.WhatsAppPhoneNumberId;
        target.WhatsAppBusinessAccountId = dto.WhatsAppBusinessAccountId;
        if (!string.IsNullOrWhiteSpace(dto.SmsApiKey)) target.SmsApiKey = dto.SmsApiKey;
        if (!string.IsNullOrWhiteSpace(dto.SmsApiSecret)) target.SmsApiSecret = dto.SmsApiSecret;
        target.SmsSenderNumber = dto.SmsSenderNumber;
        // Per-group rate limits (null = inherit global)
        target.DelayBetweenMessagesMs = dto.DelayBetweenMessagesMs;
        target.MaxMessagesPerMinute = dto.MaxMessagesPerMinute;
        // === Day 7 G2 webhook secrets — null = no change, empty string = clear ===
        if (dto.SendGridWebhookSecret is not null) target.SendGridWebhookSecret = string.IsNullOrEmpty(dto.SendGridWebhookSecret) ? null : dto.SendGridWebhookSecret;
        if (dto.BrevoWebhookSecret is not null) target.BrevoWebhookSecret = string.IsNullOrEmpty(dto.BrevoWebhookSecret) ? null : dto.BrevoWebhookSecret;
        if (dto.MailgunWebhookSecret is not null) target.MailgunWebhookSecret = string.IsNullOrEmpty(dto.MailgunWebhookSecret) ? null : dto.MailgunWebhookSecret;
        // === Day 7 G3 IMAP polling ===
        target.EnableInboxPolling = dto.EnableInboxPolling;
        target.ImapHost = string.IsNullOrWhiteSpace(dto.ImapHost) ? null : dto.ImapHost;
        if (dto.ImapPort > 0) target.ImapPort = dto.ImapPort;
        target.ImapEnableSsl = dto.ImapEnableSsl;
        target.ImapUsername = string.IsNullOrWhiteSpace(dto.ImapUsername) ? null : dto.ImapUsername;
        // Password: null = no change, empty = clear
        if (dto.ImapPassword is not null) target.ImapPassword = string.IsNullOrEmpty(dto.ImapPassword) ? null : dto.ImapPassword;
        if (!string.IsNullOrWhiteSpace(dto.ImapFolder)) target.ImapFolder = dto.ImapFolder;
        target.InboxPollingIntervalMinutes = dto.InboxPollingIntervalMinutes;
        target.DefaultInboxOwnerUserId = dto.DefaultInboxOwnerUserId;
        return target;
    }

    private async Task UnsetExistingDefaultAsync(CancellationToken ct)
    {
        var current = (await _groupRepo.FindAsync(g => g.IsDefault, ct)).ToList();
        foreach (var g in current)
        {
            g.IsDefault = false;
            await _groupRepo.UpdateAsync(g, ct);
        }
    }

    /// <summary>
    /// Adapter — converts SmtpGroup into the legacy UserSmtpSettings shape so existing
    /// IEmailService.SendWithUserSettingsAsync (and SMS/WhatsApp services) can be reused unchanged.
    /// </summary>
    public static UserSmtpSettings ToUserSmtpSettings(SmtpGroup g) => new()
    {
        EmailProvider = g.EmailProvider,
        SmtpHost = g.SmtpHost,
        SmtpPort = g.SmtpPort,
        SmtpUsername = g.SmtpUsername,
        SmtpPassword = g.SmtpPassword,
        SmtpEnableSsl = g.SmtpEnableSsl,
        SmtpTimeout = g.SmtpTimeout,
        SmtpFromEmail = g.FromEmail,
        SmtpFromName = g.FromName,
        SendGridApiKey = g.SendGridApiKey,
        BrevoApiKey = g.BrevoApiKey,
        MailgunApiKey = g.MailgunApiKey,
        MailgunDomain = g.MailgunDomain,
        WhatsAppApiKey = g.WhatsAppApiKey,
        WhatsAppPhoneNumberId = g.WhatsAppPhoneNumberId,
        WhatsAppBusinessAccountId = g.WhatsAppBusinessAccountId,
        SmsApiKey = g.SmsApiKey,
        SmsApiSecret = g.SmsApiSecret,
        SmsSenderNumber = g.SmsSenderNumber,
        SignatureDesignation = g.SignatureDesignation,
        SignaturePhone = g.SignaturePhone,
        CompanyWebsite = g.CompanyWebsite,
        SignatureImageUrl = g.SignatureImageUrl,
    };
}
