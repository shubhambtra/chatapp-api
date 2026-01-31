using ChatApp.API.Models.DTOs;

namespace ChatApp.API.Services.Interfaces;

public interface INotificationService
{
    Task<NotificationDto> CreateNotificationAsync(string userId, string type, string title, string? message, string? siteId = null, string? conversationId = null, string? actionUrl = null);
    Task<NotificationDto?> GetNotificationAsync(string notificationId);
    Task<PagedResponse<NotificationDto>> GetNotificationsAsync(string userId, NotificationListRequest request);
    Task MarkNotificationsReadAsync(string userId, MarkNotificationReadRequest request);
    Task MarkAllNotificationsReadAsync(string userId);
    Task DeleteNotificationAsync(string notificationId);
    Task<NotificationCountDto> GetNotificationCountAsync(string userId);
}
