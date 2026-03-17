using MarketingApp.Application.DTOs;

namespace MarketingApp.Application.Interfaces;

public interface INotificationService
{
    Task<List<NotificationDto>> GetNotificationsAsync(Guid userId, int count = 50);
    Task<NotificationCountDto> GetUnreadCountAsync(Guid userId);
    Task MarkAsReadAsync(Guid userId, Guid notificationId);
    Task MarkAllAsReadAsync(Guid userId);
    Task CreateNotificationAsync(Guid userId, string title, string message, string type = "info", string? relatedEntity = null, Guid? relatedEntityId = null);
    Task SendCampaignCompletionNotificationAsync(Guid userId, string campaignName, int sentCount, int failedCount, string channel);
}
