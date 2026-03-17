namespace MarketingApp.Application.DTOs;

public class DashboardStatsDto
{
    public int TotalContacts { get; set; }
    public int TotalCampaigns { get; set; }
    public int TotalMessagesSent { get; set; }
    public int ActiveCampaigns { get; set; }
    public double OverallSuccessRate { get; set; }
}

public class ChannelBreakdownDto
{
    public ChannelStatsDto Email { get; set; } = new();
    public ChannelStatsDto WhatsApp { get; set; } = new();
    public ChannelStatsDto Sms { get; set; } = new();
}

public class ChannelStatsDto
{
    public int CampaignCount { get; set; }
    public int MessagesSent { get; set; }
    public int MessagesFailed { get; set; }
    public double SuccessRate => MessagesSent + MessagesFailed > 0
        ? Math.Round((double)MessagesSent / (MessagesSent + MessagesFailed) * 100, 2) : 0;
}
