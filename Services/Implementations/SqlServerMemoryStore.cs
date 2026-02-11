using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel.Memory;
using ChatApp.API.Data;

namespace ChatApp.API.Services.Implementations;

/// <summary>
/// IMemoryStore implementation over the existing knowledge_chunks + knowledge_documents tables.
/// collectionName = siteId, key = chunk Id.
/// </summary>
public class SqlServerMemoryStore : IMemoryStore
{
    private readonly ApplicationDbContext _context;

    public SqlServerMemoryStore(ApplicationDbContext context)
    {
        _context = context;
    }

    // --- Collection operations (siteId-based, no-op for existing DB) ---

    public Task CreateCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        // Collections map to siteIds — they already exist implicitly
        return Task.CompletedTask;
    }

    public async Task<bool> DoesCollectionExistAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        return await _context.KnowledgeChunks.AnyAsync(c => c.SiteId == collectionName, cancellationToken);
    }

    public async IAsyncEnumerable<string> GetCollectionsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var siteIds = await _context.KnowledgeChunks
            .Select(c => c.SiteId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var siteId in siteIds)
        {
            yield return siteId;
        }
    }

    public async Task DeleteCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        var chunks = await _context.KnowledgeChunks
            .Where(c => c.SiteId == collectionName)
            .ToListAsync(cancellationToken);

        _context.KnowledgeChunks.RemoveRange(chunks);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // --- Record operations ---

    public async Task<string> UpsertAsync(string collectionName, MemoryRecord record, CancellationToken cancellationToken = default)
    {
        var existingChunk = await _context.KnowledgeChunks
            .FirstOrDefaultAsync(c => c.Id == record.Key && c.SiteId == collectionName, cancellationToken);

        if (existingChunk != null)
        {
            existingChunk.Content = record.Metadata.Text;
            existingChunk.EmbeddingJson = JsonSerializer.Serialize(record.Embedding.ToArray());
            existingChunk.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var chunk = new Models.Entities.KnowledgeChunk
            {
                Id = record.Key ?? Guid.NewGuid().ToString(),
                SiteId = collectionName,
                DocumentId = record.Metadata.AdditionalMetadata ?? string.Empty,
                Content = record.Metadata.Text,
                EmbeddingJson = JsonSerializer.Serialize(record.Embedding.ToArray()),
                ChunkIndex = 0,
                TokenCount = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.KnowledgeChunks.Add(chunk);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return record.Key ?? existingChunk?.Id ?? string.Empty;
    }

    public async IAsyncEnumerable<string> UpsertBatchAsync(string collectionName, IEnumerable<MemoryRecord> records,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var record in records)
        {
            yield return await UpsertAsync(collectionName, record, cancellationToken);
        }
    }

    public async Task<MemoryRecord?> GetAsync(string collectionName, string key, bool withEmbedding = false,
        CancellationToken cancellationToken = default)
    {
        var chunk = await _context.KnowledgeChunks
            .Include(c => c.Document)
            .FirstOrDefaultAsync(c => c.Id == key && c.SiteId == collectionName, cancellationToken);

        if (chunk == null) return null;

        return ChunkToMemoryRecord(chunk, withEmbedding);
    }

    public async IAsyncEnumerable<MemoryRecord> GetBatchAsync(string collectionName, IEnumerable<string> keys,
        bool withEmbedding = false, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var keyList = keys.ToList();
        var chunks = await _context.KnowledgeChunks
            .Include(c => c.Document)
            .Where(c => keyList.Contains(c.Id) && c.SiteId == collectionName)
            .ToListAsync(cancellationToken);

        foreach (var chunk in chunks)
        {
            yield return ChunkToMemoryRecord(chunk, withEmbedding);
        }
    }

    public async Task RemoveAsync(string collectionName, string key, CancellationToken cancellationToken = default)
    {
        var chunk = await _context.KnowledgeChunks
            .FirstOrDefaultAsync(c => c.Id == key && c.SiteId == collectionName, cancellationToken);

        if (chunk != null)
        {
            _context.KnowledgeChunks.Remove(chunk);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RemoveBatchAsync(string collectionName, IEnumerable<string> keys,
        CancellationToken cancellationToken = default)
    {
        var keyList = keys.ToList();
        var chunks = await _context.KnowledgeChunks
            .Where(c => keyList.Contains(c.Id) && c.SiteId == collectionName)
            .ToListAsync(cancellationToken);

        _context.KnowledgeChunks.RemoveRange(chunks);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // --- Nearest match (core search) ---

    public async Task<(MemoryRecord, double)?> GetNearestMatchAsync(string collectionName, ReadOnlyMemory<float> embedding,
        double minRelevanceScore = 0, bool withEmbedding = false, CancellationToken cancellationToken = default)
    {
        var results = GetNearestMatchesAsync(collectionName, embedding, limit: 1,
            minRelevanceScore: minRelevanceScore, withEmbedding: withEmbedding, cancellationToken: cancellationToken);

        await foreach (var result in results)
        {
            return result;
        }

        return null;
    }

    public async IAsyncEnumerable<(MemoryRecord, double)> GetNearestMatchesAsync(string collectionName,
        ReadOnlyMemory<float> embedding, int limit, double minRelevanceScore = 0, bool withEmbedding = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Load all indexed chunks for the site
        var chunks = await _context.KnowledgeChunks
            .Include(c => c.Document)
            .Where(c => c.SiteId == collectionName
                && !string.IsNullOrEmpty(c.EmbeddingJson)
                && c.Document != null
                && !c.Document.IsDeleted
                && c.Document.Status == "indexed")
            .ToListAsync(cancellationToken);

        var queryVector = embedding.ToArray();
        var scored = new List<(Models.Entities.KnowledgeChunk Chunk, double Score)>();

        Console.WriteLine($"[MemoryStore] Found {chunks.Count} chunks for collection '{collectionName}', query embedding dims: {queryVector.Length}, minScore: {minRelevanceScore}");

        foreach (var chunk in chunks)
        {
            var chunkEmbedding = JsonSerializer.Deserialize<float[]>(chunk.EmbeddingJson!);
            if (chunkEmbedding == null) continue;

            var similarity = CosineSimilarity(queryVector, chunkEmbedding);
            Console.WriteLine($"[MemoryStore] Chunk '{chunk.Id}' dims: {chunkEmbedding.Length}, similarity: {similarity:F4}");
            if (similarity >= minRelevanceScore)
            {
                scored.Add((chunk, similarity));
            }
        }

        var topResults = scored.OrderByDescending(s => s.Score).Take(limit);

        foreach (var (chunk, score) in topResults)
        {
            yield return (ChunkToMemoryRecord(chunk, withEmbedding), score);
        }
    }

    // --- Helpers ---

    private static MemoryRecord ChunkToMemoryRecord(Models.Entities.KnowledgeChunk chunk, bool withEmbedding)
    {
        var embedding = withEmbedding && !string.IsNullOrEmpty(chunk.EmbeddingJson)
            ? new ReadOnlyMemory<float>(JsonSerializer.Deserialize<float[]>(chunk.EmbeddingJson)!)
            : ReadOnlyMemory<float>.Empty;

        return MemoryRecord.LocalRecord(
            id: chunk.Id,
            text: chunk.Content,
            description: chunk.Document?.Title ?? string.Empty,
            embedding: embedding,
            additionalMetadata: chunk.DocumentId
        );
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        double dotProduct = 0, magnitudeA = 0, magnitudeB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        magnitudeA = Math.Sqrt(magnitudeA);
        magnitudeB = Math.Sqrt(magnitudeB);

        if (magnitudeA == 0 || magnitudeB == 0) return 0;

        return dotProduct / (magnitudeA * magnitudeB);
    }
}
