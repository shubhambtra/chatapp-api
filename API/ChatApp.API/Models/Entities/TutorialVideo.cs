using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChatApp.API.Models.Entities;

/// <summary>
/// Tutorial/help videos for the platform
/// </summary>
public class TutorialVideo : BaseEntity
{
    [Required]
    [Column("title")]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Column("description")]
    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    [Column("youtube_url")]
    [MaxLength(500)]
    public string YouTubeUrl { get; set; } = string.Empty;

    [Column("thumbnail_url")]
    [MaxLength(500)]
    public string? ThumbnailUrl { get; set; }

    [Column("duration")]
    [MaxLength(20)]
    public string? Duration { get; set; }

    [Column("category")]
    [MaxLength(100)]
    public string? Category { get; set; }

    [Column("display_order")]
    public int DisplayOrder { get; set; } = 0;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("is_featured")]
    public bool IsFeatured { get; set; } = false;
}
