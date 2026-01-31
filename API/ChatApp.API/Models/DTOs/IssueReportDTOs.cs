namespace ChatApp.API.Models.DTOs;

public record IssueReportDto(
    string Id,
    string Name,
    string Email,
    string? UserId,
    string? SiteId,
    string Title,
    string Category,
    string Priority,
    string Description,
    string Status,
    bool IsRead,
    DateTime? ReadAt,
    string? AdminNotes,
    string? IpAddress,
    string? UserAgent,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<IssueReportAttachmentDto>? Attachments
);

public record IssueReportAttachmentDto(
    string Id,
    string IssueReportId,
    string OriginalName,
    string StoredName,
    string MimeType,
    long FileSize,
    string FilePath,
    DateTime CreatedAt
);

public record CreateIssueReportRequest(
    string Name,
    string Email,
    string? UserId,
    string? SiteId,
    string Title,
    string? Category,
    string? Priority,
    string Description
);

public record UpdateIssueStatusRequest(
    string Status
);

public record UpdateIssueNotesRequest(
    string? AdminNotes
);
