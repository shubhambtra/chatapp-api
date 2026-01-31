namespace ChatApp.API.Models.Entities;

public class Notification : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public User User { get; set; } = null!;

    public string? SiteId { get; set; }
    public Site? Site { get; set; }

    public string Type { get; set; } = string.Empty; // new_conversation, new_message, assignment, mention, etc.
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? ActionUrl { get; set; }
    public string? Data { get; set; }

    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }

    // Optional link to conversation
    public string? ConversationId { get; set; }
    public Conversation? Conversation { get; set; }
}
