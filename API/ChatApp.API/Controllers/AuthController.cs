using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Services.Interfaces;

namespace ChatApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var result = await _authService.LoginAsync(request);
            return Ok(ApiResponse<LoginResponse>.Ok(result));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse<LoginResponse>.Fail(ex.Message));
        }
    }

    [HttpPost("support/login")]
    public async Task<ActionResult<ApiResponse<SupportLoginResponse>>> SupportLogin([FromBody] SupportLoginRequest request)
    {
        try
        {
            var result = await _authService.SupportLoginAsync(request);
            return Ok(ApiResponse<SupportLoginResponse>.Ok(result));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse<SupportLoginResponse>.Fail(ex.Message));
        }
    }

    [HttpGet("validate")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<ValidateTokenResponse>>> ValidateToken()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Ok(ApiResponse<ValidateTokenResponse>.Ok(new ValidateTokenResponse(false, null)));
        }

        var result = await _authService.ValidateTokenAsync(userId);
        return Ok(ApiResponse<ValidateTokenResponse>.Ok(result));
    }

    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var result = await _authService.RegisterAsync(request);
            return Ok(ApiResponse<LoginResponse>.Ok(result, "Registration successful"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<LoginResponse>.Fail(ex.Message));
        }
    }

    [HttpGet("check-email")]
    public async Task<ActionResult<ApiResponse<AvailabilityResponse>>> CheckEmail([FromQuery] string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(ApiResponse<AvailabilityResponse>.Fail("Email is required"));
        }

        var exists = await _authService.CheckEmailExistsAsync(email);
        return Ok(ApiResponse<AvailabilityResponse>.Ok(new AvailabilityResponse(!exists, exists ? "Email is already registered" : null)));
    }

    [HttpGet("check-username")]
    public async Task<ActionResult<ApiResponse<AvailabilityResponse>>> CheckUsername([FromQuery] string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return BadRequest(ApiResponse<AvailabilityResponse>.Fail("Username is required"));
        }

        if (username.Length < 3)
        {
            return Ok(ApiResponse<AvailabilityResponse>.Ok(new AvailabilityResponse(false, "Username must be at least 3 characters")));
        }

        var exists = await _authService.CheckUsernameExistsAsync(username);
        return Ok(ApiResponse<AvailabilityResponse>.Ok(new AvailabilityResponse(!exists, exists ? "Username is already taken" : null)));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<RefreshTokenResponse>>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var result = await _authService.RefreshTokenAsync(request);
            return Ok(ApiResponse<RefreshTokenResponse>.Ok(result));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse<RefreshTokenResponse>.Fail(ex.Message));
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Logout([FromBody] RefreshTokenRequest request)
    {
        await _authService.RevokeRefreshTokenAsync(request.RefreshToken);
        return Ok(ApiResponse<object>.Ok(null, "Logged out successfully"));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetCurrentUser()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(ApiResponse<UserDto>.Fail("User not found"));
        }

        var user = await _authService.GetCurrentUserAsync(userId);
        if (user == null)
        {
            return NotFound(ApiResponse<UserDto>.Fail("User not found"));
        }

        return Ok(ApiResponse<UserDto>.Ok(user));
    }

    [HttpPut("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserDto>>> UpdateCurrentUser([FromBody] UpdateUserRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(ApiResponse<UserDto>.Fail("User not found"));
        }

        try
        {
            var user = await _authService.UpdateUserAsync(userId, request);
            return Ok(ApiResponse<UserDto>.Ok(user));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<UserDto>.Fail(ex.Message));
        }
    }

    [HttpPut("me/status")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> UpdateStatus([FromBody] UpdateUserStatusRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(ApiResponse<object>.Fail("User not found"));
        }

        await _authService.UpdateUserStatusAsync(userId, request);
        return Ok(ApiResponse<object>.Ok(null, "Status updated"));
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(ApiResponse<object>.Fail("User not found"));
        }

        try
        {
            await _authService.ChangePasswordAsync(userId, request);
            return Ok(ApiResponse<object>.Ok(null, "Password changed successfully"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("forgot-password")]
    public async Task<ActionResult<ApiResponse<ForgotPasswordResponse>>> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var result = await _authService.ForgotPasswordAsync(request);
        return Ok(ApiResponse<ForgotPasswordResponse>.Ok(result));
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult<ApiResponse<object>>> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        try
        {
            await _authService.ResetPasswordAsync(request);
            return Ok(ApiResponse<object>.Ok(null, "Password reset successfully"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
