using System.Security.Claims;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketingApp.API.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<NotificationDto>>>> GetNotifications([FromQuery] int count = 50)
    {
        var notifications = await _notificationService.GetNotificationsAsync(GetUserId(), count);
        return Ok(ApiResponse<List<NotificationDto>>.Ok(notifications));
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<ApiResponse<NotificationCountDto>>> GetUnreadCount()
    {
        var count = await _notificationService.GetUnreadCountAsync(GetUserId());
        return Ok(ApiResponse<NotificationCountDto>.Ok(count));
    }

    [HttpPut("{id}/read")]
    public async Task<ActionResult<ApiResponse<bool>>> MarkAsRead(Guid id)
    {
        await _notificationService.MarkAsReadAsync(GetUserId(), id);
        return Ok(ApiResponse<bool>.Ok(true, "Notification marked as read"));
    }

    [HttpPut("read-all")]
    public async Task<ActionResult<ApiResponse<bool>>> MarkAllAsRead()
    {
        await _notificationService.MarkAllAsReadAsync(GetUserId());
        return Ok(ApiResponse<bool>.Ok(true, "All notifications marked as read"));
    }
}
