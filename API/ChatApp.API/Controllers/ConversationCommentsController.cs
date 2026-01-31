using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChatApp.API.Data;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Models.Entities;

namespace ChatApp.API.Controllers;

[ApiController]
[Route("api/conversations/{conversationId}/comments")]
[Authorize]
public class ConversationCommentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ConversationCommentsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ConversationCommentDto>>>> GetComments(string conversationId)
    {
        var commentsData = await _context.ConversationComments
            .Where(c => c.ConversationId == conversationId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        var comments = commentsData.Select(c => new ConversationCommentDto(
            c.Id,
            c.ConversationId,
            c.AuthorId,
            c.AuthorName,
            c.Content,
            string.IsNullOrEmpty(c.Mentions) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(c.Mentions) ?? new List<string>(),
            c.CreatedAt
        )).ToList();

        return Ok(ApiResponse<List<ConversationCommentDto>>.Ok(comments));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ConversationCommentDto>>> AddComment(
        string conversationId,
        [FromBody] CreateCommentRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(ApiResponse<ConversationCommentDto>.Fail("User not found"));
        }

        // Get the user's name
        var user = await _context.Users.FindAsync(userId);
        var authorName = user?.Username ?? user?.FirstName ?? "Agent";

        // Verify conversation exists
        var conversation = await _context.Conversations.FindAsync(conversationId);
        if (conversation == null)
        {
            return NotFound(ApiResponse<ConversationCommentDto>.Fail("Conversation not found"));
        }

        var comment = new ConversationComment
        {
            ConversationId = conversationId,
            AuthorId = userId,
            AuthorName = authorName,
            Content = request.Content,
            Mentions = request.Mentions != null ? JsonSerializer.Serialize(request.Mentions) : "[]"
        };

        _context.ConversationComments.Add(comment);
        await _context.SaveChangesAsync();

        var dto = new ConversationCommentDto(
            comment.Id,
            comment.ConversationId,
            comment.AuthorId,
            comment.AuthorName,
            comment.Content,
            request.Mentions ?? new List<string>(),
            comment.CreatedAt
        );

        return Ok(ApiResponse<ConversationCommentDto>.Ok(dto, "Comment added"));
    }

    [HttpDelete("{commentId}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteComment(string conversationId, string commentId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var comment = await _context.ConversationComments
            .FirstOrDefaultAsync(c => c.Id == commentId && c.ConversationId == conversationId);

        if (comment == null)
        {
            return NotFound(ApiResponse<object>.Fail("Comment not found"));
        }

        // Only allow author to delete their own comments
        if (comment.AuthorId != userId)
        {
            return Forbid();
        }

        _context.ConversationComments.Remove(comment);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(null, "Comment deleted"));
    }
}
