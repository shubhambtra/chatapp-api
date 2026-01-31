using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChatApp.API.Data;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Models.Entities;

namespace ChatApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SiteSettingsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public SiteSettingsController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get platform site settings (public for branding display)
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<SiteSettingsDto>>> GetSettings()
    {
        var settings = await _context.SiteSettings.FirstOrDefaultAsync();

        if (settings == null)
        {
            // Return default settings if none exist
            return Ok(ApiResponse<SiteSettingsDto>.Ok(new SiteSettingsDto
            {
                SiteName = "ChatApp",
                SiteLogo = null,
                Favicon = null,
                CopyrightText = null,
                SeoTitle = null,
                SeoDescription = null,
                SeoKeywords = null,
                OgImage = null,
                FacebookUrl = null,
                TwitterUrl = null,
                LinkedInUrl = null,
                InstagramUrl = null,
                SupportEmail = null,
                SupportPhone = null,
                SupportAddress = null,
                // Feature flags default to enabled
                FeatureSupervisorMode = true,
                FeatureAiAnalysis = true,
                FeatureAiAutoReply = true,
                FeatureFileSharing = true,
                FeatureCsatRatings = true,
                FeatureVisitorInfo = true,
                FeatureCannedResponses = true,
                FeatureConversationTransfer = true,
                FeatureTeamChat = true,
                FeatureTypingIndicators = true,
                FeatureReadReceipts = true,
                FeatureInternalNotes = true,
                FeatureEmojiPicker = true,
                FeatureEmailSending = true,
                FeatureConversationSearch = true,
                FeatureMessageSearch = true,
                FeatureBulkActions = true,
                FeatureThemes = true,
                FeatureAgentStatus = true,
                FeatureNotifications = true
            }));
        }

        return Ok(ApiResponse<SiteSettingsDto>.Ok(MapToDto(settings)));
    }

    /// <summary>
    /// Update platform site settings (super_admin only)
    /// </summary>
    [HttpPut]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<SiteSettingsDto>>> UpdateSettings([FromBody] UpdateSiteSettingsRequest request)
    {
        var settings = await _context.SiteSettings.FirstOrDefaultAsync();

        if (settings == null)
        {
            // Create new settings if none exist
            settings = new SiteSettings();
            _context.SiteSettings.Add(settings);
        }

        // Update branding
        if (request.SiteName != null) settings.SiteName = request.SiteName;
        if (request.SiteLogo != null) settings.SiteLogo = request.SiteLogo;
        if (request.Favicon != null) settings.Favicon = request.Favicon;
        if (request.CopyrightText != null) settings.CopyrightText = request.CopyrightText;

        // Update SEO
        if (request.SeoTitle != null) settings.SeoTitle = request.SeoTitle;
        if (request.SeoDescription != null) settings.SeoDescription = request.SeoDescription;
        if (request.SeoKeywords != null) settings.SeoKeywords = request.SeoKeywords;
        if (request.OgImage != null) settings.OgImage = request.OgImage;

        // Update social media
        if (request.FacebookUrl != null) settings.FacebookUrl = request.FacebookUrl;
        if (request.TwitterUrl != null) settings.TwitterUrl = request.TwitterUrl;
        if (request.LinkedInUrl != null) settings.LinkedInUrl = request.LinkedInUrl;
        if (request.InstagramUrl != null) settings.InstagramUrl = request.InstagramUrl;

        // Update contact info
        if (request.SupportEmail != null) settings.SupportEmail = request.SupportEmail;
        if (request.SupportPhone != null) settings.SupportPhone = request.SupportPhone;
        if (request.SupportAddress != null) settings.SupportAddress = request.SupportAddress;

        // Update feature flags
        if (request.FeatureSupervisorMode.HasValue) settings.FeatureSupervisorMode = request.FeatureSupervisorMode.Value;
        if (request.FeatureAiAnalysis.HasValue) settings.FeatureAiAnalysis = request.FeatureAiAnalysis.Value;
        if (request.FeatureAiAutoReply.HasValue) settings.FeatureAiAutoReply = request.FeatureAiAutoReply.Value;
        if (request.FeatureFileSharing.HasValue) settings.FeatureFileSharing = request.FeatureFileSharing.Value;
        if (request.FeatureCsatRatings.HasValue) settings.FeatureCsatRatings = request.FeatureCsatRatings.Value;
        if (request.FeatureVisitorInfo.HasValue) settings.FeatureVisitorInfo = request.FeatureVisitorInfo.Value;
        if (request.FeatureCannedResponses.HasValue) settings.FeatureCannedResponses = request.FeatureCannedResponses.Value;
        if (request.FeatureConversationTransfer.HasValue) settings.FeatureConversationTransfer = request.FeatureConversationTransfer.Value;
        if (request.FeatureTeamChat.HasValue) settings.FeatureTeamChat = request.FeatureTeamChat.Value;
        if (request.FeatureTypingIndicators.HasValue) settings.FeatureTypingIndicators = request.FeatureTypingIndicators.Value;
        if (request.FeatureReadReceipts.HasValue) settings.FeatureReadReceipts = request.FeatureReadReceipts.Value;
        if (request.FeatureInternalNotes.HasValue) settings.FeatureInternalNotes = request.FeatureInternalNotes.Value;
        if (request.FeatureEmojiPicker.HasValue) settings.FeatureEmojiPicker = request.FeatureEmojiPicker.Value;
        if (request.FeatureEmailSending.HasValue) settings.FeatureEmailSending = request.FeatureEmailSending.Value;
        if (request.FeatureConversationSearch.HasValue) settings.FeatureConversationSearch = request.FeatureConversationSearch.Value;
        if (request.FeatureMessageSearch.HasValue) settings.FeatureMessageSearch = request.FeatureMessageSearch.Value;
        if (request.FeatureBulkActions.HasValue) settings.FeatureBulkActions = request.FeatureBulkActions.Value;
        if (request.FeatureThemes.HasValue) settings.FeatureThemes = request.FeatureThemes.Value;
        if (request.FeatureAgentStatus.HasValue) settings.FeatureAgentStatus = request.FeatureAgentStatus.Value;
        if (request.FeatureNotifications.HasValue) settings.FeatureNotifications = request.FeatureNotifications.Value;

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<SiteSettingsDto>.Ok(MapToDto(settings), "Settings updated successfully"));
    }

    /// <summary>
    /// Upload site logo (super_admin only)
    /// </summary>
    [HttpPost("logo")]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<string>>> UploadLogo(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<string>.Fail("No file provided"));
        }

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp", "image/svg+xml" };
        if (!allowedTypes.Contains(file.ContentType.ToLower()))
        {
            return BadRequest(ApiResponse<string>.Fail("Invalid file type. Only images are allowed."));
        }

        if (file.Length > 5 * 1024 * 1024) // 5MB max
        {
            return BadRequest(ApiResponse<string>.Fail("File size exceeds 5MB limit"));
        }

        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "branding");
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"logo_{DateTime.UtcNow.Ticks}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadsDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var logoUrl = $"/uploads/branding/{fileName}";

        // Update settings with new logo
        var settings = await _context.SiteSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new SiteSettings { SiteLogo = logoUrl };
            _context.SiteSettings.Add(settings);
        }
        else
        {
            settings.SiteLogo = logoUrl;
        }
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok(logoUrl, "Logo uploaded successfully"));
    }

    /// <summary>
    /// Upload favicon (super_admin only)
    /// </summary>
    [HttpPost("favicon")]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<string>>> UploadFavicon(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<string>.Fail("No file provided"));
        }

        var allowedTypes = new[] { "image/x-icon", "image/png", "image/svg+xml", "image/vnd.microsoft.icon" };
        if (!allowedTypes.Contains(file.ContentType.ToLower()))
        {
            return BadRequest(ApiResponse<string>.Fail("Invalid file type. Only icon images are allowed."));
        }

        if (file.Length > 1 * 1024 * 1024) // 1MB max
        {
            return BadRequest(ApiResponse<string>.Fail("File size exceeds 1MB limit"));
        }

        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "branding");
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"favicon_{DateTime.UtcNow.Ticks}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadsDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var faviconUrl = $"/uploads/branding/{fileName}";

        // Update settings with new favicon
        var settings = await _context.SiteSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new SiteSettings { Favicon = faviconUrl };
            _context.SiteSettings.Add(settings);
        }
        else
        {
            settings.Favicon = faviconUrl;
        }
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok(faviconUrl, "Favicon uploaded successfully"));
    }

    private static SiteSettingsDto MapToDto(SiteSettings settings)
    {
        return new SiteSettingsDto
        {
            Id = settings.Id,
            SiteName = settings.SiteName,
            SiteLogo = settings.SiteLogo,
            Favicon = settings.Favicon,
            CopyrightText = settings.CopyrightText,
            SeoTitle = settings.SeoTitle,
            SeoDescription = settings.SeoDescription,
            SeoKeywords = settings.SeoKeywords,
            OgImage = settings.OgImage,
            FacebookUrl = settings.FacebookUrl,
            TwitterUrl = settings.TwitterUrl,
            LinkedInUrl = settings.LinkedInUrl,
            InstagramUrl = settings.InstagramUrl,
            SupportEmail = settings.SupportEmail,
            SupportPhone = settings.SupportPhone,
            SupportAddress = settings.SupportAddress,
            // Feature flags
            FeatureSupervisorMode = settings.FeatureSupervisorMode,
            FeatureAiAnalysis = settings.FeatureAiAnalysis,
            FeatureAiAutoReply = settings.FeatureAiAutoReply,
            FeatureFileSharing = settings.FeatureFileSharing,
            FeatureCsatRatings = settings.FeatureCsatRatings,
            FeatureVisitorInfo = settings.FeatureVisitorInfo,
            FeatureCannedResponses = settings.FeatureCannedResponses,
            FeatureConversationTransfer = settings.FeatureConversationTransfer,
            FeatureTeamChat = settings.FeatureTeamChat,
            FeatureTypingIndicators = settings.FeatureTypingIndicators,
            FeatureReadReceipts = settings.FeatureReadReceipts,
            FeatureInternalNotes = settings.FeatureInternalNotes,
            FeatureEmojiPicker = settings.FeatureEmojiPicker,
            FeatureEmailSending = settings.FeatureEmailSending,
            FeatureConversationSearch = settings.FeatureConversationSearch,
            FeatureMessageSearch = settings.FeatureMessageSearch,
            FeatureBulkActions = settings.FeatureBulkActions,
            FeatureThemes = settings.FeatureThemes,
            FeatureAgentStatus = settings.FeatureAgentStatus,
            FeatureNotifications = settings.FeatureNotifications,
            UpdatedAt = settings.UpdatedAt
        };
    }
}

// DTOs
public class SiteSettingsDto
{
    public string? Id { get; set; }
    public string SiteName { get; set; } = "ChatApp";
    public string? SiteLogo { get; set; }
    public string? Favicon { get; set; }
    public string? CopyrightText { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public string? SeoKeywords { get; set; }
    public string? OgImage { get; set; }
    public string? FacebookUrl { get; set; }
    public string? TwitterUrl { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? InstagramUrl { get; set; }
    public string? SupportEmail { get; set; }
    public string? SupportPhone { get; set; }
    public string? SupportAddress { get; set; }
    // Feature flags
    public bool FeatureSupervisorMode { get; set; } = true;
    public bool FeatureAiAnalysis { get; set; } = true;
    public bool FeatureAiAutoReply { get; set; } = true;
    public bool FeatureFileSharing { get; set; } = true;
    public bool FeatureCsatRatings { get; set; } = true;
    public bool FeatureVisitorInfo { get; set; } = true;
    public bool FeatureCannedResponses { get; set; } = true;
    public bool FeatureConversationTransfer { get; set; } = true;
    public bool FeatureTeamChat { get; set; } = true;
    public bool FeatureTypingIndicators { get; set; } = true;
    public bool FeatureReadReceipts { get; set; } = true;
    public bool FeatureInternalNotes { get; set; } = true;
    public bool FeatureEmojiPicker { get; set; } = true;
    public bool FeatureEmailSending { get; set; } = true;
    public bool FeatureConversationSearch { get; set; } = true;
    public bool FeatureMessageSearch { get; set; } = true;
    public bool FeatureBulkActions { get; set; } = true;
    public bool FeatureThemes { get; set; } = true;
    public bool FeatureAgentStatus { get; set; } = true;
    public bool FeatureNotifications { get; set; } = true;
    public DateTime? UpdatedAt { get; set; }
}

public class UpdateSiteSettingsRequest
{
    public string? SiteName { get; set; }
    public string? SiteLogo { get; set; }
    public string? Favicon { get; set; }
    public string? CopyrightText { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public string? SeoKeywords { get; set; }
    public string? OgImage { get; set; }
    public string? FacebookUrl { get; set; }
    public string? TwitterUrl { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? InstagramUrl { get; set; }
    public string? SupportEmail { get; set; }
    public string? SupportPhone { get; set; }
    public string? SupportAddress { get; set; }
    // Feature flags (nullable to support partial updates)
    public bool? FeatureSupervisorMode { get; set; }
    public bool? FeatureAiAnalysis { get; set; }
    public bool? FeatureAiAutoReply { get; set; }
    public bool? FeatureFileSharing { get; set; }
    public bool? FeatureCsatRatings { get; set; }
    public bool? FeatureVisitorInfo { get; set; }
    public bool? FeatureCannedResponses { get; set; }
    public bool? FeatureConversationTransfer { get; set; }
    public bool? FeatureTeamChat { get; set; }
    public bool? FeatureTypingIndicators { get; set; }
    public bool? FeatureReadReceipts { get; set; }
    public bool? FeatureInternalNotes { get; set; }
    public bool? FeatureEmojiPicker { get; set; }
    public bool? FeatureEmailSending { get; set; }
    public bool? FeatureConversationSearch { get; set; }
    public bool? FeatureMessageSearch { get; set; }
    public bool? FeatureBulkActions { get; set; }
    public bool? FeatureThemes { get; set; }
    public bool? FeatureAgentStatus { get; set; }
    public bool? FeatureNotifications { get; set; }
}
