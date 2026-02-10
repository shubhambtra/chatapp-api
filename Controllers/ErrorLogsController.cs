using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChatApp.API.Data;
using ChatApp.API.Models.DTOs;

namespace ChatApp.API.Controllers;

/// <summary>
/// Admin endpoints for viewing error logs (super_admin only)
/// </summary>
[ApiController]
[Route("api/admin/error-logs")]
[Authorize(Roles = "super_admin")]
public class ErrorLogsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ErrorLogsController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get all error logs with pagination and filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<ErrorLogsResponse>>> GetErrorLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? severity = null,
        [FromQuery] string? search = null)
    {
        var query = _context.ErrorLogs.AsQueryable();

        if (!string.IsNullOrEmpty(severity))
        {
            query = query.Where(e => e.Severity == severity);
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(e =>
                e.ErrorMessage.Contains(search) ||
                (e.RequestPath != null && e.RequestPath.Contains(search)) ||
                (e.ExceptionType != null && e.ExceptionType.Contains(search)) ||
                (e.Source != null && e.Source.Contains(search)));
        }

        var totalCount = await query.CountAsync();

        var errorLogs = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new ErrorLogDto(
                e.Id,
                e.ErrorMessage,
                e.StackTrace,
                e.Source,
                e.ErrorCode,
                e.RequestPath,
                e.RequestMethod,
                e.RequestBody,
                e.QueryString,
                e.UserId,
                e.IpAddress,
                e.UserAgent,
                e.ExceptionType,
                e.InnerException,
                e.Severity,
                e.CreatedAt
            ))
            .ToListAsync();

        var response = new ErrorLogsResponse(
            errorLogs,
            totalCount,
            page,
            pageSize,
            (int)Math.Ceiling((double)totalCount / pageSize)
        );

        return Ok(ApiResponse<ErrorLogsResponse>.Ok(response));
    }

    /// <summary>
    /// Get a single error log by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<ErrorLogDto>>> GetErrorLog(int id)
    {
        var errorLog = await _context.ErrorLogs
            .Where(e => e.Id == id)
            .Select(e => new ErrorLogDto(
                e.Id,
                e.ErrorMessage,
                e.StackTrace,
                e.Source,
                e.ErrorCode,
                e.RequestPath,
                e.RequestMethod,
                e.RequestBody,
                e.QueryString,
                e.UserId,
                e.IpAddress,
                e.UserAgent,
                e.ExceptionType,
                e.InnerException,
                e.Severity,
                e.CreatedAt
            ))
            .FirstOrDefaultAsync();

        if (errorLog == null)
        {
            return NotFound(ApiResponse<ErrorLogDto>.Fail("Error log not found"));
        }

        return Ok(ApiResponse<ErrorLogDto>.Ok(errorLog));
    }

    /// <summary>
    /// Get error statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<ApiResponse<ErrorStatsDto>>> GetErrorStats()
    {
        var totalErrors = await _context.ErrorLogs.CountAsync();
        var errorCount = await _context.ErrorLogs.CountAsync(e => e.Severity == "Error");
        var warningCount = await _context.ErrorLogs.CountAsync(e => e.Severity == "Warning");
        var criticalCount = await _context.ErrorLogs.CountAsync(e => e.Severity == "Critical");

        var todayStart = DateTime.UtcNow.Date;
        var todayCount = await _context.ErrorLogs.CountAsync(e => e.CreatedAt >= todayStart);

        var stats = new ErrorStatsDto(
            totalErrors,
            errorCount,
            warningCount,
            criticalCount,
            todayCount
        );

        return Ok(ApiResponse<ErrorStatsDto>.Ok(stats));
    }

    /// <summary>
    /// Delete error logs older than specified days
    /// </summary>
    [HttpDelete("cleanup")]
    public async Task<ActionResult<ApiResponse<string>>> CleanupOldLogs([FromQuery] int olderThanDays = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(-olderThanDays);
        var oldLogs = await _context.ErrorLogs.Where(e => e.CreatedAt < cutoff).ToListAsync();
        var count = oldLogs.Count;

        _context.ErrorLogs.RemoveRange(oldLogs);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok($"Deleted {count} error logs older than {olderThanDays} days"));
    }
}

// DTOs for Error Logs
public record ErrorLogDto(
    int Id,
    string ErrorMessage,
    string? StackTrace,
    string? Source,
    string? ErrorCode,
    string? RequestPath,
    string? RequestMethod,
    string? RequestBody,
    string? QueryString,
    string? UserId,
    string? IpAddress,
    string? UserAgent,
    string? ExceptionType,
    string? InnerException,
    string Severity,
    DateTime CreatedAt
);

public record ErrorLogsResponse(
    List<ErrorLogDto> ErrorLogs,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

public record ErrorStatsDto(
    int TotalErrors,
    int ErrorCount,
    int WarningCount,
    int CriticalCount,
    int TodayCount
);
