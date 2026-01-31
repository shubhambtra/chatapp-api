using System.ComponentModel.DataAnnotations.Schema;

namespace ChatApp.API.Models.Entities;

/// <summary>
/// Contact form submissions from the website
/// </summary>
public class ContactSubmission : BaseEntity
{
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Column("subject")]
    public string? Subject { get; set; }

    [Column("message")]
    public string Message { get; set; } = string.Empty;

    [Column("is_read")]
    public bool IsRead { get; set; } = false;

    [Column("read_at")]
    public DateTime? ReadAt { get; set; }

    [Column("ip_address")]
    public string? IpAddress { get; set; }

    [Column("user_agent")]
    public string? UserAgent { get; set; }
}
