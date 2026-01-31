using System.ComponentModel.DataAnnotations.Schema;

namespace ChatApp.API.Models.Entities;

public class DemoRequest : BaseEntity
{
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Column("company")]
    public string Company { get; set; } = string.Empty;

    [Column("phone")]
    public string? Phone { get; set; }

    [Column("message")]
    public string? Message { get; set; }

    [Column("status")]
    public string Status { get; set; } = "pending";

    [Column("admin_notes")]
    public string? AdminNotes { get; set; }

    [Column("ip_address")]
    public string? IpAddress { get; set; }

    [Column("user_agent")]
    public string? UserAgent { get; set; }
}
