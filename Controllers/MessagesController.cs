using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Services.Interfaces;

namespace ChatApp.API.Controllers;

[ApiController]
[Route("api/conversations/{conversationId}/[controller]")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _messageService;

    public MessagesController(IMessageService messageService)
    {
        _messageService = messageService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResponse<MessageDto>>>> GetMessages(
        string conversationId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? before = null,
        [FromQuery] string? after = null)
    {
        var request = new MessageListRequest(page, pageSize, before, after);
        var result = await _messageService.GetMessagesAsync(conversationId, request);
        return Ok(ApiResponse<PagedResponse<MessageDto>>.Ok(result));
    }

    [HttpGet("{messageId}")]
    public async Task<ActionResult<ApiResponse<MessageDto>>> GetMessage(string conversationId, string messageId)
    {
        try
        {
            var message = await _messageService.GetMessageAsync(messageId);
            return Ok(ApiResponse<MessageDto>.Ok(message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<MessageDto>.Fail(ex.Message));
        }
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<MessageDto>>> SendMessage(
        string conversationId,
        [FromBody] SendMessageRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var messageRequest = request with { ConversationId = conversationId };
        var message = await _messageService.SendMessageAsync("agent", userId, messageRequest);
        return Ok(ApiResponse<MessageDto>.Ok(message, "Message sent"));
    }

    [HttpPost("visitor")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<MessageDto>>> SendVisitorMessage(
        string conversationId,
        [FromBody] VisitorMessageRequest request)
    {
        var messageRequest = new SendMessageRequest(conversationId, request.Content, request.MessageType, request.FileId, null);
        var message = await _messageService.SendMessageAsync("visitor", request.VisitorId, messageRequest);
        return Ok(ApiResponse<MessageDto>.Ok(message, "Message sent"));
    }

    [HttpPut("{messageId}")]
    public async Task<ActionResult<ApiResponse<MessageDto>>> UpdateMessage(
        string conversationId,
        string messageId,
        [FromBody] UpdateMessageRequest request)
    {
        try
        {
            var message = await _messageService.UpdateMessageAsync(messageId, request);
            return Ok(ApiResponse<MessageDto>.Ok(message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<MessageDto>.Fail(ex.Message));
        }
    }

    [HttpDelete("{messageId}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteMessage(string conversationId, string messageId)
    {
        try
        {
            await _messageService.DeleteMessageAsync(messageId);
            return Ok(ApiResponse<object>.Ok(null, "Message deleted"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("read")]
    public async Task<ActionResult<ApiResponse<object>>> MarkMessagesRead(
        string conversationId,
        [FromBody] MarkMessagesReadRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(ApiResponse<object>.Fail("User not found"));
        }

        await _messageService.MarkMessagesReadAsync("agent", userId, request);
        return Ok(ApiResponse<object>.Ok(null, "Messages marked as read"));
    }

    [HttpPost("read/visitor")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> MarkMessagesReadByVisitor(
        string conversationId,
        [FromBody] VisitorReadRequest request)
    {
        await _messageService.MarkMessagesReadAsync("visitor", request.VisitorId, new MarkMessagesReadRequest(request.MessageIds));
        return Ok(ApiResponse<object>.Ok(null, "Messages marked as read"));
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<ApiResponse<object>>> GetUnreadCount(string conversationId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(ApiResponse<object>.Fail("User not found"));
        }

        var count = await _messageService.GetUnreadCountAsync(conversationId, "agent", userId);
        return Ok(ApiResponse<object>.Ok(new { unreadCount = count }));
    }
}

public record VisitorMessageRequest(
    string VisitorId,
    string Content,
    string MessageType = "text",
    string? FileId = null
);

public record VisitorReadRequest(
    string VisitorId,
    List<string> MessageIds
);
