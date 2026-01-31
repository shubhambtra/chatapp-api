using ChatApp.API.Models.DTOs;
using Microsoft.AspNetCore.Http;

namespace ChatApp.API.Services.Interfaces;

public interface IKnowledgeService
{
    // Document CRUD
    Task<List<KnowledgeDocumentDto>> GetDocumentsAsync(string siteId, string? status = null);
    Task<KnowledgeDocumentDetailDto?> GetDocumentAsync(string documentId);
    Task<KnowledgeDocumentDto> CreateTextDocumentAsync(string siteId, CreateTextKnowledgeRequest request);
    Task<KnowledgeDocumentDto> UploadDocumentAsync(string siteId, IFormFile file, string? title = null, string? description = null);
    Task<KnowledgeDocumentDto> UpdateDocumentAsync(string documentId, UpdateKnowledgeDocumentRequest request);
    Task<bool> DeleteDocumentAsync(string documentId);

    // Processing
    Task ProcessDocumentAsync(string documentId);
    Task<bool> ReprocessDocumentAsync(string documentId);

    // Search and RAG
    Task<List<KnowledgeSearchResult>> SearchKnowledgeAsync(string siteId, KnowledgeSearchRequest request);
    Task<AnalyzeMessageWithRagResponse> AnalyzeMessageWithRagAsync(AnalyzeMessageWithRagRequest request);

    // Stats
    Task<KnowledgeStatsDto> GetStatsAsync(string siteId);
}
