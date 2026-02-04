namespace ChatApp.API.Models.DTOs;

public record ConversationDto(
    string Id,
    string SiteId,
    string VisitorId,
    VisitorDto? Visitor,
    string? AssignedUserId,
    UserDto? AssignedUser,
    string Status,
    string Priority,
    string Channel,
    string? Subject,
    List<string>? Tags,
    int MessageCount,
    DateTime? FirstResponseAt,
    DateTime? LastMessageAt,
    DateTime? ResolvedAt,
    DateTime? ClosedAt,
    int? Rating,
    string? Feedback,
    string? ResolutionStatus,
    string? ClosingNote,
    ConversationAnalysisDto? Analysis,
    DateTime CreatedAt
);

public class ConversationListDto
{
    public string Id { get; set; } = string.Empty;
    public string VisitorId { get; set; } = string.Empty;
    public string? VisitorName { get; set; }
    public string? VisitorEmail { get; set; }
    public string? AssignedUserId { get; set; }
    public string? AssignedUserName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string? LastMessagePreview { get; set; }
    public int MessageCount { get; set; }
    public int UnreadCount { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? SiteId { get; set; }
    public string? SiteName { get; set; }
    // Advanced filter fields
    public List<string>? Tags { get; set; }
    public int? Rating { get; set; }
    public string? RatingFeedback { get; set; }
    public string? ResolutionStatus { get; set; }
    // AI fields
    public string? Sentiment { get; set; }
    public double? SentimentScore { get; set; }
    public string? Intent { get; set; }
    public double? UrgencyScore { get; set; }
}

public record CreateConversationRequest(
    string SiteId,
    string VisitorId,
    string? Subject,
    string? InitialMessage,
    string Channel = "widget"
);

public record UpdateConversationRequest(
    string? Status,
    string? Priority,
    string? Subject,
    List<string>? Tags,
    string? AssignedUserId
);

public record AssignConversationRequest(string UserId);

public record CloseConversationRequest(
    int? Rating,
    string? Feedback,
    string? ResolutionStatus,  // resolved, unresolved, spam, no-response
    string? Note
);

public record SubmitCsatRequest(
    int Rating,
    string? Feedback
);

public record ConversationAnalysisDto(
    string? Summary,
    string? Sentiment,
    double? SentimentScore,
    List<string>? Topics,
    string? Intent,
    string? Language,
    double? UrgencyScore,
    List<string>? SuggestedResponses,
    List<string>? KeyPhrases,
    DateTime AnalyzedAt
);

public record ConversationListRequest(
    int Page = 1,
    int PageSize = 20,
    string? Status = "",
    string? Priority = "",
    string? AssignedUserId = "",
    string? VisitorId = "",
    DateTime? From = null,
    DateTime? To = null,
    string? Search = "",
    // Advanced filters
    string? Tags = null,
    int? RatingMin = null,
    int? RatingMax = null,
    string? ResolutionStatus = null,
    // AI filters
    string? Sentiment = null,
    string? Intent = null,
    double? UrgencyScoreMin = null,
    double? UrgencyScoreMax = null
);

// Conversation Comments
public record ConversationCommentDto(
    string Id,
    string ConversationId,
    string AuthorId,
    string? AuthorName,
    string Content,
    List<string>? Mentions,
    DateTime CreatedAt
);

public record CreateCommentRequest(
    string Content,
    List<string>? Mentions
);
