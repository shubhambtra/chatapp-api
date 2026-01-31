namespace ChatApp.API.Models.Entities;

public class Conversation : BaseEntity
{
    public string SiteId { get; set; } = string.Empty;
    public Site Site { get; set; } = null!;

    public string VisitorId { get; set; } = string.Empty;
    public Visitor Visitor { get; set; } = null!;

    public string? AssignedUserId { get; set; }
    public User? AssignedUser { get; set; }

    // Status
    public string Status { get; set; } = "active";
    public string Priority { get; set; } = "normal";
    public string Channel { get; set; } = "widget";

    // Subject/Topic
    public string? Subject { get; set; }

    // Metadata
    public string Tags { get; set; } = "[]";
    public string CustomData { get; set; } = "{}";

    // Tracking
    public DateTime? FirstResponseAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? LastMessageAt { get; set; }

    // Statistics
    public int MessageCount { get; set; }
    public int? Rating { get; set; }
    public string? Feedback { get; set; }

    // Resolution
    public string? ResolutionStatus { get; set; }  // resolved, unresolved, spam, no-response
    public string? ClosingNote { get; set; }

    // Navigation properties
    public ICollection<Message> Messages { get; set; } = new List<Message>();
    public ConversationAnalysis? Analysis { get; set; }
}

public class ConversationAnalysis : BaseEntityWithIntId
{
    public string ConversationId { get; set; } = string.Empty;
    public Conversation Conversation { get; set; } = null!;

    public string? Summary { get; set; }
    public string? Sentiment { get; set; }
    public string? Topics { get; set; }
    public string? Intent { get; set; }
    public string? Language { get; set; }
    public double? SentimentScore { get; set; }
    public double? UrgencyScore { get; set; }
    public string? SuggestedResponses { get; set; }
    public string? KeyPhrases { get; set; }

    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}
