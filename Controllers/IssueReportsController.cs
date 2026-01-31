using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChatApp.API.Data;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Models.Entities;

namespace ChatApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IssueReportsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private const long MaxFileSize = 10 * 1024 * 1024; // 10MB
    private const int MaxFiles = 5;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp",
        ".pdf", ".doc", ".docx", ".txt", ".log",
        ".zip", ".rar", ".7z"
    };

    public IssueReportsController(ApplicationDbContext context, IWebHostEnvironment env, IConfiguration configuration)
    {
        _context = context;
        _env = env;
        _configuration = configuration;
    }

    private string GetUploadsBasePath()
    {
        var uploadPath = _configuration["FileUpload:UploadPath"] ?? "uploads";
        return Path.Combine(_env.ContentRootPath, uploadPath);
    }

    /// <summary>
    /// Submit an issue report (public endpoint)
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [RequestSizeLimit(60 * 1024 * 1024)] // 60MB total limit
    public async Task<ActionResult<ApiResponse<IssueReportDto>>> Submit()
    {
        var form = await Request.ReadFormAsync();

        var name = form["name"].ToString().Trim();
        var email = form["email"].ToString().Trim();
        var title = form["title"].ToString().Trim();
        var category = form["category"].ToString().Trim();
        var priority = form["priority"].ToString().Trim();
        var description = form["description"].ToString().Trim();
        var userId = form["userId"].ToString().Trim();
        var siteId = form["siteId"].ToString().Trim();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
        {
            return BadRequest(ApiResponse<IssueReportDto>.Fail("Name, email, title, and description are required"));
        }

        var report = new IssueReport
        {
            Name = name,
            Email = email,
            Title = title,
            Category = string.IsNullOrWhiteSpace(category) ? "general" : category,
            Priority = string.IsNullOrWhiteSpace(priority) ? "medium" : priority,
            Description = description,
            UserId = string.IsNullOrWhiteSpace(userId) ? null : userId,
            SiteId = string.IsNullOrWhiteSpace(siteId) ? null : siteId,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers["User-Agent"].ToString()
        };

        _context.IssueReports.Add(report);
        await _context.SaveChangesAsync();

        // Handle file uploads
        var files = form.Files;
        if (files.Count > MaxFiles)
        {
            return BadRequest(ApiResponse<IssueReportDto>.Fail($"Maximum {MaxFiles} files allowed"));
        }

        var uploadsBase = GetUploadsBasePath();
        var uploadsDir = Path.Combine(uploadsBase, "issues", report.Id);
        if (files.Count > 0)
        {
            try
            {
                Directory.CreateDirectory(uploadsDir);
            }
            catch
            {
                // Fallback: use the base uploads folder directly (same as widget files)
                uploadsDir = uploadsBase;
                Directory.CreateDirectory(uploadsDir);
            }
        }

        foreach (var file in files)
        {
            if (file.Length > MaxFileSize)
            {
                return BadRequest(ApiResponse<IssueReportDto>.Fail($"File '{file.FileName}' exceeds the 10MB limit"));
            }

            var ext = Path.GetExtension(file.FileName);
            if (!AllowedExtensions.Contains(ext))
            {
                return BadRequest(ApiResponse<IssueReportDto>.Fail($"File type '{ext}' is not allowed"));
            }

            var storedName = $"issue_{report.Id}_{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadsDir, storedName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var attachment = new IssueReportAttachment
            {
                IssueReportId = report.Id,
                OriginalName = file.FileName,
                StoredName = storedName,
                MimeType = file.ContentType ?? "application/octet-stream",
                FileSize = file.Length,
                FilePath = filePath
            };

            _context.IssueReportAttachments.Add(attachment);
        }

        if (files.Count > 0)
        {
            await _context.SaveChangesAsync();
        }

        // Reload with attachments
        var saved = await _context.IssueReports
            .Include(r => r.Attachments)
            .FirstAsync(r => r.Id == report.Id);

        return Ok(ApiResponse<IssueReportDto>.Ok(MapToDto(saved), "Issue report submitted successfully"));
    }

    /// <summary>
    /// List all issue reports (admin only)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<PagedResponse<IssueReportDto>>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? category = null,
        [FromQuery] string? priority = null,
        [FromQuery] bool? isRead = null,
        [FromQuery] string? search = null)
    {
        var query = _context.IssueReports.Include(r => r.Attachments).AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(r => r.Status == status);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(r => r.Category == category);

        if (!string.IsNullOrWhiteSpace(priority))
            query = query.Where(r => r.Priority == priority);

        if (isRead.HasValue)
            query = query.Where(r => r.IsRead == isRead.Value);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(r =>
                r.Title.Contains(search) ||
                r.Name.Contains(search) ||
                r.Email.Contains(search) ||
                r.Description.Contains(search));

        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var pagedResponse = new PagedResponse<IssueReportDto>(
            items.Select(MapToDto).ToList(),
            page,
            pageSize,
            totalItems,
            totalPages
        );

        return Ok(ApiResponse<PagedResponse<IssueReportDto>>.Ok(pagedResponse));
    }

    /// <summary>
    /// Get a single issue report with attachments (admin only)
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<IssueReportDto>>> GetById(string id)
    {
        var report = await _context.IssueReports
            .Include(r => r.Attachments)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (report == null)
            return NotFound(ApiResponse<IssueReportDto>.Fail("Issue report not found"));

        return Ok(ApiResponse<IssueReportDto>.Ok(MapToDto(report)));
    }

    /// <summary>
    /// Mark report as read (admin only)
    /// </summary>
    [HttpPut("{id}/read")]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<IssueReportDto>>> MarkAsRead(string id)
    {
        var report = await _context.IssueReports
            .Include(r => r.Attachments)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (report == null)
            return NotFound(ApiResponse<IssueReportDto>.Fail("Issue report not found"));

        report.IsRead = true;
        report.ReadAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<IssueReportDto>.Ok(MapToDto(report), "Marked as read"));
    }

    /// <summary>
    /// Update report status (admin only)
    /// </summary>
    [HttpPut("{id}/status")]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<IssueReportDto>>> UpdateStatus(string id, [FromBody] UpdateIssueStatusRequest request)
    {
        var report = await _context.IssueReports
            .Include(r => r.Attachments)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (report == null)
            return NotFound(ApiResponse<IssueReportDto>.Fail("Issue report not found"));

        report.Status = request.Status;
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<IssueReportDto>.Ok(MapToDto(report), "Status updated"));
    }

    /// <summary>
    /// Update admin notes (admin only)
    /// </summary>
    [HttpPut("{id}/notes")]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<IssueReportDto>>> UpdateNotes(string id, [FromBody] UpdateIssueNotesRequest request)
    {
        var report = await _context.IssueReports
            .Include(r => r.Attachments)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (report == null)
            return NotFound(ApiResponse<IssueReportDto>.Fail("Issue report not found"));

        report.AdminNotes = request.AdminNotes;
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<IssueReportDto>.Ok(MapToDto(report), "Notes updated"));
    }

    /// <summary>
    /// Delete an issue report (admin only)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<string>>> Delete(string id)
    {
        var report = await _context.IssueReports.FindAsync(id);

        if (report == null)
            return NotFound(ApiResponse<string>.Fail("Issue report not found"));

        // Delete files from disk
        var uploadsDir = Path.Combine(GetUploadsBasePath(), "issues", id);
        if (Directory.Exists(uploadsDir))
        {
            Directory.Delete(uploadsDir, true);
        }

        _context.IssueReports.Remove(report);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Deleted successfully"));
    }

    /// <summary>
    /// Download an attachment file (public endpoint)
    /// </summary>
    [HttpGet("attachments/{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadAttachment(string id)
    {
        var attachment = await _context.IssueReportAttachments.FindAsync(id);

        if (attachment == null)
            return NotFound();

        if (!System.IO.File.Exists(attachment.FilePath))
            return NotFound();

        var stream = new FileStream(attachment.FilePath, FileMode.Open, FileAccess.Read);
        return File(stream, attachment.MimeType, attachment.OriginalName);
    }

    private static IssueReportDto MapToDto(IssueReport report)
    {
        return new IssueReportDto(
            report.Id,
            report.Name,
            report.Email,
            report.UserId,
            report.SiteId,
            report.Title,
            report.Category,
            report.Priority,
            report.Description,
            report.Status,
            report.IsRead,
            report.ReadAt,
            report.AdminNotes,
            report.IpAddress,
            report.UserAgent,
            report.CreatedAt,
            report.UpdatedAt,
            report.Attachments?.Select(a => new IssueReportAttachmentDto(
                a.Id,
                a.IssueReportId,
                a.OriginalName,
                a.StoredName,
                a.MimeType,
                a.FileSize,
                a.FilePath,
                a.CreatedAt
            )).ToList()
        );
    }
}
