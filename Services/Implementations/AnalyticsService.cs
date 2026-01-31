using Microsoft.EntityFrameworkCore;
using ChatApp.API.Data;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Services.Interfaces;

namespace ChatApp.API.Services.Implementations;

public class AnalyticsService : IAnalyticsService
{
    private readonly ApplicationDbContext _context;

    public AnalyticsService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardStatsDto> GetDashboardStatsAsync(string siteId, AnalyticsRequest request)
    {
        var from = request.From ?? DateTime.UtcNow.AddDays(-30);
        var to = request.To ?? DateTime.UtcNow;

        var totalConversations = await _context.Conversations
            .CountAsync(c => c.SiteId == siteId && c.CreatedAt >= from && c.CreatedAt <= to);

        var activeConversations = await _context.Conversations
            .CountAsync(c => c.SiteId == siteId && c.Status == "active");

        var totalMessages = await _context.Messages
            .CountAsync(m => m.Conversation.SiteId == siteId && m.CreatedAt >= from && m.CreatedAt <= to);

        var totalVisitors = await _context.Visitors
            .CountAsync(v => v.SiteId == siteId && v.CreatedAt >= from && v.CreatedAt <= to);

        var onlineVisitors = await _context.Visitors
            .CountAsync(v => v.SiteId == siteId && v.IsOnline);

        var totalAgents = await _context.UserSites
            .CountAsync(us => us.SiteId == siteId);

        var onlineAgents = await _context.UserSites
            .CountAsync(us => us.SiteId == siteId && us.User.Status == "online");

        // Calculate average response time (fetch and compute in memory)
        var conversationsWithResponse = await _context.Conversations
            .Where(c => c.SiteId == siteId && c.FirstResponseAt != null)
            .Select(c => new { c.CreatedAt, c.FirstResponseAt })
            .ToListAsync();

        var avgResponseTime = conversationsWithResponse.Any()
            ? conversationsWithResponse.Average(c => (c.FirstResponseAt!.Value - c.CreatedAt).TotalSeconds)
            : 0;

        // Calculate average rating (fetch and compute in memory)
        var conversationsWithRating = await _context.Conversations
            .Where(c => c.SiteId == siteId && c.Rating != null)
            .Select(c => c.Rating!.Value)
            .ToListAsync();

        var avgRating = conversationsWithRating.Any()
            ? conversationsWithRating.Average(r => (double)r)
            : 0;

        // Get conversation trend
        var conversationTrend = await GetTrendDataAsync(
            _context.Conversations.Where(c => c.SiteId == siteId),
            c => c.CreatedAt,
            from, to, request.GroupBy);

        // Get message trend
        var messageTrend = await GetTrendDataAsync(
            _context.Messages.Where(m => m.Conversation.SiteId == siteId),
            m => m.CreatedAt,
            from, to, request.GroupBy);

        return new DashboardStatsDto(
            totalConversations,
            activeConversations,
            totalMessages,
            totalVisitors,
            onlineVisitors,
            totalAgents,
            onlineAgents,
            avgResponseTime,
            avgRating,
            conversationTrend,
           null
        );
    }

    public async Task<ConversationAnalyticsDto> GetConversationAnalyticsAsync(string siteId, AnalyticsRequest request)
    {
        var from = request.From ?? DateTime.UtcNow.AddDays(-30);
        var to = request.To ?? DateTime.UtcNow;

        var conversations = await _context.Conversations
            .Where(c => c.SiteId == siteId && c.CreatedAt >= from && c.CreatedAt <= to)
            .ToListAsync();

        var total = conversations.Count;
        var active = conversations.Count(c => c.Status == "active");
        var closed = conversations.Count(c => c.Status == "closed");
        var resolved = conversations.Count(c => c.Status == "resolved");

        var avgMessages = conversations.Any() ? conversations.Average(c => c.MessageCount) : 0;

        var avgFirstResponse = conversations
            .Where(c => c.FirstResponseAt != null)
            .Select(c => (c.FirstResponseAt!.Value - c.CreatedAt).TotalSeconds)
            .DefaultIfEmpty(0)
            .Average();

        var avgResolution = conversations
            .Where(c => c.ResolvedAt != null)
            .Select(c => (c.ResolvedAt!.Value - c.CreatedAt).TotalSeconds)
            .DefaultIfEmpty(0)
            .Average();

        var avgRating = conversations
            .Where(c => c.Rating != null)
            .Select(c => (double)c.Rating!.Value)
            .DefaultIfEmpty(0)
            .Average();

        var byStatus = conversations
            .GroupBy(c => c.Status)
            .ToDictionary(g => g.Key, g => g.Count());

        var byPriority = conversations
            .GroupBy(c => c.Priority)
            .ToDictionary(g => g.Key, g => g.Count());

        var byChannel = conversations
            .GroupBy(c => c.Channel)
            .ToDictionary(g => g.Key, g => g.Count());

        var trend = await GetTrendDataAsync(
            _context.Conversations.Where(c => c.SiteId == siteId),
            c => c.CreatedAt,
            from, to, request.GroupBy);

        return new ConversationAnalyticsDto(
            total,
            active,
            closed,
            resolved,
            avgMessages,
            avgFirstResponse,
            avgResolution,
            avgRating,
            byStatus,
            byPriority,
            byChannel,
            trend?.Data
        );
    }

    public async Task<List<AgentPerformanceDto>> GetAgentPerformanceAsync(string siteId, AnalyticsRequest request)
    {
        var from = request.From ?? DateTime.UtcNow.AddDays(-30);
        var to = request.To ?? DateTime.UtcNow;

        var agents = await _context.UserSites
            .Include(us => us.User)
            .Where(us => us.SiteId == siteId)
            .ToListAsync();

        var result = new List<AgentPerformanceDto>();

        foreach (var agent in agents)
        {
            var conversations = await _context.Conversations
                .Where(c => c.SiteId == siteId &&
                           c.AssignedUserId == agent.UserId &&
                           c.CreatedAt >= from && c.CreatedAt <= to)
                .ToListAsync();

            var totalConv = conversations.Count;
            var resolvedConv = conversations.Count(c => c.Status == "resolved" || c.Status == "closed");

            var messages = await _context.Messages
                .CountAsync(m => m.Conversation.SiteId == siteId &&
                                m.SenderType == "agent" &&
                                m.SenderId == agent.UserId &&
                                m.CreatedAt >= from && m.CreatedAt <= to);

            var avgResponseTime = conversations
                .Where(c => c.FirstResponseAt != null)
                .Select(c => (c.FirstResponseAt!.Value - c.CreatedAt).TotalSeconds)
                .DefaultIfEmpty(0)
                .Average();

            var avgRating = conversations
                .Where(c => c.Rating != null)
                .Select(c => (double)c.Rating!.Value)
                .DefaultIfEmpty(0)
                .Average();

            var resolutionRate = totalConv > 0 ? (double)resolvedConv / totalConv * 100 : 0;

            // Calculate online time from agent sessions
            var sessions = await _context.AgentSessions
                .Where(s => s.UserId == agent.UserId &&
                           s.SiteId == siteId &&
                           s.ConnectedAt >= from && s.ConnectedAt <= to)
                .ToListAsync();

            var totalOnlineTime = TimeSpan.FromSeconds(sessions.Sum(s =>
                ((s.DisconnectedAt ?? DateTime.UtcNow) - s.ConnectedAt).TotalSeconds));

            result.Add(new AgentPerformanceDto(
                agent.UserId,
                agent.User.Username,
                agent.User.FullName,
                totalConv,
                resolvedConv,
                messages,
                avgResponseTime,
                avgRating,
                resolutionRate,
                totalOnlineTime
            ));
        }

        return result.OrderByDescending(a => a.TotalConversations).ToList();
    }

    public async Task<VisitorAnalyticsDto> GetVisitorAnalyticsAsync(string siteId, AnalyticsRequest request)
    {
        var from = request.From ?? DateTime.UtcNow.AddDays(-30);
        var to = request.To ?? DateTime.UtcNow;
        var previousFrom = from.AddDays(-(to - from).TotalDays);

        var visitors = await _context.Visitors
            .Where(v => v.SiteId == siteId && v.CreatedAt >= from && v.CreatedAt <= to)
            .ToListAsync();

        var totalVisitors = visitors.Count;
        var newVisitors = visitors.Count(v => v.TotalVisits == 1);
        var returningVisitors = visitors.Count(v => v.TotalVisits > 1);
        var onlineVisitors = await _context.Visitors.CountAsync(v => v.SiteId == siteId && v.IsOnline);

        var byCountry = visitors
            .Where(v => !string.IsNullOrEmpty(v.Country))
            .GroupBy(v => v.Country!)
            .ToDictionary(g => g.Key, g => g.Count());

        var byBrowser = visitors
            .Where(v => !string.IsNullOrEmpty(v.Browser))
            .GroupBy(v => v.Browser!)
            .ToDictionary(g => g.Key, g => g.Count());

        var byDevice = visitors
            .Where(v => !string.IsNullOrEmpty(v.DeviceType))
            .GroupBy(v => v.DeviceType!)
            .ToDictionary(g => g.Key, g => g.Count());

        var trend = await GetTrendDataAsync(
            _context.Visitors.Where(v => v.SiteId == siteId),
            v => v.CreatedAt,
            from, to, request.GroupBy);

        return new VisitorAnalyticsDto(
            totalVisitors,
            newVisitors,
            returningVisitors,
            onlineVisitors,
            byCountry,
            byBrowser,
            byDevice,
            trend?.Data
        );
    }

    public async Task<ResponseTimeAnalyticsDto> GetResponseTimeAnalyticsAsync(string siteId, AnalyticsRequest request)
    {
        var from = request.From ?? DateTime.UtcNow.AddDays(-30);
        var to = request.To ?? DateTime.UtcNow;

        var conversations = await _context.Conversations
            .Include(c => c.AssignedUser)
            .Where(c => c.SiteId == siteId &&
                       c.FirstResponseAt != null &&
                       c.CreatedAt >= from && c.CreatedAt <= to)
            .ToListAsync();

        var responseTimes = conversations
            .Select(c => (c.FirstResponseAt!.Value - c.CreatedAt).TotalSeconds)
            .OrderBy(t => t)
            .ToList();

        var avgFirstResponseTime = responseTimes.Any() ? responseTimes.Average() : 0;
        var avgResponseTime = avgFirstResponseTime; // Simplified
        var medianResponseTime = responseTimes.Any()
            ? responseTimes[responseTimes.Count / 2]
            : 0;

        var byAgent = conversations
            .Where(c => c.AssignedUser != null)
            .GroupBy(c => c.AssignedUser!.Username)
            .ToDictionary(
                g => g.Key,
                g => g.Average(c => (c.FirstResponseAt!.Value - c.CreatedAt).TotalSeconds)
            );

        var byHour = conversations
            .GroupBy(c => c.CreatedAt.Hour.ToString("00"))
            .ToDictionary(
                g => g.Key,
                g => g.Average(c => (c.FirstResponseAt!.Value - c.CreatedAt).TotalSeconds)
            );

        return new ResponseTimeAnalyticsDto(
            avgFirstResponseTime,
            avgResponseTime,
            medianResponseTime,
            byAgent,
            byHour,
            null // Trend calculation would be more complex
        );
    }

    private async Task<ConversationTrendDto?> GetTrendDataAsync<T>(
        IQueryable<T> query,
        Func<T, DateTime> dateSelector,
        DateTime from, DateTime to, string? groupBy) where T : class
    {
        var items = await query.ToListAsync();

        var filteredItems = items
            .Where(i => dateSelector(i) >= from && dateSelector(i) <= to)
            .ToList();

        if (!filteredItems.Any())
        {
            return new ConversationTrendDto(new List<TrendDataPoint>(), 0);
        }

        var grouped = groupBy switch
        {
            "hour" => filteredItems.GroupBy(i => new DateTime(
                dateSelector(i).Year, dateSelector(i).Month, dateSelector(i).Day,
                dateSelector(i).Hour, 0, 0)),
            "week" => filteredItems.GroupBy(i => dateSelector(i).Date.AddDays(-(int)dateSelector(i).DayOfWeek)),
            "month" => filteredItems.GroupBy(i => new DateTime(dateSelector(i).Year, dateSelector(i).Month, 1)),
            _ => filteredItems.GroupBy(i => dateSelector(i).Date)
        };

        var data = grouped
            .Select(g => new TrendDataPoint(g.Key, g.Count()))
            .OrderBy(d => d.Date)
            .ToList();

        // Calculate change percentage (current period vs previous)
        var midPoint = from.AddTicks((to - from).Ticks / 2);
        var firstHalf = filteredItems.Count(i => dateSelector(i) < midPoint);
        var secondHalf = filteredItems.Count(i => dateSelector(i) >= midPoint);
        var changePercentage = firstHalf > 0
            ? ((double)secondHalf - firstHalf) / firstHalf * 100
            : secondHalf > 0 ? 100 : 0;

        return new ConversationTrendDto(data, changePercentage);
    }
}
