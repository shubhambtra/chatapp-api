using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ChatApp.API.Data;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Models.Entities;
using ChatApp.API.Services.Interfaces;

namespace ChatApp.API.Services.Implementations;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;

    public NotificationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<NotificationDto> CreateNotificationAsync(
        string userId, string type, string title, string? message,
        string? siteId = null, string? conversationId = null, string? actionUrl = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            SiteId = siteId,
            ConversationId = conversationId,
            ActionUrl = actionUrl
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        return MapToDto(notification);
    }

    public async Task<NotificationDto?> GetNotificationAsync(string notificationId)
    {
        var notification = await _context.Notifications.FindAsync(notificationId);
        return notification != null ? MapToDto(notification) : null;
    }

    public async Task<PagedResponse<NotificationDto>> GetNotificationsAsync(string userId, NotificationListRequest request)
    {
        var query = _context.Notifications.Where(n => n.UserId == userId);

        if (request.IsRead.HasValue)
        {
            query = query.Where(n => n.IsRead == request.IsRead.Value);
        }

        var totalItems = await query.CountAsync();
        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResponse<NotificationDto>(
            notifications.Select(MapToDto).ToList(),
            request.Page,
            request.PageSize,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)request.PageSize)
        );
    }

    public async Task MarkNotificationsReadAsync(string userId, MarkNotificationReadRequest request)
    {
        var notifications = await _context.Notifications
            .Where(n => n.UserId == userId && request.NotificationIds.Contains(n.Id))
            .ToListAsync();

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    public async Task MarkAllNotificationsReadAsync(string userId)
    {
        var notifications = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    public async Task DeleteNotificationAsync(string notificationId)
    {
        var notification = await _context.Notifications.FindAsync(notificationId);
        if (notification == null) throw new KeyNotFoundException("Notification not found");

        _context.Notifications.Remove(notification);
        await _context.SaveChangesAsync();
    }

    public async Task<NotificationCountDto> GetNotificationCountAsync(string userId)
    {
        var total = await _context.Notifications.CountAsync(n => n.UserId == userId);
        var unread = await _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

        return new NotificationCountDto(total, unread);
    }

    private static NotificationDto MapToDto(Notification notification)
    {
        Dictionary<string, object>? data = null;
        try
        {
            if (!string.IsNullOrEmpty(notification.Data))
                data = JsonSerializer.Deserialize<Dictionary<string, object>>(notification.Data);
        }
        catch { }

        return new NotificationDto(
            notification.Id,
            notification.Type,
            notification.Title,
            notification.Message,
            notification.ActionUrl,
            data,
            notification.ConversationId,
            notification.IsRead,
            notification.ReadAt,
            notification.CreatedAt
        );
    }
}
