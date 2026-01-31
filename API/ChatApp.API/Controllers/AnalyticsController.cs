using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Services.Interfaces;

namespace ChatApp.API.Controllers;

[ApiController]
[Route("api/sites/{siteId}/[controller]")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;

    public AnalyticsController(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<DashboardStatsDto>>> GetDashboardStats(
        string siteId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? groupBy = null)
    {
        var request = new AnalyticsRequest(from, to, groupBy);
        var stats = await _analyticsService.GetDashboardStatsAsync(siteId, request);
        return Ok(ApiResponse<DashboardStatsDto>.Ok(stats));
    }

    [HttpGet("conversations")]
    public async Task<ActionResult<ApiResponse<ConversationAnalyticsDto>>> GetConversationAnalytics(
        string siteId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? groupBy = null)
    {
        var request = new AnalyticsRequest(from, to, groupBy);
        var analytics = await _analyticsService.GetConversationAnalyticsAsync(siteId, request);
        return Ok(ApiResponse<ConversationAnalyticsDto>.Ok(analytics));
    }

    [HttpGet("agents")]
    public async Task<ActionResult<ApiResponse<List<AgentPerformanceDto>>>> GetAgentPerformance(
        string siteId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? groupBy = null)
    {
        var request = new AnalyticsRequest(from, to, groupBy);
        var performance = await _analyticsService.GetAgentPerformanceAsync(siteId, request);
        return Ok(ApiResponse<List<AgentPerformanceDto>>.Ok(performance));
    }

    [HttpGet("visitors")]
    public async Task<ActionResult<ApiResponse<VisitorAnalyticsDto>>> GetVisitorAnalytics(
        string siteId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? groupBy = null)
    {
        var request = new AnalyticsRequest(from, to, groupBy);
        var analytics = await _analyticsService.GetVisitorAnalyticsAsync(siteId, request);
        return Ok(ApiResponse<VisitorAnalyticsDto>.Ok(analytics));
    }

    [HttpGet("response-times")]
    public async Task<ActionResult<ApiResponse<ResponseTimeAnalyticsDto>>> GetResponseTimeAnalytics(
        string siteId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? groupBy = null)
    {
        var request = new AnalyticsRequest(from, to, groupBy);
        var analytics = await _analyticsService.GetResponseTimeAnalyticsAsync(siteId, request);
        return Ok(ApiResponse<ResponseTimeAnalyticsDto>.Ok(analytics));
    }
}
