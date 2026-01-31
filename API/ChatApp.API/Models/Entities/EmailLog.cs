namespace ChatApp.API.Models.Entities;

public class EmailLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FromEmail { get; set; } = string.Empty;
    public string? FromName { get; set; }
    public string ToEmail { get; set; } = string.Empty;
    public string? ToName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsHtml { get; set; } = true;
    public string Status { get; set; } = "sent"; // sent, failed, pending
    public string? ErrorMessage { get; set; }
    public string? SiteId { get; set; }
    public string? UserId { get; set; }
    public string? EmailType { get; set; } // welcome, password_reset, subscription_expiry, etc.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }

    // Navigation properties
    public Site? Site { get; set; }
    public User? User { get; set; }
}
