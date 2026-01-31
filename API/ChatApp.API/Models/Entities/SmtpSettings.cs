using System.ComponentModel.DataAnnotations.Schema;

namespace ChatApp.API.Models.Entities;

/// <summary>
/// Platform-wide SMTP/Email settings (single row table)
/// </summary>
public class SmtpSettings : BaseEntity
{
    [Column("smtp_host")]
    public string SmtpHost { get; set; } = "smtp.gmail.com";

    [Column("smtp_port")]
    public int SmtpPort { get; set; } = 587;

    [Column("smtp_username")]
    public string SmtpUsername { get; set; } = string.Empty;

    [Column("smtp_password")]
    public string SmtpPassword { get; set; } = string.Empty;

    [Column("from_email")]
    public string FromEmail { get; set; } = "noreply@chatapp.com";

    [Column("from_name")]
    public string FromName { get; set; } = "ChatApp";

    [Column("enable_ssl")]
    public bool EnableSsl { get; set; } = true;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;
}
