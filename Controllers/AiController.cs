using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Services.Interfaces;

namespace ChatApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AiController : ControllerBase
{
    private readonly IAiService _aiService;
    private readonly IKnowledgeService _knowledgeService;

    public AiController(IAiService aiService, IKnowledgeService knowledgeService)
    {
        _aiService = aiService;
        _knowledgeService = knowledgeService;
    }

    [HttpPost("analyze/{conversationId}")]
    public async Task<ActionResult<ApiResponse<AnalyzeConversationResponse>>> AnalyzeConversation(string conversationId)
    {
        try
        {
            var result = await _aiService.AnalyzeConversationAsync(conversationId);
            return Ok(ApiResponse<AnalyzeConversationResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<AnalyzeConversationResponse>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<AnalyzeConversationResponse>.Fail(ex.Message));
        }
    }

    [HttpPost("conversations/{conversationId}/analyze")]
    public async Task<ActionResult<ApiResponse<AnalyzeConversationResponse>>> AnalyzeConversationAlt(string conversationId)
    {
        try
        {
            var result = await _aiService.AnalyzeConversationAsync(conversationId);
            return Ok(ApiResponse<AnalyzeConversationResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<AnalyzeConversationResponse>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<AnalyzeConversationResponse>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Analyze a single customer message for sales insights
    /// POST /api/ai/analyze-message
    /// </summary>
    [HttpPost("analyze-message")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AnalyzeMessageResponse>>> AnalyzeMessage([FromBody] AnalyzeMessageRequest request)
    {
        try
        {
            var result = await _aiService.AnalyzeMessageAsync(request);
            return Ok(ApiResponse<AnalyzeMessageResponse>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<AnalyzeMessageResponse>.Fail(ex.Message));
        }
    }

    [HttpPost("generate-response")]
    public async Task<ActionResult<ApiResponse<GenerateResponseResponse>>> GenerateResponse([FromBody] GenerateResponseRequest request)
    {
        try
        {
            var result = await _aiService.GenerateResponseAsync(request);
            return Ok(ApiResponse<GenerateResponseResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<GenerateResponseResponse>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<GenerateResponseResponse>.Fail(ex.Message));
        }
    }

    [HttpPost("summarize/{conversationId}")]
    public async Task<ActionResult<ApiResponse<SummarizeConversationResponse>>> SummarizeConversation(string conversationId)
    {
        try
        {
            var result = await _aiService.SummarizeConversationAsync(conversationId);
            return Ok(ApiResponse<SummarizeConversationResponse>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<SummarizeConversationResponse>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SummarizeConversationResponse>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Analyze a customer message using RAG (Retrieval-Augmented Generation)
    /// POST /api/ai/analyze-message-with-rag
    /// </summary>
    [HttpPost("analyze-message-with-rag")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AnalyzeMessageWithRagResponse>>> AnalyzeMessageWithRag(
        [FromBody] AnalyzeMessageWithRagRequest request)
    {
        try
        {
            var result = await _knowledgeService.AnalyzeMessageWithRagAsync(request);
            return Ok(ApiResponse<AnalyzeMessageWithRagResponse>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<AnalyzeMessageWithRagResponse>.Fail(ex.Message));
        }
    }
}
