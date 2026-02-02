using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using ChatApp.API.Data;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Models.Entities;
using ChatApp.API.Services.Interfaces;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;

namespace ChatApp.API.Services.Implementations;

public class KnowledgeService : IKnowledgeService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly IWebHostEnvironment _environment;

    private const int CHUNK_SIZE_TOKENS = 500;
    private const int CHUNK_OVERLAP_TOKENS = 50;
    private const int EMBEDDING_DIMENSIONS = 1536; // text-embedding-3-small

    private async Task<(string? ApiKey, string Model)> GetOpenAiSettingsAsync()
    {
        var settings = await _context.AppConfigurations.FirstOrDefaultAsync();
        var apiKey = settings?.OpenAiApiKey;
        var model = settings?.OpenAiModel ?? "gpt-4o-mini";
        return (apiKey, model);
    }

    public KnowledgeService(
        ApplicationDbContext context,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IWebHostEnvironment environment)
    {
        _context = context;
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient("OpenAI");
        _environment = environment;
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

            // Generate embeddings and save chunks
            for (int i = 0; i < chunks.Count; i++)
            {
                var chunkText = chunks[i].Text;
                var embedding = await GenerateEmbeddingAsync(chunkText);

                var chunk = new KnowledgeChunk
                {
                    Id = Guid.NewGuid().ToString(),
                    DocumentId = documentId,
                    SiteId = document.SiteId,
                    Content = chunkText,
                    ChunkIndex = i,
                    StartOffset = chunks[i].StartOffset,
                    EndOffset = chunks[i].EndOffset,
                    TokenCount = chunks[i].TokenCount,
                    EmbeddingJson = JsonSerializer.Serialize(embedding),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                context.KnowledgeChunks.Add(chunk);
                totalTokens += chunks[i].TokenCount;
            }

            document.ChunkCount = chunks.Count;
            document.TokenCount = totalTokens;
            document.Status = "indexed";
            document.IndexedAt = DateTime.UtcNow;
            document.ProcessingError = null;

            await context.SaveChangesAsync();
            Console.WriteLine($"[Knowledge] Document {documentId} indexed with {chunks.Count} chunks");
        }
        catch (Exception ex)
        {
            document.Status = "failed";
            document.ProcessingError = ex.Message;
            await context.SaveChangesAsync();
            Console.WriteLine($"[Knowledge] Document {documentId} processing failed: {ex.Message}");
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
            }
        });

        return true;
    }

    #endregion

    #region Search and RAG

    public async Task<List<KnowledgeSearchResult>> SearchKnowledgeAsync(string siteId, KnowledgeSearchRequest request)
    {
        // Generate query embedding
        var queryEmbedding = await GenerateEmbeddingAsync(request.Query);

        // Get all chunks for the site
        var chunks = await _context.KnowledgeChunks
            .Include(c => c.Document)
            .Where(c => c.SiteId == siteId && !c.Document!.IsDeleted && c.Document.Status == "indexed")
            .ToListAsync();

        // Calculate similarities
        var results = new List<(KnowledgeChunk Chunk, double Similarity)>();

        foreach (var chunk in chunks)
        {
            if (string.IsNullOrEmpty(chunk.EmbeddingJson)) continue;

            var chunkEmbedding = JsonSerializer.Deserialize<float[]>(chunk.EmbeddingJson);
            if (chunkEmbedding == null) continue;

            var similarity = CosineSimilarity(queryEmbedding, chunkEmbedding);
            if (similarity >= request.MinSimilarity)
            {
                results.Add((chunk, similarity));
            }
        }

        // Return top results
        return results
            .OrderByDescending(r => r.Similarity)
            .Take(request.MaxResults)
            .Select(r => new KnowledgeSearchResult(
                r.Chunk.Id,
                r.Chunk.DocumentId,
                r.Chunk.Document?.Title ?? "Unknown",
                r.Chunk.Content,
                r.Similarity,
                r.Chunk.ChunkIndex
            ))
            .ToList();
    }

    public async Task<AnalyzeMessageWithRagResponse> AnalyzeMessageWithRagAsync(AnalyzeMessageWithRagRequest request)
    {
        // Search for relevant knowledge
        var searchResults = await SearchKnowledgeAsync(request.SiteId, new KnowledgeSearchRequest(
            request.Message,
            request.MaxChunks,
            request.MinSimilarity
        ));

        var relevantKnowledge = searchResults.Select(r => new KnowledgeContextDto(
            r.DocumentTitle,
            r.Content,
            r.Similarity
        )).ToList();

        // If no relevant knowledge found, return a polite decline without calling GPT
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

        // Note: We let GPT decide if the content is relevant enough to answer
        // The similarity threshold already filters obviously unrelated content

        // Build context for GPT
        var contextBuilder = new StringBuilder();
        contextBuilder.AppendLine("KNOWLEDGE BASE (You MUST only use this information to answer):");
        foreach (var result in searchResults)
        {
            contextBuilder.AppendLine($"---");
            contextBuilder.AppendLine($"Source: {result.DocumentTitle}");
            contextBuilder.AppendLine(result.Content);
        }
        contextBuilder.AppendLine("---");

        // Call GPT with augmented prompt
        var openAiSettings = await GetOpenAiSettingsAsync();
        var model = openAiSettings.Model;

        var systemPrompt = @"You are a customer support assistant with STRICT limitations. You can ONLY answer questions using the EXACT information provided in the knowledge base content below.

CRITICAL RULES - YOU MUST FOLLOW THESE:
1. ONLY use information that is EXPLICITLY stated in the provided knowledge base content
2. If the customer's question is NOT DIRECTLY answered by the knowledge base content, you MUST set suggested_reply to: 'I don't have specific information about that topic. A support agent will be with you shortly to help.'
3. NEVER use your general knowledge or training data to answer questions
4. NEVER make assumptions or inferences beyond what is explicitly written in the knowledge base
5. If the question is about a completely different topic than what's in the knowledge base (e.g., asking about cooking when KB is about software), always decline to answer
6. Be honest - if you're not sure if the KB answers the question, decline to answer

EXAMPLES OF WHEN TO DECLINE:
- Customer asks 'how to make tea' but KB is about product features → DECLINE
- Customer asks about pricing but KB only has technical docs → DECLINE
- Customer asks something vaguely related but KB doesn't have the specific answer → DECLINE";

        var prompt = $@"Analyze this customer message and respond ONLY if the knowledge base below contains a DIRECT answer.

{contextBuilder}

Customer message: ""{request.Message}""

IMPORTANT: First check if the knowledge base content above DIRECTLY answers this question. If NOT, set suggested_reply to the decline message.

Return ONLY valid JSON:
{{
  ""suggested_reply"": ""<answer from KB OR decline message if KB doesn't cover this topic>"",
  ""interest_level"": ""Low | Medium | High"",
  ""conversion_percentage"": <number 0-100>,
  ""objection"": ""<any objection detected or empty string>"",
  ""next_action"": ""<recommended action>"",
  ""sentiment"": ""positive | negative | neutral"",
  ""intent"": ""<customer intent>"",
  ""keywords"": [""<relevant keywords>""]
}}";

        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = prompt }
        };

        try
        {
            var responseContent = await CallOpenAiAsync(model, messages);
            var result = JsonSerializer.Deserialize<JsonElement>(responseContent);

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
            Console.WriteLine($"[RAG] Error processing GPT response: {ex.Message}");
            return new AnalyzeMessageWithRagResponse(
                "Thank you for your message. How can I help you further?",
                "Medium",
                50,
                null,
                "Continue the conversation",
                "neutral",
                null,
                null,
                relevantKnowledge,
                searchResults.Any()
            );
        }
    }

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

    private async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        var settings = await GetOpenAiSettingsAsync();
        var apiKey = settings.ApiKey;

        var requestBody = new
        {
            model = "text-embedding-3-small",
            input = text
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/embeddings");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json"
        );

        var response = await _httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OpenAI API error: {responseContent}");
        }

        var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
        var embeddingArray = jsonResponse.GetProperty("data")[0].GetProperty("embedding");

        var embedding = new float[EMBEDDING_DIMENSIONS];
        var index = 0;
        foreach (var value in embeddingArray.EnumerateArray())
        {
            embedding[index++] = value.GetSingle();
        }

        return embedding;
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        double dotProduct = 0;
        double magnitudeA = 0;
        double magnitudeB = 0;

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

    private async Task<string> CallOpenAiAsync(string model, List<object> messages)
    {
        var settings = await GetOpenAiSettingsAsync();
        var apiKey = settings.ApiKey;

        var requestBody = new
        {
            model = model,
            messages = messages,
            temperature = 0.7
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json"
        );

        var response = await _httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OpenAI API error: {responseContent}");
        }

        var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
        var content = jsonResponse.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";

        // Strip markdown code blocks if present
        content = content.Trim();
        if (content.StartsWith("```json"))
        {
            content = content.Substring(7);
        }
        else if (content.StartsWith("```"))
        {
            content = content.Substring(3);
        }
        if (content.EndsWith("```"))
        {
            content = content.Substring(0, content.Length - 3);
        }

        return content.Trim();
    }

    #endregion
}
