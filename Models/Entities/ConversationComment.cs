namespace ChatApp.API.Models.Entities;

public class ConversationComment : BaseEntity
{
    public string ConversationId { get; set; } = string.Empty;
    public Conversation Conversation { get; set; } = null!;

    public string AuthorId { get; set; } = string.Empty;
    public User? Author { get; set; }

    public string? AuthorName { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Mentions { get; set; } = "[]"; // JSON array of mentioned user IDs
}
