using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChatApp.API.Data;
using ChatApp.API.Models.Entities;
using ChatApp.API.Models.DTOs;

namespace ChatApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TutorialVideosController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public TutorialVideosController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get all active tutorial videos (public)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<TutorialVideoDto>>>> GetAll([FromQuery] string? category = null)
    {
        var query = _context.TutorialVideos
            .Where(v => v.IsActive)
            .OrderBy(v => v.DisplayOrder)
            .ThenByDescending(v => v.CreatedAt)
            .AsQueryable();

        if (!string.IsNullOrEmpty(category))
        {
            query = query.Where(v => v.Category == category);
        }

        var videos = await query.ToListAsync();
        var dtos = videos.Select(MapToDto).ToList();

        return Ok(ApiResponse<List<TutorialVideoDto>>.Ok(dtos));
    }

    /// <summary>
    /// Get featured videos (public)
    /// </summary>
    [HttpGet("featured")]
    public async Task<ActionResult<ApiResponse<List<TutorialVideoDto>>>> GetFeatured()
    {
        var videos = await _context.TutorialVideos
            .Where(v => v.IsActive && v.IsFeatured)
            .OrderBy(v => v.DisplayOrder)
            .Take(5)
            .ToListAsync();

        var dtos = videos.Select(MapToDto).ToList();
        return Ok(ApiResponse<List<TutorialVideoDto>>.Ok(dtos));
    }

    /// <summary>
    /// Get all categories (public)
    /// </summary>
    [HttpGet("categories")]
    public async Task<ActionResult<ApiResponse<List<string>>>> GetCategories()
    {
        var categories = await _context.TutorialVideos
            .Where(v => v.IsActive && !string.IsNullOrEmpty(v.Category))
            .Select(v => v.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        return Ok(ApiResponse<List<string>>.Ok(categories));
    }

    /// <summary>
    /// Get a single video by ID (public)
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<TutorialVideoDto>>> GetById(string id)
    {
        var video = await _context.TutorialVideos.FindAsync(id);
        if (video == null || !video.IsActive)
        {
            return NotFound(ApiResponse<TutorialVideoDto>.Fail("Video not found"));
        }

        return Ok(ApiResponse<TutorialVideoDto>.Ok(MapToDto(video)));
    }

    /// <summary>
    /// Get all videos including inactive (admin only)
    /// </summary>
    [HttpGet("admin/all")]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<List<TutorialVideoDto>>>> GetAllAdmin()
    {
        var videos = await _context.TutorialVideos
            .OrderBy(v => v.DisplayOrder)
            .ThenByDescending(v => v.CreatedAt)
            .ToListAsync();

        var dtos = videos.Select(MapToDto).ToList();
        return Ok(ApiResponse<List<TutorialVideoDto>>.Ok(dtos));
    }

    /// <summary>
    /// Create a new tutorial video (admin only)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<TutorialVideoDto>>> Create([FromBody] CreateTutorialVideoRequest request)
    {
        var video = new TutorialVideo
        {
            Title = request.Title,
            Description = request.Description,
            YouTubeUrl = request.YouTubeUrl,
            ThumbnailUrl = request.ThumbnailUrl ?? ExtractYouTubeThumbnail(request.YouTubeUrl),
            Duration = request.Duration,
            Category = request.Category,
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive,
            IsFeatured = request.IsFeatured
        };

        _context.TutorialVideos.Add(video);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<TutorialVideoDto>.Ok(MapToDto(video), "Video created successfully"));
    }

    /// <summary>
    /// Update a tutorial video (admin only)
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<TutorialVideoDto>>> Update(string id, [FromBody] UpdateTutorialVideoRequest request)
    {
        var video = await _context.TutorialVideos.FindAsync(id);
        if (video == null)
        {
            return NotFound(ApiResponse<TutorialVideoDto>.Fail("Video not found"));
        }

        if (request.Title != null) video.Title = request.Title;
        if (request.Description != null) video.Description = request.Description;
        if (request.YouTubeUrl != null)
        {
            video.YouTubeUrl = request.YouTubeUrl;
            // Update thumbnail if URL changed and no custom thumbnail
            if (request.ThumbnailUrl == null)
            {
                video.ThumbnailUrl = ExtractYouTubeThumbnail(request.YouTubeUrl);
            }
        }
        if (request.ThumbnailUrl != null) video.ThumbnailUrl = request.ThumbnailUrl;
        if (request.Duration != null) video.Duration = request.Duration;
        if (request.Category != null) video.Category = request.Category;
        if (request.DisplayOrder.HasValue) video.DisplayOrder = request.DisplayOrder.Value;
        if (request.IsActive.HasValue) video.IsActive = request.IsActive.Value;
        if (request.IsFeatured.HasValue) video.IsFeatured = request.IsFeatured.Value;

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<TutorialVideoDto>.Ok(MapToDto(video), "Video updated successfully"));
    }

    /// <summary>
    /// Delete a tutorial video (admin only)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string id)
    {
        var video = await _context.TutorialVideos.FindAsync(id);
        if (video == null)
        {
            return NotFound(ApiResponse<object>.Fail("Video not found"));
        }

        _context.TutorialVideos.Remove(video);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(null, "Video deleted successfully"));
    }

    /// <summary>
    /// Reorder videos (admin only)
    /// </summary>
    [HttpPost("reorder")]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<object>>> Reorder([FromBody] ReorderVideosRequest request)
    {
        foreach (var item in request.Items)
        {
            var video = await _context.TutorialVideos.FindAsync(item.Id);
            if (video != null)
            {
                video.DisplayOrder = item.Order;
            }
        }

        await _context.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(null, "Videos reordered successfully"));
    }

    private static TutorialVideoDto MapToDto(TutorialVideo video)
    {
        return new TutorialVideoDto
        {
            Id = video.Id,
            Title = video.Title,
            Description = video.Description,
            YouTubeUrl = video.YouTubeUrl,
            EmbedUrl = ConvertToEmbedUrl(video.YouTubeUrl),
            ThumbnailUrl = video.ThumbnailUrl,
            Duration = video.Duration,
            Category = video.Category,
            DisplayOrder = video.DisplayOrder,
            IsActive = video.IsActive,
            IsFeatured = video.IsFeatured,
            CreatedAt = video.CreatedAt,
            UpdatedAt = video.UpdatedAt
        };
    }

    private static string? ExtractYouTubeThumbnail(string youtubeUrl)
    {
        var videoId = ExtractVideoId(youtubeUrl);
        if (string.IsNullOrEmpty(videoId)) return null;
        return $"https://img.youtube.com/vi/{videoId}/maxresdefault.jpg";
    }

    private static string? ConvertToEmbedUrl(string youtubeUrl)
    {
        var videoId = ExtractVideoId(youtubeUrl);
        if (string.IsNullOrEmpty(videoId)) return null;
        return $"https://www.youtube.com/embed/{videoId}";
    }

    private static string? ExtractVideoId(string url)
    {
        if (string.IsNullOrEmpty(url)) return null;

        // Handle various YouTube URL formats
        // https://www.youtube.com/watch?v=VIDEO_ID
        // https://youtu.be/VIDEO_ID
        // https://www.youtube.com/embed/VIDEO_ID

        try
        {
            if (url.Contains("youtu.be/"))
            {
                var uri = new Uri(url);
                return uri.AbsolutePath.TrimStart('/').Split('?')[0];
            }

            if (url.Contains("youtube.com/embed/"))
            {
                var uri = new Uri(url);
                return uri.AbsolutePath.Replace("/embed/", "").Split('?')[0];
            }

            if (url.Contains("youtube.com/watch"))
            {
                var uri = new Uri(url);
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                return query["v"];
            }
        }
        catch
        {
            // Return null if parsing fails
        }

        return null;
    }
}

// DTOs
public record TutorialVideoDto
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string YouTubeUrl { get; init; } = string.Empty;
    public string? EmbedUrl { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string? Duration { get; init; }
    public string? Category { get; init; }
    public int DisplayOrder { get; init; }
    public bool IsActive { get; init; }
    public bool IsFeatured { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public record CreateTutorialVideoRequest
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string YouTubeUrl { get; init; } = string.Empty;
    public string? ThumbnailUrl { get; init; }
    public string? Duration { get; init; }
    public string? Category { get; init; }
    public int DisplayOrder { get; init; } = 0;
    public bool IsActive { get; init; } = true;
    public bool IsFeatured { get; init; } = false;
}

public record UpdateTutorialVideoRequest
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? YouTubeUrl { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string? Duration { get; init; }
    public string? Category { get; init; }
    public int? DisplayOrder { get; init; }
    public bool? IsActive { get; init; }
    public bool? IsFeatured { get; init; }
}

public record ReorderVideosRequest
{
    public List<ReorderItem> Items { get; init; } = new();
}

public record ReorderItem
{
    public string Id { get; init; } = string.Empty;
    public int Order { get; init; }
}
