using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Services.Interfaces;

namespace ChatApp.API.Controllers.V1;

/// <summary>
/// V1 Visitors API - matches API documentation routes
/// </summary>
[ApiController]
[Route("api/v1/visitors")]
[Authorize]
public class VisitorsV1Controller : ControllerBase
{
    private readonly IVisitorService _visitorService;

    public VisitorsV1Controller(IVisitorService visitorService)
    {
        _visitorService = visitorService;
    }

    /// <summary>
    /// Register a new visitor (called when customer starts chat)
    /// POST /visitors
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<RegisterVisitorResponse>>> RegisterVisitor([FromBody] RegisterVisitorRequest request)
    {
        var createRequest = new CreateVisitorRequest(
            request.SiteId,
            request.VisitorId,
            request.Email,
            request.Name,
            null,
            request.Metadata?.UserAgent,
            null,
            request.Metadata?.Referrer,
            request.Metadata?.PageUrl,
            null
        );

        var visitor = await _visitorService.CreateVisitorAsync(createRequest);

        // Start a session for the visitor
        var session = await _visitorService.StartSessionAsync(
            visitor.Id,
            null,
            request.Metadata?.UserAgent,
            request.Metadata?.PageUrl,
            request.Metadata?.Referrer
        );

        return Ok(ApiResponse<RegisterVisitorResponse>.Ok(new RegisterVisitorResponse(
            visitor.Id,
            request.VisitorId,
            visitor.Name,
            session.Id,
            visitor.CreatedAt
        )));
    }

    /// <summary>
    /// Get visitor details and history
    /// GET /visitors/{visitor_id}
    /// </summary>
    [HttpGet("{visitorId}")]
    public async Task<ActionResult<ApiResponse<VisitorDetailResponse>>> GetVisitor(
        string visitorId,
        [FromQuery] string siteId)
    {
        var visitor = await _visitorService.GetVisitorByExternalIdAsync(siteId, visitorId)
            ?? await _visitorService.GetVisitorAsync(visitorId);

        if (visitor == null)
        {
            return NotFound(ApiResponse<VisitorDetailResponse>.Fail("Visitor not found"));
        }

        var response = new VisitorDetailResponse(
            visitor.Id,
            visitorId,
            visitor.Name,
            visitor.Email,
            visitor.CustomData,
            visitor.CreatedAt,
            visitor.LastSeenAt,
            visitor.TotalVisits,
            visitor.IsOnline ? "online" : "offline"
        );

        return Ok(ApiResponse<VisitorDetailResponse>.Ok(response));
    }

    /// <summary>
    /// List all currently active visitors for a site
    /// GET /visitors/active
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<ApiResponse<ActiveVisitorsResponse>>> GetActiveVisitors([FromQuery] string siteId)
    {
        var result = await _visitorService.GetActiveVisitorsAsync(siteId);
        return Ok(ApiResponse<ActiveVisitorsResponse>.Ok(result));
    }
}

// DTOs specific to V1 API documentation format
public record RegisterVisitorRequest(
    string SiteId,
    string VisitorId,
    string Name,
    string? Email,
    VisitorMetadata? Metadata
);

public record VisitorMetadata(
    string? PageUrl,
    string? UserAgent,
    string? Referrer
);

public record VisitorDetailResponse(
    string Id,
    string VisitorId,
    string? Name,
    string? Email,
    Dictionary<string, object>? Metadata,
    DateTime FirstSeen,
    DateTime? LastSeen,
    int TotalConversations,
    string Status
);
