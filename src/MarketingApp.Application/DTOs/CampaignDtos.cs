namespace MarketingApp.Application.DTOs;

public class CampaignDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? TemplateName { get; set; }
    public string? GroupName { get; set; }
    public int TotalContacts { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // Sender info (populated for admin view + campaign detail)
    public Guid OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public string? OwnerEmail { get; set; }
    public string? SmtpGroupName { get; set; }
}

public class CampaignDetailDto : CampaignDto
{
    public Guid TemplateId { get; set; }
    public Guid GroupId { get; set; }
    public DateTime? StartedAt { get; set; }
}

public class CreateCampaignDto
{
    public string Name { get; set; } = string.Empty;
    public Guid TemplateId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public Guid GroupId { get; set; }
    public DateTime? ScheduledAt { get; set; }
}

public class UpdateCampaignDto
{
    public string Name { get; set; } = string.Empty;
    public Guid TemplateId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public Guid GroupId { get; set; }
}

public class SendCampaignDto
{
    public DateTime? ScheduledAt { get; set; }
}

public class CampaignReportDto
{
    public Guid CampaignId { get; set; }
    public string CampaignName { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TotalContacts { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public int DeliveredCount { get; set; }
    public int OpenedCount { get; set; }
    public int ClickedCount { get; set; }
    public int TotalClicks { get; set; }
    public int BouncedCount { get; set; }
    public double SendRate => TotalContacts > 0 ? Math.Round((double)SentCount / TotalContacts * 100, 2) : 0;
    public double FailRate => TotalContacts > 0 ? Math.Round((double)FailedCount / TotalContacts * 100, 2) : 0;
    public double OpenRate => SentCount > 0 ? Math.Round((double)OpenedCount / SentCount * 100, 2) : 0;
    public double ClickRate => OpenedCount > 0 ? Math.Round((double)ClickedCount / OpenedCount * 100, 2) : 0;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class CampaignMessageDto
{
    public Guid Id { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? OpenedAt { get; set; }
}
