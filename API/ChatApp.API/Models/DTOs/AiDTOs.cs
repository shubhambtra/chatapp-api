namespace ChatApp.API.Models.DTOs;

public record AnalyzeConversationRequest(string ConversationId);

public record AnalyzeConversationResponse(
    string ConversationId,
    string? Summary,
    string? Sentiment,
    double? SentimentScore,
    List<string>? Topics,
    string? Intent,
    string? Language,
    double? UrgencyScore,
    List<string>? SuggestedResponses,
    List<string>? KeyPhrases,
    DateTime AnalyzedAt
);

// Message analysis for real-time chat (used by main.py)
public record AnalyzeMessageRequest(
    string Message,
    string? ConversationId = null,
    string? VisitorId = null
);

public record AnalyzeMessageResponse(
    string SuggestedReply,
    string InterestLevel,
    int ConversionPercentage,
    string? Objection,
    string NextAction,
    string? Sentiment,
    string? Intent,
    List<string>? Keywords
);

public record GenerateResponseRequest(
    string ConversationId,
    string? Tone,
    string? Context
);

public record GenerateResponseResponse(
    string SuggestedResponse,
    double Confidence,
    string? Reasoning
);

public record SummarizeConversationRequest(string ConversationId);

public record SummarizeConversationResponse(
    string ConversationId,
    string Summary,
    int MessageCount,
    TimeSpan Duration
);
