using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Services.Interfaces;

namespace ChatApp.API.Controllers;

/// <summary>
/// Simple Chat API for Python WebSocket integration
/// Combines visitor, conversation, and message operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IVisitorService _visitorService;
    private readonly IConversationService _conversationService;
    private readonly IMessageService _messageService;

    public ChatController(
        IVisitorService visitorService,
        IConversationService conversationService,
        IMessageService messageService)
    {
        _visitorService = visitorService;
        _conversationService = conversationService;
        _messageService = messageService;
    }

    /// <summary>
    /// Initialize a chat session - creates/gets visitor and conversation
    /// Called when customer connects to WebSocket
    /// </summary>
    [HttpPost("init")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ChatInitResponse>>> InitializeChat([FromBody] ChatInitRequest request)
    {
        try
        {
            // Get or create visitor
            var visitor = await _visitorService.GetOrCreateVisitorAsync(
                request.SiteId,
                request.VisitorId,
                request.Name,
                request.Email
            );

            // Get or create conversation
            var conversation = await _conversationService.GetOrCreateConversationAsync(
                request.SiteId,
                visitor.Id
            );

            return Ok(ApiResponse<ChatInitResponse>.Ok(new ChatInitResponse(
                visitor.Id,
                visitor.Name,
                conversation.Id,
                conversation.Status
            )));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<ChatInitResponse>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Save a message from WebSocket
    /// </summary>
    [HttpPost("message")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ChatMessageResponse>>> SaveMessage([FromBody] ChatMessageRequest request)
    {
        try
        {
            var messageRequest = new SendMessageRequest(
                request.ConversationId,
                request.Content,
                request.MessageType,
                request.FileId
            );

            var message = await _messageService.SendMessageAsync(
                request.SenderType,
                request.SenderId,
                messageRequest
            );

            return Ok(ApiResponse<ChatMessageResponse>.Ok(new ChatMessageResponse(
                message.Id,
                message.ConversationId,
                message.SenderType,
                message.Content,
                message.MessageType,
                message.CreatedAt
            )));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<ChatMessageResponse>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Get conversation history
    /// </summary>
    [HttpGet("history/{conversationId}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<ChatMessageResponse>>>> GetHistory(
        string conversationId,
        [FromQuery] int limit = 50)
    {
        try
        {
            var request = new MessageListRequest(1, limit, null, null);
            var messages = await _messageService.GetMessagesAsync(conversationId, request);

            var response = messages.Items.Select(m => new ChatMessageResponse(
                m.Id,
                m.ConversationId,
                m.SenderType,
                m.Content,
                m.MessageType,
                m.CreatedAt
            )).ToList();

            return Ok(ApiResponse<List<ChatMessageResponse>>.Ok(response));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<List<ChatMessageResponse>>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Update visitor status (online/offline)
    /// </summary>
    [HttpPost("status")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> UpdateVisitorStatus([FromBody] VisitorStatusRequest request)
    {
        try
        {
            if (request.IsOnline)
            {
                await _visitorService.StartSessionAsync(
                    request.VisitorId,
                    request.IpAddress,
                    request.UserAgent,
                    request.CurrentPage,
                    null
                );
            }
            else
            {
                // End all active sessions for this visitor
                var sessions = await _visitorService.GetVisitorSessionsAsync(request.VisitorId);
                foreach (var session in sessions.Where(s => s.IsActive))
                {
                    await _visitorService.EndSessionAsync(session.Id);
                }
            }

            return Ok(ApiResponse<object>.Ok(null, "Status updated"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}

// DTOs for Chat API
public record ChatInitRequest(
    string SiteId,
    string VisitorId,
    string? Name = null,
    string? Email = null
);

public record ChatInitResponse(
    string VisitorId,
    string? VisitorName,
    string ConversationId,
    string ConversationStatus
);

public record ChatMessageRequest(
    string ConversationId,
    string SenderType,  // "visitor" or "agent"
    string SenderId,
    string Content,
    string MessageType = "text",
    string? FileId = null
);

public record ChatMessageResponse(
    string Id,
    string ConversationId,
    string SenderType,
    string? Content,
    string MessageType,
    DateTime CreatedAt
);

public record VisitorStatusRequest(
    string VisitorId,
    bool IsOnline,
    string? IpAddress = null,
    string? UserAgent = null,
    string? CurrentPage = null
);
