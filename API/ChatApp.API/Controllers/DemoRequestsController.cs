using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChatApp.API.Data;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Models.Entities;

namespace ChatApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DemoRequestsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DemoRequestsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // Inline DTOs
    public record CreateDemoRequestDto(string Name, string Email, string Company, string? Phone, string? Message);
    public record UpdateDemoStatusDto(string Status);
    public record UpdateDemoNotesDto(string? AdminNotes);
    public record DemoRequestDto(
        string Id, string Name, string Email, string Company, string? Phone, string? Message,
        string Status, string? AdminNotes, string? IpAddress, string? UserAgent,
        DateTime CreatedAt, DateTime UpdatedAt
    );

    /// <summary>
    /// Submit a demo request (public endpoint)
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<DemoRequestDto>>> Submit([FromBody] CreateDemoRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Company))
        {
            return BadRequest(ApiResponse<DemoRequestDto>.Fail("Name, email, and company are required"));
        }

        var demoRequest = new DemoRequest
        {
            Name = request.Name.Trim(),
            Email = request.Email.Trim(),
            Company = request.Company.Trim(),
            Phone = request.Phone?.Trim(),
            Message = request.Message?.Trim(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers["User-Agent"].ToString()
        };

        _context.DemoRequests.Add(demoRequest);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<DemoRequestDto>.Ok(MapToDto(demoRequest), "Demo request submitted successfully"));
    }

    /// <summary>
    /// List all demo requests (admin only)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<PagedResponse<DemoRequestDto>>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null)
    {
        var query = _context.DemoRequests.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(r => r.Status == status);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(r =>
                r.Name.Contains(search) ||
                r.Email.Contains(search) ||
                r.Company.Contains(search) ||
                (r.Message != null && r.Message.Contains(search)));

        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var pagedResponse = new PagedResponse<DemoRequestDto>(
            items.Select(MapToDto).ToList(),
            page,
            pageSize,
            totalItems,
            totalPages
        );

        return Ok(ApiResponse<PagedResponse<DemoRequestDto>>.Ok(pagedResponse));
    }

    /// <summary>
    /// Get a single demo request (admin only)
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<DemoRequestDto>>> GetById(string id)
    {
        var demoRequest = await _context.DemoRequests.FirstOrDefaultAsync(r => r.Id == id);

        if (demoRequest == null)
            return NotFound(ApiResponse<DemoRequestDto>.Fail("Demo request not found"));

        return Ok(ApiResponse<DemoRequestDto>.Ok(MapToDto(demoRequest)));
    }

    /// <summary>
    /// Update demo request status (admin only)
    /// </summary>
    [HttpPut("{id}/status")]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<DemoRequestDto>>> UpdateStatus(string id, [FromBody] UpdateDemoStatusDto request)
    {
        var demoRequest = await _context.DemoRequests.FirstOrDefaultAsync(r => r.Id == id);

        if (demoRequest == null)
            return NotFound(ApiResponse<DemoRequestDto>.Fail("Demo request not found"));

        demoRequest.Status = request.Status;
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<DemoRequestDto>.Ok(MapToDto(demoRequest), "Status updated"));
    }

    /// <summary>
    /// Update admin notes (admin only)
    /// </summary>
    [HttpPut("{id}/notes")]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<DemoRequestDto>>> UpdateNotes(string id, [FromBody] UpdateDemoNotesDto request)
    {
        var demoRequest = await _context.DemoRequests.FirstOrDefaultAsync(r => r.Id == id);

        if (demoRequest == null)
            return NotFound(ApiResponse<DemoRequestDto>.Fail("Demo request not found"));

        demoRequest.AdminNotes = request.AdminNotes;
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<DemoRequestDto>.Ok(MapToDto(demoRequest), "Notes updated"));
    }

    /// <summary>
    /// Delete a demo request (admin only)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<string>>> Delete(string id)
    {
        var demoRequest = await _context.DemoRequests.FindAsync(id);

        if (demoRequest == null)
            return NotFound(ApiResponse<string>.Fail("Demo request not found"));

        _context.DemoRequests.Remove(demoRequest);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Deleted successfully"));
    }

    private static DemoRequestDto MapToDto(DemoRequest r)
    {
        return new DemoRequestDto(
            r.Id, r.Name, r.Email, r.Company, r.Phone, r.Message,
            r.Status, r.AdminNotes, r.IpAddress, r.UserAgent,
            r.CreatedAt, r.UpdatedAt
        );
    }
}
