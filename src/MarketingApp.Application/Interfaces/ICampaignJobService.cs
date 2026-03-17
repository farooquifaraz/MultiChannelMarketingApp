namespace MarketingApp.Application.Interfaces;

public interface ICampaignJobService
{
    Task ProcessCampaignAsync(Guid campaignId);
}
