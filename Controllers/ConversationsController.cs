using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Services.Interfaces;

namespace ChatApp.API.Controllers;

[ApiController]
[Route("api/sites/{siteId}/[controller]")]
[Authorize]
public class ConversationsController : ControllerBase
{
    private readonly IConversationService _conversationService;

    public ConversationsController(IConversationService conversationService)
    {
        _conversationService = conversationService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResponse<ConversationListDto>>>> GetConversations(
        string siteId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? priority = null,
        [FromQuery] string? assignedUserId = null,
        [FromQuery] string? visitorId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? search = null,
        [FromQuery] string? tags = null,
        [FromQuery] int? ratingMin = null,
        [FromQuery] int? ratingMax = null,
        [FromQuery] string? resolutionStatus = null,
        [FromQuery] string? sentiment = null,
        [FromQuery] string? intent = null,
        [FromQuery] double? urgencyScoreMin = null,
        [FromQuery] double? urgencyScoreMax = null)
    {
        var request = new ConversationListRequest(page, pageSize, status, priority, assignedUserId, visitorId, from, to, search,
            tags, ratingMin, ratingMax, resolutionStatus, sentiment, intent, urgencyScoreMin, urgencyScoreMax);
        var result = await _conversationService.GetConversationsAsync(siteId, request);
        return Ok(ApiResponse<PagedResponse<ConversationListDto>>.Ok(result));
    }

    [HttpGet("{conversationId}")]
    public async Task<ActionResult<ApiResponse<ConversationDto>>> GetConversation(string siteId, string conversationId)
    {
        try
        {
            var conversation = await _conversationService.GetConversationAsync(conversationId);
            return Ok(ApiResponse<ConversationDto>.Ok(conversation));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ConversationDto>.Fail(ex.Message));
        }
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ConversationDto>>> CreateConversation(
        string siteId,
        [FromBody] CreateConversationRequest request)
    {
        var conversationRequest = request with { SiteId = siteId };
        var conversation = await _conversationService.CreateConversationAsync(conversationRequest);
        return Ok(ApiResponse<ConversationDto>.Ok(conversation, "Conversation created"));
    }

    [HttpPut("{conversationId}")]
    public async Task<ActionResult<ApiResponse<ConversationDto>>> UpdateConversation(
        string siteId,
        string conversationId,
        [FromBody] UpdateConversationRequest request)
    {
        try
        {
            var conversation = await _conversationService.UpdateConversationAsync(conversationId, request);
            return Ok(ApiResponse<ConversationDto>.Ok(conversation));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ConversationDto>.Fail(ex.Message));
        }
    }

    [HttpPost("{conversationId}/assign")]
    public async Task<ActionResult<ApiResponse<ConversationDto>>> AssignConversation(
        string siteId,
        string conversationId,
        [FromBody] AssignConversationRequest request)
    {
        try
        {
            var conversation = await _conversationService.AssignConversationAsync(conversationId, request);
            return Ok(ApiResponse<ConversationDto>.Ok(conversation));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ConversationDto>.Fail(ex.Message));
        }
    }

    [HttpPost("{conversationId}/close")]
    public async Task<ActionResult<ApiResponse<ConversationDto>>> CloseConversation(
        string siteId,
        string conversationId,
        [FromBody] CloseConversationRequest request)
    {
        try
        {
            var conversation = await _conversationService.CloseConversationAsync(conversationId, request);
            return Ok(ApiResponse<ConversationDto>.Ok(conversation));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ConversationDto>.Fail(ex.Message));
        }
    }

    [HttpPost("{conversationId}/csat")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ConversationDto>>> SubmitCsat(
        string siteId,
        string conversationId,
        [FromBody] SubmitCsatRequest request)
    {
        try
        {
            var conversation = await _conversationService.SubmitCsatAsync(conversationId, request);
            return Ok(ApiResponse<ConversationDto>.Ok(conversation, "Rating submitted successfully"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ConversationDto>.Fail(ex.Message));
        }
    }

    [HttpPost("{conversationId}/reopen")]
    public async Task<ActionResult<ApiResponse<ConversationDto>>> ReopenConversation(string siteId, string conversationId)
    {
        try
        {
            var conversation = await _conversationService.ReopenConversationAsync(conversationId);
            return Ok(ApiResponse<ConversationDto>.Ok(conversation));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ConversationDto>.Fail(ex.Message));
        }
    }

    [HttpDelete("{conversationId}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteConversation(string siteId, string conversationId)
    {
        try
        {
            await _conversationService.DeleteConversationAsync(conversationId);
            return Ok(ApiResponse<object>.Ok(null, "Conversation deleted"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MyConversationsController : ControllerBase
{
    private readonly IConversationService _conversationService;

    public MyConversationsController(IConversationService conversationService)
    {
        _conversationService = conversationService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ConversationListDto>>>> GetMyConversations(
        [FromQuery] string? siteId = null)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(ApiResponse<List<ConversationListDto>>.Fail("User not found"));
        }

        var conversations = await _conversationService.GetAgentConversationsAsync(userId, siteId);
        return Ok(ApiResponse<List<ConversationListDto>>.Ok(conversations));
    }
}

/// <summary>
/// Super Admin controller to get all conversations across all sites
/// </summary>
[ApiController]
[Route("api/conversations")]
[Authorize(Roles = "super_admin")]
public class AdminConversationsController : ControllerBase
{
    private readonly IConversationService _conversationService;
    private readonly ISiteService _siteService;

    public AdminConversationsController(IConversationService conversationService, ISiteService siteService)
    {
        _conversationService = conversationService;
        _siteService = siteService;
    }

    /// <summary>
    /// Get all conversations across all sites (super_admin only)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ConversationListDto>>>> GetAllConversations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        // Get all sites
        var sitesResult = await _siteService.GetAllSitesAsync(1, 1000);
        var allConversations = new List<ConversationListDto>();

        // Get conversations from each site
        foreach (var site in sitesResult.Items)
        {
            var request = new ConversationListRequest(page, pageSize, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
            var conversations = await _conversationService.GetConversationsAsync(site.Id, request);

            // Add site info to each conversation
            foreach (var conv in conversations.Items)
            {
                conv.SiteName = site.Name;
                conv.SiteId = site.Id;
            }

            allConversations.AddRange(conversations.Items);
        }

        return Ok(ApiResponse<List<ConversationListDto>>.Ok(allConversations));
    }
}
