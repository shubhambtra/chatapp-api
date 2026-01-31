namespace ChatApp.API.Models.Entities;

public class KnowledgeChunk : BaseEntity
{
    // Document relationship
    public string DocumentId { get; set; } = string.Empty;
    public KnowledgeDocument? Document { get; set; }

    // Site relationship (denormalized for faster queries)
    public string SiteId { get; set; } = string.Empty;
    public Site? Site { get; set; }

    // Chunk content
    public string Content { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public int StartOffset { get; set; }
    public int EndOffset { get; set; }
    public int TokenCount { get; set; }

    // Embedding (JSON array of 1536 floats for text-embedding-3-small)
    public string? EmbeddingJson { get; set; }
}
