namespace ChatApp.API.Models.DTOs;

// Document DTOs
public record KnowledgeDocumentDto(
    string Id,
    string SiteId,
    string Title,
    string? Description,
    string DocumentType,
    string? OriginalFileName,
    long? FileSize,
    string Status,
    string? ProcessingError,
    int ChunkCount,
    int TokenCount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? IndexedAt
);

public record KnowledgeDocumentDetailDto(
    string Id,
    string SiteId,
    string Title,
    string? Description,
    string DocumentType,
    string? OriginalFileName,
    long? FileSize,
    string? MimeType,
    string Status,
    string? ProcessingError,
    int ChunkCount,
    int TokenCount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? IndexedAt,
    string? ExtractedText,
    List<KnowledgeChunkDto> Chunks
);

public record KnowledgeChunkDto(
    string Id,
    int ChunkIndex,
    string Content,
    int TokenCount
);

// Request DTOs
public record CreateTextKnowledgeRequest(
    string Title,
    string? Description,
    string Content
);

public record UpdateKnowledgeDocumentRequest(
    string Title,
    string? Description
);

public record KnowledgeSearchRequest(
    string Query,
    int MaxResults = 5,
    double MinSimilarity = 0.7
);

public record KnowledgeSearchResult(
    string ChunkId,
    string DocumentId,
    string DocumentTitle,
    string Content,
    double Similarity,
    int ChunkIndex
);

// RAG DTOs
public record AnalyzeMessageWithRagRequest(
    string Message,
    string SiteId,
    string? ConversationId = null,
    string? VisitorId = null,
    int MaxChunks = 3,
    double MinSimilarity = 0.7
);

public record AnalyzeMessageWithRagResponse(
    string SuggestedReply,
    string InterestLevel,
    int ConversionPercentage,
    string? Objection,
    string NextAction,
    string? Sentiment,
    string? Intent,
    List<string>? Keywords,
    List<KnowledgeContextDto> RelevantKnowledge,
    bool UsedKnowledgeBase
);

public record KnowledgeContextDto(
    string DocumentTitle,
    string ChunkContent,
    double Similarity
);

// Stats DTO
public record KnowledgeStatsDto(
    int TotalDocuments,
    int IndexedDocuments,
    int ProcessingDocuments,
    int FailedDocuments,
    int PendingDocuments,
    int TotalChunks,
    int TotalTokens
);
