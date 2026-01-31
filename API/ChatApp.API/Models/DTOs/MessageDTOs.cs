namespace ChatApp.API.Models.DTOs;

public record MessageDto(
    string Id,
    string ConversationId,
    string SenderType,
    string? SenderId,
    string? SenderName,
    string? SenderAvatar,
    string Content,
    string MessageType,
    FileDto? File,
    Dictionary<string, object>? Metadata,
    bool IsEdited,
    DateTime? EditedAt,
    DateTime CreatedAt
);

public record SendMessageRequest(
    string ConversationId,
    string Content,
    string MessageType = "text",
    string? FileId = null,
    Dictionary<string, object>? Metadata = null
);

public record UpdateMessageRequest(string Content);

public record MessageListRequest(
    int Page = 1,
    int PageSize = 50,
    string? Before = "",
    string? After = ""
);

public record MarkMessagesReadRequest(List<string> MessageIds);

public record FileDto(
    string Id,
    string OriginalName,
    string MimeType,
    long FileSize,
    string Url,
    string? ThumbnailUrl,
    int? Width,
    int? Height,
    DateTime CreatedAt
);

public record UploadFileRequest(
    string SiteId,
    string? ConversationId
);

public record UploadFileResponse(
    string Id,
    string OriginalName,
    string MimeType,
    long FileSize,
    string Url,
    string? ThumbnailUrl
);
