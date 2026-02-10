using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using ChatApp.API.Data;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Models.Entities;
using ChatApp.API.Plugins;
using ChatApp.API.Services.Interfaces;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;

namespace ChatApp.API.Services.Implementations;

public class KnowledgeService : IKnowledgeService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly IErrorLogService _errorLogService;
    private readonly ISemanticKernelFactory _kernelFactory;
    private readonly QueryRewritePlugin _queryRewritePlugin;
    private readonly KnowledgeSearchPlugin _knowledgeSearchPlugin;
    private readonly ResponseGenerationPlugin _responseGenerationPlugin;

    private const int CHUNK_SIZE_TOKENS = 500;
    private const int CHUNK_OVERLAP_TOKENS = 50;

    public KnowledgeService(
        ApplicationDbContext context,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        IErrorLogService errorLogService,
        ISemanticKernelFactory kernelFactory,
        QueryRewritePlugin queryRewritePlugin,
        KnowledgeSearchPlugin knowledgeSearchPlugin,
        ResponseGenerationPlugin responseGenerationPlugin)
    {
        _context = context;
        _configuration = configuration;
        _environment = environment;
        _errorLogService = errorLogService;
        _kernelFactory = kernelFactory;
        _queryRewritePlugin = queryRewritePlugin;
        _knowledgeSearchPlugin = knowledgeSearchPlugin;
        _responseGenerationPlugin = responseGenerationPlugin;
    }

    #region Document CRUD

    public async Task<List<KnowledgeDocumentDto>> GetDocumentsAsync(string siteId, string? status = null)
    {
        var query = _context.KnowledgeDocuments
            .Where(d => d.SiteId == siteId && !d.IsDeleted);

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(d => d.Status == status);
        }

        var documents = await query
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        return documents.Select(MapToDto).ToList();
    }

    public async Task<KnowledgeDocumentDetailDto?> GetDocumentAsync(string documentId)
    {
        var document = await _context.KnowledgeDocuments
            .Include(d => d.Chunks.OrderBy(c => c.ChunkIndex))
            .FirstOrDefaultAsync(d => d.Id == documentId && !d.IsDeleted);

        if (document == null) return null;

        return new KnowledgeDocumentDetailDto(
            document.Id,
            document.SiteId,
            document.Title,
            document.Description,
            document.DocumentType,
            document.OriginalFileName,
            document.FileSize,
            document.MimeType,
            document.Status,
            document.ProcessingError,
            document.ChunkCount,
            document.TokenCount,
            document.CreatedAt,
            document.UpdatedAt,
            document.IndexedAt,
            document.ExtractedText,
            document.Chunks.Select(c => new KnowledgeChunkDto(
                c.Id,
                c.ChunkIndex,
                c.Content,
                c.TokenCount
            )).ToList()
        );
    }

    public async Task<KnowledgeDocumentDto> CreateTextDocumentAsync(string siteId, CreateTextKnowledgeRequest request)
    {
        var document = new KnowledgeDocument
        {
            SiteId = siteId,
            Title = request.Title,
            Description = request.Description,
            DocumentType = "text",
            RawContent = request.Content,
            ExtractedText = request.Content,
            Status = "pending"
        };

        _context.KnowledgeDocuments.Add(document);
        await _context.SaveChangesAsync();

        // Process in background
        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessDocumentAsync(document.Id);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Knowledge] Background processing failed for {document.Id}: {ex.Message}");
                await _errorLogService.LogErrorAsync(ex, null, "Error");
            }
        });

        return MapToDto(document);
    }

    public async Task<KnowledgeDocumentDto> UploadDocumentAsync(string siteId, IFormFile file, string? title = null, string? description = null)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var documentType = extension switch
        {
            ".pdf" => "pdf",
            ".docx" => "docx",
            ".doc" => "docx",
            ".txt" => "txt",
            _ => throw new ArgumentException($"Unsupported file type: {extension}")
        };

        // Save file
        var uploadsPath = Path.Combine(_environment.ContentRootPath, "uploads", "knowledge");
        Directory.CreateDirectory(uploadsPath);

        var storedFileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(uploadsPath, storedFileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var document = new KnowledgeDocument
        {
            SiteId = siteId,
            Title = title ?? Path.GetFileNameWithoutExtension(file.FileName),
            Description = description,
            DocumentType = documentType,
            OriginalFileName = file.FileName,
            StoredFileName = storedFileName,
            FilePath = filePath,
            FileSize = file.Length,
            MimeType = file.ContentType,
            Status = "pending"
        };

        _context.KnowledgeDocuments.Add(document);
        await _context.SaveChangesAsync();

        // Process in background
        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessDocumentAsync(document.Id);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Knowledge] Background processing failed for {document.Id}: {ex.Message}");
                await _errorLogService.LogErrorAsync(ex, null, "Error");
            }
        });

        return MapToDto(document);
    }

    public async Task<KnowledgeDocumentDto> UpdateDocumentAsync(string documentId, UpdateKnowledgeDocumentRequest request)
    {
        var document = await _context.KnowledgeDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId && !d.IsDeleted);

        if (document == null)
            throw new KeyNotFoundException("Document not found");

        document.Title = request.Title;
        document.Description = request.Description;
        document.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return MapToDto(document);
    }

    public async Task<bool> DeleteDocumentAsync(string documentId)
    {
        var document = await _context.KnowledgeDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null) return false;

        // Soft delete
        document.IsDeleted = true;
        document.UpdatedAt = DateTime.UtcNow;

        // Delete chunks
        var chunks = await _context.KnowledgeChunks
            .Where(c => c.DocumentId == documentId)
            .ToListAsync();

        _context.KnowledgeChunks.RemoveRange(chunks);
        await _context.SaveChangesAsync();

        // Delete file if exists
        if (!string.IsNullOrEmpty(document.FilePath) && File.Exists(document.FilePath))
        {
            try
            {
                File.Delete(document.FilePath);
            }
            catch { }
        }

        return true;
    }

    #endregion

    #region Processing

    public async Task ProcessDocumentAsync(string documentId)
    {
        // Use a new context for background processing
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(_configuration.GetConnectionString("DefaultConnection"));

        using var context = new ApplicationDbContext(optionsBuilder.Options);

        var document = await context.KnowledgeDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null) return;

        try
        {
            document.Status = "processing";
            await context.SaveChangesAsync();

            // Extract text if needed
            if (string.IsNullOrEmpty(document.ExtractedText))
            {
                document.ExtractedText = await ExtractTextFromFileAsync(document);
            }

            if (string.IsNullOrEmpty(document.ExtractedText))
            {
                throw new InvalidOperationException("No text could be extracted from document");
            }

            // Chunk text
            var chunks = ChunkText(document.ExtractedText);
            var totalTokens = 0;

            // Delete existing chunks
            var existingChunks = await context.KnowledgeChunks
                .Where(c => c.DocumentId == documentId)
                .ToListAsync();
            context.KnowledgeChunks.RemoveRange(existingChunks);

            // Use SK memory to generate embeddings and save chunks
            var memory = await _kernelFactory.CreateMemoryAsync();

            for (int i = 0; i < chunks.Count; i++)
            {
                var chunkText = chunks[i].Text;
                var chunkId = Guid.NewGuid().ToString();

                // SaveInformationAsync generates embedding via SK and stores via our SqlServerMemoryStore
                await memory.SaveInformationAsync(
                    collection: document.SiteId,
                    text: chunkText,
                    id: chunkId,
                    description: document.Title,
                    additionalMetadata: documentId);

                // Update the chunk record with proper metadata (SaveInformation created it via UpsertAsync)
                var savedChunk = await context.KnowledgeChunks.FirstOrDefaultAsync(c => c.Id == chunkId);
                if (savedChunk != null)
                {
                    savedChunk.ChunkIndex = i;
                    savedChunk.StartOffset = chunks[i].StartOffset;
                    savedChunk.EndOffset = chunks[i].EndOffset;
                    savedChunk.TokenCount = chunks[i].TokenCount;
                }

                totalTokens += chunks[i].TokenCount;
            }

            document.ChunkCount = chunks.Count;
            document.TokenCount = totalTokens;
            document.Status = "indexed";
            document.IndexedAt = DateTime.UtcNow;
            document.ProcessingError = null;

            await context.SaveChangesAsync();
            Console.WriteLine($"[Knowledge] Document {documentId} indexed with {chunks.Count} chunks via Semantic Kernel");
        }
        catch (Exception ex)
        {
            document.Status = "failed";
            document.ProcessingError = ex.Message;
            await context.SaveChangesAsync();
            Console.WriteLine($"[Knowledge] Document {documentId} processing failed: {ex.Message}");
            await _errorLogService.LogErrorAsync(ex, null, "Error");
            throw;
        }
    }

    public async Task<bool> ReprocessDocumentAsync(string documentId)
    {
        var document = await _context.KnowledgeDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId && !d.IsDeleted);

        if (document == null) return false;

        document.Status = "pending";
        document.ProcessingError = null;
        await _context.SaveChangesAsync();

        // Process in background
        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessDocumentAsync(document.Id);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Knowledge] Reprocessing failed for {document.Id}: {ex.Message}");
                await _errorLogService.LogErrorAsync(ex, null, "Error");
            }
        });

        return true;
    }

    #endregion

    #region Search and RAG

    public async Task<List<KnowledgeSearchResult>> SearchKnowledgeAsync(string siteId, KnowledgeSearchRequest request)
    {
        var memory = await _kernelFactory.CreateMemoryAsync();
        var results = new List<KnowledgeSearchResult>();

        await foreach (var result in memory.SearchAsync(
            collection: siteId,
            query: request.Query,
            limit: request.MaxResults,
            minRelevanceScore: request.MinSimilarity))
        {
            results.Add(new KnowledgeSearchResult(
                result.Metadata.Id,
                result.Metadata.AdditionalMetadata ?? string.Empty,
                result.Metadata.Description ?? "Unknown",
                result.Metadata.Text,
                result.Relevance,
                0 // ChunkIndex not available from memory search; could be enriched if needed
            ));
        }

        return results;
    }

    public async Task<AnalyzeMessageWithRagResponse> AnalyzeMessageWithRagAsync(AnalyzeMessageWithRagRequest request)
    {
        try
        {
            var kernel = await _kernelFactory.CreateKernelAsync();
            var memory = await _kernelFactory.CreateMemoryAsync();

            // Step 1: Rewrite query for better retrieval
            var rewrittenQuery = await _queryRewritePlugin.RewriteQueryAsync(kernel, request.Message);

            // Step 2: Search knowledge base via SK memory
            var searchResultsJson = await _knowledgeSearchPlugin.SearchKnowledgeBaseAsync(
                memory, rewrittenQuery, request.SiteId, request.MaxChunks, request.MinSimilarity);

            // Parse search results for building relevantKnowledge
            var searchResults = JsonSerializer.Deserialize<List<SearchResultItem>>(searchResultsJson)
                ?? new List<SearchResultItem>();

            var relevantKnowledge = searchResults.Select(r => new KnowledgeContextDto(
                r.documentTitle ?? "Unknown",
                r.content ?? string.Empty,
                r.similarity
            )).ToList();

            // If no relevant knowledge found, return polite decline without calling GPT
            if (!searchResults.Any())
            {
                return new AnalyzeMessageWithRagResponse(
                    "I don't have specific information about that topic. A support agent will be with you shortly to help.",
                    "Medium",
                    50,
                    null,
                    "Wait for human agent",
                    "neutral",
                    "out_of_scope",
                    null,
                    relevantKnowledge,
                    false
                );
            }

            // Step 3: Generate structured response via SK plugin
            var responseJson = await _responseGenerationPlugin.GenerateRagResponseAsync(
                kernel, request.Message, searchResultsJson);

            var result = JsonSerializer.Deserialize<JsonElement>(responseJson);

            // Parse conversion_percentage safely (handle both number and string)
            int conversionPct = 50;
            if (result.TryGetProperty("conversion_percentage", out var conversion))
            {
                if (conversion.ValueKind == JsonValueKind.Number)
                    conversionPct = conversion.GetInt32();
                else if (conversion.ValueKind == JsonValueKind.String && int.TryParse(conversion.GetString(), out var parsed))
                    conversionPct = parsed;
            }

            return new AnalyzeMessageWithRagResponse(
                result.TryGetProperty("suggested_reply", out var reply) ? reply.GetString() ?? "" : "",
                result.TryGetProperty("interest_level", out var interest) ? interest.GetString() ?? "Medium" : "Medium",
                conversionPct,
                result.TryGetProperty("objection", out var objection) ? objection.GetString() : null,
                result.TryGetProperty("next_action", out var action) ? action.GetString() ?? "" : "",
                result.TryGetProperty("sentiment", out var sentiment) ? sentiment.GetString() : null,
                result.TryGetProperty("intent", out var intent) ? intent.GetString() : null,
                result.TryGetProperty("keywords", out var keywords)
                    ? JsonSerializer.Deserialize<List<string>>(keywords.GetRawText())
                    : null,
                relevantKnowledge,
                searchResults.Any()
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RAG] Error in SK pipeline: {ex.Message}");
            await _errorLogService.LogErrorAsync(ex, null, "Warning");
            return new AnalyzeMessageWithRagResponse(
                "Thank you for your message. How can I help you further?",
                "Medium",
                50,
                null,
                "Continue the conversation",
                "neutral",
                null,
                null,
                new List<KnowledgeContextDto>(),
                false
            );
        }
    }

    // Internal DTO for deserializing search plugin results
    private record SearchResultItem(
        string? chunkId,
        string? documentId,
        string? documentTitle,
        string? content,
        double similarity);

    #endregion

    #region Stats

    public async Task<KnowledgeStatsDto> GetStatsAsync(string siteId)
    {
        var documents = await _context.KnowledgeDocuments
            .Where(d => d.SiteId == siteId && !d.IsDeleted)
            .ToListAsync();

        return new KnowledgeStatsDto(
            documents.Count,
            documents.Count(d => d.Status == "indexed"),
            documents.Count(d => d.Status == "processing"),
            documents.Count(d => d.Status == "failed"),
            documents.Count(d => d.Status == "pending"),
            documents.Sum(d => d.ChunkCount),
            documents.Sum(d => d.TokenCount)
        );
    }

    #endregion

    #region Private Helpers

    private static KnowledgeDocumentDto MapToDto(KnowledgeDocument document)
    {
        return new KnowledgeDocumentDto(
            document.Id,
            document.SiteId,
            document.Title,
            document.Description,
            document.DocumentType,
            document.OriginalFileName,
            document.FileSize,
            document.Status,
            document.ProcessingError,
            document.ChunkCount,
            document.TokenCount,
            document.CreatedAt,
            document.UpdatedAt,
            document.IndexedAt
        );
    }

    private async Task<string> ExtractTextFromFileAsync(KnowledgeDocument document)
    {
        if (string.IsNullOrEmpty(document.FilePath) || !File.Exists(document.FilePath))
        {
            return document.RawContent ?? "";
        }

        return document.DocumentType switch
        {
            "pdf" => ExtractTextFromPdf(document.FilePath),
            "docx" => ExtractTextFromDocx(document.FilePath),
            "txt" => await File.ReadAllTextAsync(document.FilePath),
            _ => document.RawContent ?? ""
        };
    }

    private static string ExtractTextFromPdf(string filePath)
    {
        var sb = new StringBuilder();
        using var document = PdfDocument.Open(filePath);

        foreach (var page in document.GetPages())
        {
            sb.AppendLine(page.Text);
        }

        return sb.ToString();
    }

    private static string ExtractTextFromDocx(string filePath)
    {
        var sb = new StringBuilder();
        using var document = WordprocessingDocument.Open(filePath, false);

        var body = document.MainDocumentPart?.Document.Body;
        if (body != null)
        {
            sb.Append(body.InnerText);
        }

        return sb.ToString();
    }

    private record ChunkInfo(string Text, int StartOffset, int EndOffset, int TokenCount);

    private static List<ChunkInfo> ChunkText(string text)
    {
        var chunks = new List<ChunkInfo>();
        var words = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        // Approximate tokens (roughly 0.75 words per token for English)
        var tokensPerWord = 1.33;
        var wordsPerChunk = (int)(CHUNK_SIZE_TOKENS / tokensPerWord);
        var overlapWords = (int)(CHUNK_OVERLAP_TOKENS / tokensPerWord);

        var currentIndex = 0;
        var textIndex = 0;

        while (currentIndex < words.Length)
        {
            var endIndex = Math.Min(currentIndex + wordsPerChunk, words.Length);
            var chunkWords = words.Skip(currentIndex).Take(endIndex - currentIndex).ToArray();
            var chunkText = string.Join(" ", chunkWords);

            var startOffset = textIndex;
            var endOffset = startOffset + chunkText.Length;
            var tokenCount = (int)(chunkWords.Length * tokensPerWord);

            chunks.Add(new ChunkInfo(chunkText, startOffset, endOffset, tokenCount));

            textIndex = endOffset + 1;
            currentIndex = endIndex - overlapWords;

            if (currentIndex >= words.Length - overlapWords)
                break;
        }

        return chunks;
    }

    #endregion
}
