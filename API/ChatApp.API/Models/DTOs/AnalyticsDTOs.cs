namespace ChatApp.API.Models.DTOs;

public record AnalyticsRequest(
    DateTime? From,
    DateTime? To,
    string? GroupBy // hour, day, week, month
);

public record DashboardStatsDto(
    int TotalConversations,
    int ActiveConversations,
    int TotalMessages,
    int TotalVisitors,
    int OnlineVisitors,
    int TotalAgents,
    int OnlineAgents,
    double AverageResponseTime,
    double AverageRating,
    ConversationTrendDto? ConversationTrend,
    MessageTrendDto? MessageTrend
);

public record ConversationTrendDto(
    List<TrendDataPoint> Data,
    double ChangePercentage
);

public record MessageTrendDto(
    List<TrendDataPoint> Data,
    double ChangePercentage
);

public record TrendDataPoint(
    DateTime Date,
    int Count
);

public record ConversationAnalyticsDto(
    int Total,
    int Active,
    int Closed,
    int Resolved,
    double AverageMessagesPerConversation,
    double AverageTimeToFirstResponse,
    double AverageResolutionTime,
    double AverageRating,
    Dictionary<string, int>? ByStatus,
    Dictionary<string, int>? ByPriority,
    Dictionary<string, int>? ByChannel,
    List<TrendDataPoint>? Trend
);

public record AgentPerformanceDto(
    string UserId,
    string Username,
    string? FullName,
    int TotalConversations,
    int ResolvedConversations,
    int TotalMessages,
    double AverageResponseTime,
    double AverageRating,
    double ResolutionRate,
    TimeSpan TotalOnlineTime
);

public record VisitorAnalyticsDto(
    int TotalVisitors,
    int NewVisitors,
    int ReturningVisitors,
    int OnlineVisitors,
    Dictionary<string, int>? ByCountry,
    Dictionary<string, int>? ByBrowser,
    Dictionary<string, int>? ByDevice,
    List<TrendDataPoint>? Trend
);

public record ResponseTimeAnalyticsDto(
    double AverageFirstResponseTime,
    double AverageResponseTime,
    double MedianResponseTime,
    Dictionary<string, double>? ByAgent,
    Dictionary<string, double>? ByHour,
    List<ResponseTimeTrendPoint>? Trend
);

public record ResponseTimeTrendPoint(
    DateTime Date,
    double AverageTime
);
