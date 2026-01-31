using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Services.Interfaces;

namespace ChatApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResponse<NotificationDto>>>> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? isRead = null)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(ApiResponse<PagedResponse<NotificationDto>>.Fail("User not found"));
        }

        var request = new NotificationListRequest(page, pageSize, isRead);
        var result = await _notificationService.GetNotificationsAsync(userId, request);
        return Ok(ApiResponse<PagedResponse<NotificationDto>>.Ok(result));
    }

    [HttpGet("{notificationId}")]
    public async Task<ActionResult<ApiResponse<NotificationDto>>> GetNotification(string notificationId)
    {
        var notification = await _notificationService.GetNotificationAsync(notificationId);
        if (notification == null)
        {
            return NotFound(ApiResponse<NotificationDto>.Fail("Notification not found"));
        }

        return Ok(ApiResponse<NotificationDto>.Ok(notification));
    }

    [HttpGet("count")]
    public async Task<ActionResult<ApiResponse<NotificationCountDto>>> GetNotificationCount()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(ApiResponse<NotificationCountDto>.Fail("User not found"));
        }

        var count = await _notificationService.GetNotificationCountAsync(userId);
        return Ok(ApiResponse<NotificationCountDto>.Ok(count));
    }

    [HttpPost("read")]
    public async Task<ActionResult<ApiResponse<object>>> MarkNotificationsRead([FromBody] MarkNotificationReadRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(ApiResponse<object>.Fail("User not found"));
        }

        await _notificationService.MarkNotificationsReadAsync(userId, request);
        return Ok(ApiResponse<object>.Ok(null, "Notifications marked as read"));
    }

    [HttpPost("read-all")]
    public async Task<ActionResult<ApiResponse<object>>> MarkAllNotificationsRead()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(ApiResponse<object>.Fail("User not found"));
        }

        await _notificationService.MarkAllNotificationsReadAsync(userId);
        return Ok(ApiResponse<object>.Ok(null, "All notifications marked as read"));
    }

    [HttpDelete("{notificationId}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteNotification(string notificationId)
    {
        try
        {
            await _notificationService.DeleteNotificationAsync(notificationId);
            return Ok(ApiResponse<object>.Ok(null, "Notification deleted"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
