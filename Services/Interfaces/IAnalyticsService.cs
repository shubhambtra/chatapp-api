using ChatApp.API.Models.DTOs;

namespace ChatApp.API.Services.Interfaces;

public interface IAnalyticsService
{
    Task<DashboardStatsDto> GetDashboardStatsAsync(string siteId, AnalyticsRequest request);
    Task<ConversationAnalyticsDto> GetConversationAnalyticsAsync(string siteId, AnalyticsRequest request);
    Task<List<AgentPerformanceDto>> GetAgentPerformanceAsync(string siteId, AnalyticsRequest request);
    Task<VisitorAnalyticsDto> GetVisitorAnalyticsAsync(string siteId, AnalyticsRequest request);
    Task<ResponseTimeAnalyticsDto> GetResponseTimeAnalyticsAsync(string siteId, AnalyticsRequest request);
}
