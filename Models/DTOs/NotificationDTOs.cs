namespace ChatApp.API.Models.DTOs;

public record NotificationDto(
    string Id,
    string Type,
    string Title,
    string? Message,
    string? ActionUrl,
    Dictionary<string, object>? Data,
    string? ConversationId,
    bool IsRead,
    DateTime? ReadAt,
    DateTime CreatedAt
);

public record NotificationListRequest(
    int Page = 1,
    int PageSize = 20,
    bool? IsRead=   null
);

public record MarkNotificationReadRequest(List<string> NotificationIds);

public record NotificationCountDto(
    int Total,
    int Unread
);
