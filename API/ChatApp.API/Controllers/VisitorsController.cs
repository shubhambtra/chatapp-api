using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Services.Interfaces;

namespace ChatApp.API.Controllers;

[ApiController]
[Route("api/sites/{siteId}/[controller]")]
[Authorize]
public class VisitorsController : ControllerBase
{
    private readonly IVisitorService _visitorService;

    public VisitorsController(IVisitorService visitorService)
    {
        _visitorService = visitorService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResponse<VisitorDto>>>> GetVisitors(
        string siteId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var request = new VisitorListRequest(page, pageSize, status, search, from, to);
        var result = await _visitorService.GetVisitorsAsync(siteId, request);
        return Ok(ApiResponse<PagedResponse<VisitorDto>>.Ok(result));
    }

    [HttpGet("active")]
    public async Task<ActionResult<ApiResponse<ActiveVisitorsResponse>>> GetActiveVisitors(string siteId)
    {
        var result = await _visitorService.GetActiveVisitorsAsync(siteId);
        return Ok(ApiResponse<ActiveVisitorsResponse>.Ok(result));
    }

    [HttpGet("{visitorId}")]
    public async Task<ActionResult<ApiResponse<VisitorDto>>> GetVisitor(string siteId, string visitorId)
    {
        var visitor = await _visitorService.GetVisitorAsync(visitorId);
        if (visitor == null)
        {
            return NotFound(ApiResponse<VisitorDto>.Fail("Visitor not found"));
        }

        return Ok(ApiResponse<VisitorDto>.Ok(visitor));
    }

    [HttpGet("external/{externalId}")]
    public async Task<ActionResult<ApiResponse<VisitorDto>>> GetVisitorByExternalId(string siteId, string externalId)
    {
        var visitor = await _visitorService.GetVisitorByExternalIdAsync(siteId, externalId);
        if (visitor == null)
        {
            return NotFound(ApiResponse<VisitorDto>.Fail("Visitor not found"));
        }

        return Ok(ApiResponse<VisitorDto>.Ok(visitor));
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<VisitorDto>>> CreateVisitor(string siteId, [FromBody] CreateVisitorRequest request)
    {
        // Capture IP from server if not provided
        var ipAddress = request.IpAddress ?? GetClientIpAddress();
        var visitorRequest = request with { SiteId = siteId, IpAddress = ipAddress };
        var visitor = await _visitorService.CreateVisitorAsync(visitorRequest);
        return Ok(ApiResponse<VisitorDto>.Ok(visitor, "Visitor created"));
    }

    [HttpPut("{visitorId}")]
    public async Task<ActionResult<ApiResponse<VisitorDto>>> UpdateVisitor(
        string siteId,
        string visitorId,
        [FromBody] UpdateVisitorRequest request)
    {
        try
        {
            var visitor = await _visitorService.UpdateVisitorAsync(visitorId, request);
            return Ok(ApiResponse<VisitorDto>.Ok(visitor));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<VisitorDto>.Fail(ex.Message));
        }
    }

    [HttpPost("{visitorId}/block")]
    public async Task<ActionResult<ApiResponse<object>>> BlockVisitor(string siteId, string visitorId)
    {
        try
        {
            await _visitorService.BlockVisitorAsync(visitorId);
            return Ok(ApiResponse<object>.Ok(null, "Visitor blocked"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("{visitorId}/unblock")]
    public async Task<ActionResult<ApiResponse<object>>> UnblockVisitor(string siteId, string visitorId)
    {
        try
        {
            await _visitorService.UnblockVisitorAsync(visitorId);
            return Ok(ApiResponse<object>.Ok(null, "Visitor unblocked"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("{visitorId}/sessions")]
    public async Task<ActionResult<ApiResponse<List<VisitorSessionDto>>>> GetVisitorSessions(string siteId, string visitorId)
    {
        var sessions = await _visitorService.GetVisitorSessionsAsync(visitorId);
        return Ok(ApiResponse<List<VisitorSessionDto>>.Ok(sessions));
    }

    [HttpPost("{visitorId}/sessions")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<VisitorSessionDto>>> StartSession(
        string siteId,
        string visitorId,
        [FromBody] StartSessionRequest request)
    {
        try
        {
            // Capture IP from server if not provided
            var ipAddress = request.IpAddress ?? GetClientIpAddress();
            var session = await _visitorService.StartSessionAsync(
                visitorId,
                ipAddress,
                request.UserAgent,
                request.CurrentPage,
                request.ReferrerUrl);
            return Ok(ApiResponse<VisitorSessionDto>.Ok(session));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<VisitorSessionDto>.Fail(ex.Message));
        }
    }

    [HttpPut("{visitorId}/sessions/{sessionId}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> UpdateSession(
        string siteId,
        string visitorId,
        string sessionId,
        [FromBody] UpdateSessionRequest request)
    {
        try
        {
            await _visitorService.UpdateSessionActivityAsync(sessionId, request.CurrentPage);
            return Ok(ApiResponse<object>.Ok(null, "Session updated"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpDelete("{visitorId}/sessions/{sessionId}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> EndSession(string siteId, string visitorId, string sessionId)
    {
        try
        {
            await _visitorService.EndSessionAsync(sessionId);
            return Ok(ApiResponse<object>.Ok(null, "Session ended"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Gets the client IP address from the HTTP request, handling proxies and load balancers
    /// </summary>
    private string? GetClientIpAddress()
    {
        // Check for forwarded headers (when behind proxy/load balancer)
        var forwardedFor = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // X-Forwarded-For can contain multiple IPs, the first one is the original client
            return forwardedFor.Split(',')[0].Trim();
        }

        // Check for other common headers
        var realIp = HttpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        // Fall back to connection remote IP
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}

public record StartSessionRequest(
    string? IpAddress,
    string? UserAgent,
    string? CurrentPage,
    string? ReferrerUrl
);

public record UpdateSessionRequest(string? CurrentPage);
