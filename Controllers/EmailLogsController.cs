using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChatApp.API.Data;
using ChatApp.API.Models.DTOs;

namespace ChatApp.API.Controllers;

/// <summary>
/// Admin endpoints for viewing email logs (super_admin only)
/// </summary>
[ApiController]
[Route("api/admin/email-logs")]
[Authorize(Roles = "super_admin")]
public class EmailLogsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public EmailLogsController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get all email logs with pagination
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<EmailLogsResponse>>> GetEmailLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? emailType = null,
        [FromQuery] string? search = null)
    {
        var query = _context.EmailLogs.AsQueryable();

        // Apply filters
        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(e => e.Status == status);
        }

        if (!string.IsNullOrEmpty(emailType))
        {
            query = query.Where(e => e.EmailType == emailType);
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(e =>
                e.ToEmail.Contains(search) ||
                e.FromEmail.Contains(search) ||
                e.Subject.Contains(search));
        }

        var totalCount = await query.CountAsync();

        var emailLogs = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EmailLogDto(
                e.Id,
                e.FromEmail,
                e.FromName,
                e.ToEmail,
                e.ToName,
                e.Subject,
                e.Body,
                e.IsHtml,
                e.Status,
                e.ErrorMessage,
                e.EmailType,
                e.SiteId,
                e.UserId,
                e.CreatedAt,
                e.SentAt
            ))
            .ToListAsync();

        var response = new EmailLogsResponse(
            emailLogs,
            totalCount,
            page,
            pageSize,
            (int)Math.Ceiling((double)totalCount / pageSize)
        );

        return Ok(ApiResponse<EmailLogsResponse>.Ok(response));
    }

    /// <summary>
    /// Get a single email log by ID with full body content
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<EmailLogDto>>> GetEmailLog(string id)
    {
        var emailLog = await _context.EmailLogs
            .Where(e => e.Id == id)
            .Select(e => new EmailLogDto(
                e.Id,
                e.FromEmail,
                e.FromName,
                e.ToEmail,
                e.ToName,
                e.Subject,
                e.Body,
                e.IsHtml,
                e.Status,
                e.ErrorMessage,
                e.EmailType,
                e.SiteId,
                e.UserId,
                e.CreatedAt,
                e.SentAt
            ))
            .FirstOrDefaultAsync();

        if (emailLog == null)
        {
            return NotFound(ApiResponse<EmailLogDto>.Fail("Email log not found"));
        }

        return Ok(ApiResponse<EmailLogDto>.Ok(emailLog));
    }

    /// <summary>
    /// Get email statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<ApiResponse<EmailStatsDto>>> GetEmailStats()
    {
        var totalEmails = await _context.EmailLogs.CountAsync();
        var sentCount = await _context.EmailLogs.CountAsync(e => e.Status == "sent");
        var failedCount = await _context.EmailLogs.CountAsync(e => e.Status == "failed");
        var pendingCount = await _context.EmailLogs.CountAsync(e => e.Status == "pending");

        var todayStart = DateTime.UtcNow.Date;
        var todayCount = await _context.EmailLogs.CountAsync(e => e.CreatedAt >= todayStart);

        var weekStart = DateTime.UtcNow.Date.AddDays(-7);
        var weekCount = await _context.EmailLogs.CountAsync(e => e.CreatedAt >= weekStart);

        var typeBreakdown = await _context.EmailLogs
            .GroupBy(e => e.EmailType ?? "unknown")
            .Select(g => new EmailTypeCount(g.Key, g.Count()))
            .ToListAsync();

        var stats = new EmailStatsDto(
            totalEmails,
            sentCount,
            failedCount,
            pendingCount,
            todayCount,
            weekCount,
            typeBreakdown
        );

        return Ok(ApiResponse<EmailStatsDto>.Ok(stats));
    }
}

// DTOs for Email Logs
public record EmailLogDto(
    string Id,
    string FromEmail,
    string? FromName,
    string ToEmail,
    string? ToName,
    string Subject,
    string Body,
    bool IsHtml,
    string Status,
    string? ErrorMessage,
    string? EmailType,
    string? SiteId,
    string? UserId,
    DateTime CreatedAt,
    DateTime? SentAt
);

public record EmailLogsResponse(
    List<EmailLogDto> EmailLogs,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

public record EmailTypeCount(string EmailType, int Count);

public record EmailStatsDto(
    int TotalEmails,
    int SentCount,
    int FailedCount,
    int PendingCount,
    int TodayCount,
    int WeekCount,
    List<EmailTypeCount> TypeBreakdown
);
