namespace ChatApp.API.Models.Entities;

public class KnowledgeDocument : BaseEntity
{
    // Site relationship
    public string SiteId { get; set; } = string.Empty;
    public Site? Site { get; set; }

    // Document metadata
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DocumentType { get; set; } = "text"; // text, pdf, docx, txt

    // File information (for uploaded files)
    public string? OriginalFileName { get; set; }
    public string? StoredFileName { get; set; }
    public string? FilePath { get; set; }
    public long? FileSize { get; set; }
    public string? MimeType { get; set; }

    // Content
    public string? RawContent { get; set; }
    public string? ExtractedText { get; set; }

    // Processing status
    public string Status { get; set; } = "pending"; // pending, processing, indexed, failed
    public string? ProcessingError { get; set; }
    public DateTime? IndexedAt { get; set; }

    // Stats
    public int ChunkCount { get; set; }
    public int TokenCount { get; set; }

    // Soft delete
    public bool IsDeleted { get; set; }

    // Navigation properties
    public ICollection<KnowledgeChunk> Chunks { get; set; } = new List<KnowledgeChunk>();
}
