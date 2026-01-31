using System.ComponentModel.DataAnnotations.Schema;

namespace ChatApp.API.Models.Entities;

public class IssueReport : BaseEntity
{
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Column("user_id")]
    public string? UserId { get; set; }

    [Column("site_id")]
    public string? SiteId { get; set; }

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("category")]
    public string Category { get; set; } = "general";

    [Column("priority")]
    public string Priority { get; set; } = "medium";

    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Column("status")]
    public string Status { get; set; } = "open";

    [Column("is_read")]
    public bool IsRead { get; set; } = false;

    [Column("read_at")]
    public DateTime? ReadAt { get; set; }

    [Column("admin_notes")]
    public string? AdminNotes { get; set; }

    [Column("ip_address")]
    public string? IpAddress { get; set; }

    [Column("user_agent")]
    public string? UserAgent { get; set; }

    // Navigation
    public ICollection<IssueReportAttachment> Attachments { get; set; } = new List<IssueReportAttachment>();
}

public class IssueReportAttachment : BaseEntity
{
    [Column("issue_report_id")]
    public string IssueReportId { get; set; } = string.Empty;

    [Column("original_name")]
    public string OriginalName { get; set; } = string.Empty;

    [Column("stored_name")]
    public string StoredName { get; set; } = string.Empty;

    [Column("mime_type")]
    public string MimeType { get; set; } = string.Empty;

    [Column("file_size")]
    public long FileSize { get; set; }

    [Column("file_path")]
    public string FilePath { get; set; } = string.Empty;

    // Navigation
    public IssueReport IssueReport { get; set; } = null!;
}
