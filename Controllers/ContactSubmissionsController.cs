using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChatApp.API.Data;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Models.Entities;

namespace ChatApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContactSubmissionsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ContactSubmissionsController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Submit a contact form (public endpoint)
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ContactSubmissionDto>>> Submit([FromBody] CreateContactSubmissionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(ApiResponse<ContactSubmissionDto>.Fail("Name, email, and message are required"));
        }

        var submission = new ContactSubmission
        {
            Name = request.Name.Trim(),
            Email = request.Email.Trim(),
            Subject = request.Subject?.Trim(),
            Message = request.Message.Trim(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers["User-Agent"].ToString()
        };

        _context.ContactSubmissions.Add(submission);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<ContactSubmissionDto>.Ok(MapToDto(submission), "Thank you! Your message has been received."));
    }

    /// <summary>
    /// Get all contact submissions (admin only)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<PagedResponse<ContactSubmissionDto>>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? isRead = null)
    {
        var query = _context.ContactSubmissions.AsQueryable();

        if (isRead.HasValue)
        {
            query = query.Where(c => c.IsRead == isRead.Value);
        }

        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var submissions = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var pagedResponse = new PagedResponse<ContactSubmissionDto>(
            submissions.Select(MapToDto).ToList(),
            page,
            pageSize,
            totalItems,
            totalPages
        );

        return Ok(ApiResponse<PagedResponse<ContactSubmissionDto>>.Ok(pagedResponse));
    }

    /// <summary>
    /// Get unread count (admin only)
    /// </summary>
    [HttpGet("unread-count")]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<int>>> GetUnreadCount()
    {
        var count = await _context.ContactSubmissions.CountAsync(c => !c.IsRead);
        return Ok(ApiResponse<int>.Ok(count));
    }

    /// <summary>
    /// Get a single contact submission (admin only)
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<ContactSubmissionDto>>> GetById(string id)
    {
        var submission = await _context.ContactSubmissions.FindAsync(id);

        if (submission == null)
        {
            return NotFound(ApiResponse<ContactSubmissionDto>.Fail("Contact submission not found"));
        }

        return Ok(ApiResponse<ContactSubmissionDto>.Ok(MapToDto(submission)));
    }

    /// <summary>
    /// Mark a submission as read (admin only)
    /// </summary>
    [HttpPut("{id}/read")]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<ContactSubmissionDto>>> MarkAsRead(string id)
    {
        var submission = await _context.ContactSubmissions.FindAsync(id);

        if (submission == null)
        {
            return NotFound(ApiResponse<ContactSubmissionDto>.Fail("Contact submission not found"));
        }

        submission.IsRead = true;
        submission.ReadAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<ContactSubmissionDto>.Ok(MapToDto(submission), "Marked as read"));
    }

    /// <summary>
    /// Mark a submission as unread (admin only)
    /// </summary>
    [HttpPut("{id}/unread")]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<ContactSubmissionDto>>> MarkAsUnread(string id)
    {
        var submission = await _context.ContactSubmissions.FindAsync(id);

        if (submission == null)
        {
            return NotFound(ApiResponse<ContactSubmissionDto>.Fail("Contact submission not found"));
        }

        submission.IsRead = false;
        submission.ReadAt = null;
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<ContactSubmissionDto>.Ok(MapToDto(submission), "Marked as unread"));
    }

    /// <summary>
    /// Delete a submission (admin only)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<string>>> Delete(string id)
    {
        var submission = await _context.ContactSubmissions.FindAsync(id);

        if (submission == null)
        {
            return NotFound(ApiResponse<string>.Fail("Contact submission not found"));
        }

        _context.ContactSubmissions.Remove(submission);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Deleted successfully"));
    }

    private static ContactSubmissionDto MapToDto(ContactSubmission submission)
    {
        return new ContactSubmissionDto
        {
            Id = submission.Id,
            Name = submission.Name,
            Email = submission.Email,
            Subject = submission.Subject,
            Message = submission.Message,
            IsRead = submission.IsRead,
            ReadAt = submission.ReadAt,
            IpAddress = submission.IpAddress,
            CreatedAt = submission.CreatedAt
        };
    }
}

// DTOs
public class ContactSubmissionDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateContactSubmissionRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string Message { get; set; } = string.Empty;
}
