using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ChatApp.API.Data;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Models.Entities;
using ChatApp.API.Services.Interfaces;

namespace ChatApp.API.Services.Implementations;

public class AiService : IAiService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly IKnowledgeService _knowledgeService;

    public AiService(ApplicationDbContext context, IConfiguration configuration, IHttpClientFactory httpClientFactory, IKnowledgeService knowledgeService)
    {
        _context = context;
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient("OpenAI");
        _knowledgeService = knowledgeService;
    }

    private async Task<(string? ApiKey, string Model)> GetOpenAiSettingsAsync()
    {
        var settings = await _context.AppConfigurations.FirstOrDefaultAsync();
        var apiKey = settings?.OpenAiApiKey;
        var model = settings?.OpenAiModel ?? "gpt-4o-mini";
        return (apiKey, model);
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

    public async Task<AnalyzeConversationResponse> AnalyzeConversationAsync(string conversationId)
    {
        var conversation = await _context.Conversations
            .Include(c => c.Site)
            .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        if (conversation == null) throw new KeyNotFoundException("Conversation not found");

        if (!conversation.Site.AiEnabled)
        {
            throw new InvalidOperationException("AI is not enabled for this site");
        }

        var chatMessages = conversation.Messages
            .Where(m => !m.IsDeleted)
            .Select(m => $"{m.SenderType}: {m.Content}")
            .ToList();

        var transcript = string.Join("\n", chatMessages);
        var openAiSettings = await GetOpenAiSettingsAsync();
        var model = conversation.Site.AiModel ?? openAiSettings.Model;

        var prompt = $@"Analyze the following customer support conversation and provide:
1. A brief summary (2-3 sentences)
2. Overall sentiment (positive, negative, neutral, mixed)
3. Sentiment score (-1.0 to 1.0)
4. Main topics discussed (as JSON array)
5. Customer intent
6. Language
7. Urgency score (0.0 to 1.0)
8. 3 suggested responses for the agent (as JSON array)
9. Key phrases (as JSON array)

Conversation:
{transcript}

Respond in JSON format with keys: summary, sentiment, sentimentScore, topics, intent, language, urgencyScore, suggestedResponses, keyPhrases";

        var messages = new List<object>
        {
            new { role = "user", content = prompt }
        };

        var responseContent = await CallOpenAiAsync(model, messages);
        var analysisResult = JsonSerializer.Deserialize<JsonElement>(responseContent);

        var analysis = await _context.ConversationAnalyses
            .FirstOrDefaultAsync(ca => ca.ConversationId == conversationId);

        if (analysis == null)
        {
            analysis = new ConversationAnalysis { ConversationId = conversationId };
            _context.ConversationAnalyses.Add(analysis);
        }

        analysis.Summary = analysisResult.TryGetProperty("summary", out var summary) ? summary.GetString() : null;
        analysis.Sentiment = analysisResult.TryGetProperty("sentiment", out var sentiment) ? sentiment.GetString() : null;
        analysis.SentimentScore = analysisResult.TryGetProperty("sentimentScore", out var score) ? score.GetDouble() : null;
        analysis.Topics = analysisResult.TryGetProperty("topics", out var topics) ? topics.GetRawText() : null;
        analysis.Intent = analysisResult.TryGetProperty("intent", out var intent) ? intent.GetString() : null;
        analysis.Language = analysisResult.TryGetProperty("language", out var language) ? language.GetString() : null;
        analysis.UrgencyScore = analysisResult.TryGetProperty("urgencyScore", out var urgency) ? urgency.GetDouble() : null;
        analysis.SuggestedResponses = analysisResult.TryGetProperty("suggestedResponses", out var responses) ? responses.GetRawText() : null;
        analysis.KeyPhrases = analysisResult.TryGetProperty("keyPhrases", out var phrases) ? phrases.GetRawText() : null;
        analysis.AnalyzedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
            Console.WriteLine($"[AI] Analysis saved for conversation {conversationId}, Id: {analysis.Id}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AI] Failed to save analysis for conversation {conversationId}: {ex.Message}");
            throw;
        }

        return new AnalyzeConversationResponse(
            conversationId,
            analysis.Summary,
            analysis.Sentiment,
            analysis.SentimentScore,
            analysis.Topics != null ? JsonSerializer.Deserialize<List<string>>(analysis.Topics) : null,
            analysis.Intent,
            analysis.Language,
            analysis.UrgencyScore,
            analysis.SuggestedResponses != null ? JsonSerializer.Deserialize<List<string>>(analysis.SuggestedResponses) : null,
            analysis.KeyPhrases != null ? JsonSerializer.Deserialize<List<string>>(analysis.KeyPhrases) : null,
            analysis.AnalyzedAt
        );
    }

    public async Task<AnalyzeMessageResponse> AnalyzeMessageAsync(AnalyzeMessageRequest request)
    {
        var openAiSettings = await GetOpenAiSettingsAsync();
        var model = openAiSettings.Model;

        var prompt = $@"Return ONLY valid JSON.

{{
  ""suggested_reply"": """",
  ""interest_level"": ""Low | Medium | High"",
  ""conversion_percentage"": number,
  ""objection"": """",
  ""next_action"": """",
  ""sentiment"": ""positive | negative | neutral"",
  ""intent"": """",
  ""keywords"": []
}}

Customer message:
{request.Message}";

        var messages = new List<object>
        {
            new { role = "system", content = "You are a strict sales coach JSON API." },
            new { role = "user", content = prompt }
        };

        try
        {
            var responseContent = await CallOpenAiAsync(model, messages);
            var result = JsonSerializer.Deserialize<JsonElement>(responseContent);

            return new AnalyzeMessageResponse(
                result.TryGetProperty("suggested_reply", out var reply) ? reply.GetString() ?? "" : "",
                result.TryGetProperty("interest_level", out var interest) ? interest.GetString() ?? "Medium" : "Medium",
                result.TryGetProperty("conversion_percentage", out var conversion) ? conversion.GetInt32() : 50,
                result.TryGetProperty("objection", out var objection) ? objection.GetString() : null,
                result.TryGetProperty("next_action", out var action) ? action.GetString() ?? "" : "",
                result.TryGetProperty("sentiment", out var sentiment) ? sentiment.GetString() : null,
                result.TryGetProperty("intent", out var intent) ? intent.GetString() : null,
                result.TryGetProperty("keywords", out var keywords)
                    ? JsonSerializer.Deserialize<List<string>>(keywords.GetRawText())
                    : null
            );
        }
        catch
        {
            return new AnalyzeMessageResponse(
                "Thank you for your message. How can I help you further?",
                "Medium",
                50,
                null,
                "Continue the conversation",
                "neutral",
                null,
                null
            );
        }
    }

    public async Task<GenerateResponseResponse> GenerateResponseAsync(GenerateResponseRequest request)
    {
        var conversation = await _context.Conversations
            .Include(c => c.Site)
            .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(c => c.Id == request.ConversationId);

        if (conversation == null) throw new KeyNotFoundException("Conversation not found");

        if (!conversation.Site.AiEnabled)
        {
            throw new InvalidOperationException("AI is not enabled for this site");
        }

        var chatMessages = conversation.Messages
            .Where(m => !m.IsDeleted)
            .TakeLast(10)
            .Select(m => $"{m.SenderType}: {m.Content}")
            .ToList();

        var transcript = string.Join("\n", chatMessages);
        var openAiSettings = await GetOpenAiSettingsAsync();
        var model = conversation.Site.AiModel ?? openAiSettings.Model;

        // Get the last visitor message for knowledge base search
        var lastVisitorMessage = conversation.Messages
            .Where(m => !m.IsDeleted && m.SenderType == "visitor")
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefault();

        // Search knowledge base for relevant content
        var knowledgeContext = new StringBuilder();
        var hasKnowledge = false;

        if (lastVisitorMessage != null)
        {
            try
            {
                var searchResults = await _knowledgeService.SearchKnowledgeAsync(
                    conversation.SiteId,
                    new KnowledgeSearchRequest(lastVisitorMessage.Content, 5, 0.3)
                );

                if (searchResults.Any())
                {
                    hasKnowledge = true;
                    knowledgeContext.AppendLine("KNOWLEDGE BASE CONTENT (use this to answer):");
                    knowledgeContext.AppendLine("---");
                    foreach (var searchResult in searchResults)
                    {
                        knowledgeContext.AppendLine($"Source: {searchResult.DocumentTitle}");
                        knowledgeContext.AppendLine(searchResult.Content);
                        knowledgeContext.AppendLine("---");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI] Knowledge search failed: {ex.Message}");
            }
        }

        var toneInstruction = request.Tone != null ? $"Use a {request.Tone} tone." : "Use a professional and friendly tone.";
        var contextInstruction = request.Context != null ? $"Additional context: {request.Context}" : "";

        string systemPrompt;
        string prompt;

        if (hasKnowledge)
        {
            systemPrompt = @"You are a helpful customer support agent. You MUST answer questions ONLY based on the provided knowledge base content.
If the knowledge base does not contain relevant information to answer the question, politely say you don't have that information and suggest contacting support directly.
Do NOT make up information that is not in the knowledge base.";

            prompt = $@"{knowledgeContext}

{toneInstruction}
{contextInstruction}

Conversation:
{transcript}

Generate a helpful response based ONLY on the knowledge base content above. If the knowledge base doesn't have relevant information, say so politely.
Provide your confidence level (0.0 to 1.0) and brief reasoning.

Respond in JSON format with keys: response, confidence, reasoning";
        }
        else
        {
            systemPrompt = @"You are a helpful customer support agent. Since there is no knowledge base content available, provide a polite response acknowledging the customer's query and let them know a human agent will assist them shortly.";

            prompt = $@"{toneInstruction}
{contextInstruction}

Conversation:
{transcript}

Generate a polite holding response since no specific knowledge base information is available. Let the customer know their query has been received.
Provide your confidence level (0.0 to 1.0) and brief reasoning.

Respond in JSON format with keys: response, confidence, reasoning";
        }

        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = prompt }
        };

        var responseContent = await CallOpenAiAsync(model, messages);
        var result = JsonSerializer.Deserialize<JsonElement>(responseContent);

        return new GenerateResponseResponse(
            result.TryGetProperty("response", out var response) ? response.GetString()! : "",
            result.TryGetProperty("confidence", out var confidence) ? confidence.GetDouble() : 0.8,
            result.TryGetProperty("reasoning", out var reasoning) ? reasoning.GetString() : null
        );
    }

    public async Task<SummarizeConversationResponse> SummarizeConversationAsync(string conversationId)
    {
        var conversation = await _context.Conversations
            .Include(c => c.Site)
            .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        if (conversation == null) throw new KeyNotFoundException("Conversation not found");

        if (!conversation.Site.AiEnabled)
        {
            throw new InvalidOperationException("AI is not enabled for this site");
        }

        var chatMessages = conversation.Messages.Where(m => !m.IsDeleted).ToList();
        var transcript = string.Join("\n", chatMessages.Select(m => $"{m.SenderType}: {m.Content}"));
        var openAiSettings = await GetOpenAiSettingsAsync();
        var model = conversation.Site.AiModel ?? openAiSettings.Model;

        var prompt = $@"Summarize the following customer support conversation in 2-3 sentences, highlighting the main issue and resolution (if any):

{transcript}";

        var messages = new List<object>
        {
            new { role = "user", content = prompt }
        };

        var summaryText = await CallOpenAiAsync(model, messages);

        var duration = chatMessages.Count > 0
            ? chatMessages.Last().CreatedAt - chatMessages.First().CreatedAt
            : TimeSpan.Zero;

        return new SummarizeConversationResponse(
            conversationId,
            summaryText,
            chatMessages.Count,
            duration
        );
    }
}
