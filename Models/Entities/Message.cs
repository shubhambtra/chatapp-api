namespace ChatApp.API.Models.Entities;

public class Message : BaseEntity
{
    public string ConversationId { get; set; } = string.Empty;
    public Conversation Conversation { get; set; } = null!;

    // Sender
    public string SenderType { get; set; } = string.Empty; // visitor, agent, system, ai
    public string? SenderId { get; set; }

    // Content
    public string Content { get; set; } = string.Empty;
    public string MessageType { get; set; } = "text"; // text, file, image, system

    // File attachment
    public string? FileId { get; set; }
    public ChatFile? File { get; set; }

    // Metadata
    public string? Metadata { get; set; }

    // Status
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public bool IsEdited { get; set; }
    public DateTime? EditedAt { get; set; }

    // Navigation properties
    public ICollection<MessageRead> ReadReceipts { get; set; } = new List<MessageRead>();
}

public class MessageRead : BaseEntityWithIntId
{
    public string MessageId { get; set; } = string.Empty;
    public Message Message { get; set; } = null!;

    public string ReaderType { get; set; } = string.Empty; // visitor, agent
    public string ReaderId { get; set; } = string.Empty;

    public DateTime ReadAt { get; set; } = DateTime.UtcNow;
}
