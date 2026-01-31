namespace ChatApp.API.Models.DTOs;

public record VisitorDto(
    string Id,
    string SiteId,
    string? ExternalId,
    string? Email,
    string? Name,
    string? Phone,
    string? AvatarUrl,
    string? Browser,
    string? Os,
    string? DeviceType,
    string? Country,
    string? City,
    string? CurrentPage,
    int PageViews,
    int TotalVisits,
    List<string>? Tags,
    Dictionary<string, object>? CustomData,
    DateTime? LastSeenAt,
    bool IsOnline,
    bool IsBlocked,
    DateTime CreatedAt
);

public record CreateVisitorRequest(
    string SiteId,
    string? ExternalId,
    string? Email,
    string? Name,
    string? Phone,
    string? UserAgent,
    string? IpAddress,
    string? ReferrerUrl,
    string? LandingPage,
    Dictionary<string, object>? CustomData
);

public record UpdateVisitorRequest(
    string? Email,
    string? Name,
    string? Phone,
    string? ExternalId,
    List<string>? Tags,
    Dictionary<string, object>? CustomData
);

public record VisitorSessionDto(
    string Id,
    string VisitorId,
    string? CurrentPage,
    string? ReferrerUrl,
    bool IsActive,
    DateTime StartedAt,
    DateTime LastActivityAt,
    DateTime? EndedAt,
    int PageViews
);

public record VisitorListRequest(
    int Page = 1,
    int PageSize = 20,
    string? Status="",
    string? Search =""     ,
    DateTime? From = null,
    DateTime? To=null
);

// Active visitors response per API documentation
public record ActiveVisitorDto(
    string VisitorId,
    string? Name,
    string Status,
    string? CurrentPage,
    int UnreadCount,
    DateTime? LastMessageAt
);

public record ActiveVisitorsResponse(
    List<ActiveVisitorDto> Visitors,
    int Total
);

// Register visitor response per API documentation
public record RegisterVisitorResponse(
    string Id,
    string VisitorId,
    string? Name,
    string SessionToken,
    DateTime CreatedAt
);
