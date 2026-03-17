using AutoMapper;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace MarketingApp.Application.Services;

public class NotificationService : INotificationService
{
    private readonly IGenericRepository<Notification> _notificationRepo;
    private readonly IMapper _mapper;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IGenericRepository<Notification> notificationRepo,
        IMapper mapper,
        ILogger<NotificationService> logger)
    {
        _notificationRepo = notificationRepo;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<List<NotificationDto>> GetNotificationsAsync(Guid userId, int count = 50)
    {
        var notifications = await _notificationRepo.FindAsync(n => n.UserId == userId);
        return notifications
            .OrderByDescending(n => n.CreatedAt)
            .Take(count)
            .Select(n => _mapper.Map<NotificationDto>(n))
            .ToList();
    }

    public async Task<NotificationCountDto> GetUnreadCountAsync(Guid userId)
    {
        var unread = await _notificationRepo.FindAsync(n => n.UserId == userId && !n.IsRead);
        var list = unread.ToList();

        return new NotificationCountDto
        {
            TotalUnread = list.Count,
            InfoCount = list.Count(n => n.Type == "info"),
            SuccessCount = list.Count(n => n.Type == "success"),
            WarningCount = list.Count(n => n.Type == "warning"),
            ErrorCount = list.Count(n => n.Type == "error")
        };
    }

    public async Task MarkAsReadAsync(Guid userId, Guid notificationId)
    {
        var notification = await _notificationRepo.GetByIdAsync(notificationId);
        if (notification is not null && notification.UserId == userId)
        {
            notification.IsRead = true;
            await _notificationRepo.UpdateAsync(notification);
        }
    }

    public async Task MarkAllAsReadAsync(Guid userId)
    {
        var unread = await _notificationRepo.FindAsync(n => n.UserId == userId && !n.IsRead);
        foreach (var n in unread)
        {
            n.IsRead = true;
            await _notificationRepo.UpdateAsync(n);
        }
    }

    public async Task CreateNotificationAsync(Guid userId, string title, string message, string type = "info", string? relatedEntity = null, Guid? relatedEntityId = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            RelatedEntity = relatedEntity,
            RelatedEntityId = relatedEntityId
        };

        await _notificationRepo.AddAsync(notification);
        _logger.LogInformation("Notification created for user {UserId}: {Title}", userId, title);
    }

    public async Task SendCampaignCompletionNotificationAsync(Guid userId, string campaignName, int sentCount, int failedCount, string channel)
    {
        var totalCount = sentCount + failedCount;
        var successRate = totalCount > 0 ? (sentCount * 100.0 / totalCount).ToString("F1") : "0";

        var type = failedCount == 0 ? "success" : failedCount > sentCount ? "error" : "warning";
        var title = $"Campaign \"{campaignName}\" Completed";
        var message = $"Channel: {channel.ToUpper()} | Total: {totalCount} | Sent: {sentCount} | Failed: {failedCount} | Success Rate: {successRate}%";

        await CreateNotificationAsync(userId, title, message, type, "campaign", null);

        if (failedCount > 0)
        {
            await CreateNotificationAsync(
                userId,
                $"{failedCount} Messages Failed",
                $"{failedCount} messages failed to deliver in campaign \"{campaignName}\". Check the campaign report for details.",
                "error",
                "campaign",
                null);
        }
    }
}
