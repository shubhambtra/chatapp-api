namespace ChatApp.API.Models.Entities;

public class ChatFile : BaseEntity
{
    public string SiteId { get; set; } = string.Empty;
    public Site Site { get; set; } = null!;

    // Uploader
    public string UploaderType { get; set; } = string.Empty; // visitor, agent
    public string UploaderId { get; set; } = string.Empty;

    // File info
    public string OriginalName { get; set; } = string.Empty;
    public string StoredName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FilePath { get; set; } = string.Empty;

    // Optional thumbnail for images
    public string? ThumbnailPath { get; set; }

    // Metadata
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? Duration { get; set; } // for video/audio

    // Status
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
