using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChatApp.API.Data;
using ChatApp.API.Models.DTOs;

namespace ChatApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public UsersController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get all users (super_admin only)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<PagedResponse<UserAdminDto>>>> GetAllUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = _context.Users.AsQueryable();

        var totalItems = await query.CountAsync();
        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserAdminDto(
                u.Id,
                u.Username,
                u.Email,
                u.FirstName,
                u.LastName,
                u.Role,
                u.Status,
                u.IsActive,
                u.EmailVerified,
                u.LastLoginAt,
                u.LoginCount,
                u.CreatedAt
            ))
            .ToListAsync();

        var result = new PagedResponse<UserAdminDto>(
            users,
            page,
            pageSize,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)pageSize)
        );

        return Ok(ApiResponse<PagedResponse<UserAdminDto>>.Ok(result));
    }

    /// <summary>
    /// Get user by ID (super_admin only)
    /// </summary>
    [HttpGet("{userId}")]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<UserAdminDto>>> GetUser(string userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return NotFound(ApiResponse<UserAdminDto>.Fail("User not found"));
        }

        var dto = new UserAdminDto(
            user.Id,
            user.Username,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Role,
            user.Status,
            user.IsActive,
            user.EmailVerified,
            user.LastLoginAt,
            user.LoginCount,
            user.CreatedAt
        );

        return Ok(ApiResponse<UserAdminDto>.Ok(dto));
    }

    /// <summary>
    /// Update user role (super_admin only)
    /// </summary>
    [HttpPut("{userId}/role")]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<UserAdminDto>>> UpdateUserRole(
        string userId,
        [FromBody] UpdateUserRoleRequest request)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return NotFound(ApiResponse<UserAdminDto>.Fail("User not found"));
        }

        user.Role = request.Role;
        await _context.SaveChangesAsync();

        var dto = new UserAdminDto(
            user.Id,
            user.Username,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Role,
            user.Status,
            user.IsActive,
            user.EmailVerified,
            user.LastLoginAt,
            user.LoginCount,
            user.CreatedAt
        );

        return Ok(ApiResponse<UserAdminDto>.Ok(dto, "User role updated"));
    }

    /// <summary>
    /// Disable/Enable user (super_admin only)
    /// </summary>
    [HttpPut("{userId}/active")]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<UserAdminDto>>> UpdateUserActiveStatus(
        string userId,
        [FromBody] UpdateUserActiveRequest request)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return NotFound(ApiResponse<UserAdminDto>.Fail("User not found"));
        }

        user.IsActive = request.IsActive;
        await _context.SaveChangesAsync();

        var dto = new UserAdminDto(
            user.Id,
            user.Username,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Role,
            user.Status,
            user.IsActive,
            user.EmailVerified,
            user.LastLoginAt,
            user.LoginCount,
            user.CreatedAt
        );

        return Ok(ApiResponse<UserAdminDto>.Ok(dto, user.IsActive ? "User enabled" : "User disabled"));
    }
}

// DTOs for admin user management
public record UserAdminDto(
    string Id,
    string Username,
    string Email,
    string? FirstName,
    string? LastName,
    string Role,
    string Status,
    bool IsActive,
    bool EmailVerified,
    DateTime? LastLoginAt,
    int LoginCount,
    DateTime CreatedAt
);

public record UpdateUserRoleRequest(string Role);
public record UpdateUserActiveRequest(bool IsActive);
