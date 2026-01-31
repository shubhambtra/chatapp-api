using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Services.Interfaces;

namespace ChatApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class KnowledgeController : ControllerBase
{
    private readonly IKnowledgeService _knowledgeService;

    public KnowledgeController(IKnowledgeService knowledgeService)
    {
        _knowledgeService = knowledgeService;
    }

    /// <summary>
    /// Get all documents for a site
    /// GET /api/knowledge/sites/{siteId}/documents
    /// </summary>
    [HttpGet("sites/{siteId}/documents")]
    public async Task<ActionResult<ApiResponse<List<KnowledgeDocumentDto>>>> GetDocuments(
        string siteId,
        [FromQuery] string? status = null)
    {
        try
        {
            var documents = await _knowledgeService.GetDocumentsAsync(siteId, status);
            return Ok(ApiResponse<List<KnowledgeDocumentDto>>.Ok(documents));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<List<KnowledgeDocumentDto>>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Get document detail with chunks
    /// GET /api/knowledge/documents/{id}
    /// </summary>
    [HttpGet("documents/{id}")]
    public async Task<ActionResult<ApiResponse<KnowledgeDocumentDetailDto>>> GetDocument(string id)
    {
        try
        {
            var document = await _knowledgeService.GetDocumentAsync(id);
            if (document == null)
            {
                return NotFound(ApiResponse<KnowledgeDocumentDetailDto>.Fail("Document not found"));
            }
            return Ok(ApiResponse<KnowledgeDocumentDetailDto>.Ok(document));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<KnowledgeDocumentDetailDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Create a text knowledge entry
    /// POST /api/knowledge/sites/{siteId}/documents/text
    /// </summary>
    [HttpPost("sites/{siteId}/documents/text")]
    public async Task<ActionResult<ApiResponse<KnowledgeDocumentDto>>> CreateTextDocument(
        string siteId,
        [FromBody] CreateTextKnowledgeRequest request)
    {
        try
        {
            var document = await _knowledgeService.CreateTextDocumentAsync(siteId, request);
            return Ok(ApiResponse<KnowledgeDocumentDto>.Ok(document, "Document created and queued for processing"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<KnowledgeDocumentDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Upload a document file (PDF, DOCX, TXT)
    /// POST /api/knowledge/sites/{siteId}/documents/upload
    /// </summary>
    [HttpPost("sites/{siteId}/documents/upload")]
    public async Task<ActionResult<ApiResponse<KnowledgeDocumentDto>>> UploadDocument(
        string siteId,
        IFormFile file,
        [FromForm] string? title = null,
        [FromForm] string? description = null)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(ApiResponse<KnowledgeDocumentDto>.Fail("No file provided"));
            }

            // Validate file size (max 10MB)
            if (file.Length > 10 * 1024 * 1024)
            {
                return BadRequest(ApiResponse<KnowledgeDocumentDto>.Fail("File too large. Maximum size is 10MB"));
            }

            var document = await _knowledgeService.UploadDocumentAsync(siteId, file, title, description);
            return Ok(ApiResponse<KnowledgeDocumentDto>.Ok(document, "Document uploaded and queued for processing"));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<KnowledgeDocumentDto>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<KnowledgeDocumentDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Update document metadata
    /// PUT /api/knowledge/documents/{id}
    /// </summary>
    [HttpPut("documents/{id}")]
    public async Task<ActionResult<ApiResponse<KnowledgeDocumentDto>>> UpdateDocument(
        string id,
        [FromBody] UpdateKnowledgeDocumentRequest request)
    {
        try
        {
            var document = await _knowledgeService.UpdateDocumentAsync(id, request);
            return Ok(ApiResponse<KnowledgeDocumentDto>.Ok(document));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<KnowledgeDocumentDto>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<KnowledgeDocumentDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Delete a document
    /// DELETE /api/knowledge/documents/{id}
    /// </summary>
    [HttpDelete("documents/{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteDocument(string id)
    {
        try
        {
            var result = await _knowledgeService.DeleteDocumentAsync(id);
            if (!result)
            {
                return NotFound(ApiResponse<bool>.Fail("Document not found"));
            }
            return Ok(ApiResponse<bool>.Ok(true, "Document deleted"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<bool>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Reprocess a failed document
    /// POST /api/knowledge/documents/{id}/reprocess
    /// </summary>
    [HttpPost("documents/{id}/reprocess")]
    public async Task<ActionResult<ApiResponse<bool>>> ReprocessDocument(string id)
    {
        try
        {
            var result = await _knowledgeService.ReprocessDocumentAsync(id);
            if (!result)
            {
                return NotFound(ApiResponse<bool>.Fail("Document not found"));
            }
            return Ok(ApiResponse<bool>.Ok(true, "Document queued for reprocessing"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<bool>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Search knowledge base
    /// POST /api/knowledge/sites/{siteId}/search
    /// </summary>
    [HttpPost("sites/{siteId}/search")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<KnowledgeSearchResult>>>> SearchKnowledge(
        string siteId,
        [FromBody] KnowledgeSearchRequest request)
    {
        try
        {
            var results = await _knowledgeService.SearchKnowledgeAsync(siteId, request);
            return Ok(ApiResponse<List<KnowledgeSearchResult>>.Ok(results));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<List<KnowledgeSearchResult>>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Get knowledge base stats for a site
    /// GET /api/knowledge/sites/{siteId}/stats
    /// </summary>
    [HttpGet("sites/{siteId}/stats")]
    public async Task<ActionResult<ApiResponse<KnowledgeStatsDto>>> GetStats(string siteId)
    {
        try
        {
            var stats = await _knowledgeService.GetStatsAsync(siteId);
            return Ok(ApiResponse<KnowledgeStatsDto>.Ok(stats));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<KnowledgeStatsDto>.Fail(ex.Message));
        }
    }
}
