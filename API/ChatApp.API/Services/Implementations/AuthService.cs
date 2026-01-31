using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ChatApp.API.Data;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Models.Entities;
using ChatApp.API.Services.Interfaces;

namespace ChatApp.API.Services.Implementations;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _emailService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(ApplicationDbContext context, IConfiguration configuration, IEmailService emailService, ILogger<AuthService> logger)
    {
        _context = context;
        _configuration = configuration;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid email or password");
        }

        user.LastLoginAt = DateTime.UtcNow;
        user.LoginCount++;

        var accessToken = GenerateAccessToken(user);
        var refreshToken = await GenerateRefreshTokenAsync(user.Id);

        await _context.SaveChangesAsync();

        var expiresAt = DateTime.UtcNow.AddMinutes(
            _configuration.GetValue<int>("JwtSettings:AccessTokenExpirationMinutes"));

        return new LoginResponse(accessToken, refreshToken, expiresAt, MapToDto(user));
    }

    public async Task<LoginResponse> RegisterAsync(RegisterRequest request)
    {
        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
        {
            throw new InvalidOperationException("Email already registered");
        }

        if (await _context.Users.AnyAsync(u => u.Username == request.Username))
        {
            throw new InvalidOperationException("Username already taken");
        }

        var user = new User
        {
            Email = request.Email,
            Username = request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Role = "site_admin",
            Status = "offline"
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Send welcome email
        try
        {
            await _emailService.SendWelcomeEmailAsync(user.Email, user.Username, "ChatApp");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send welcome email to {Email}", user.Email);
        }

        // Auto-login after registration
        var accessToken = GenerateAccessToken(user);
        var refreshToken = await GenerateRefreshTokenAsync(user.Id);

        await _context.SaveChangesAsync();

        var expiresAt = DateTime.UtcNow.AddMinutes(
            _configuration.GetValue<int>("JwtSettings:AccessTokenExpirationMinutes"));

        return new LoginResponse(accessToken, refreshToken, expiresAt, MapToDto(user));
    }

    public async Task<RefreshTokenResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var storedToken = await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken && !rt.IsRevoked);

        if (storedToken == null || storedToken.ExpiresAt < DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException("Invalid or expired refresh token");
        }

        // Revoke old token
        storedToken.IsRevoked = true;
        storedToken.RevokedAt = DateTime.UtcNow;

        // Generate new tokens
        var accessToken = GenerateAccessToken(storedToken.User);
        var newRefreshToken = await GenerateRefreshTokenAsync(storedToken.UserId);

        await _context.SaveChangesAsync();

        var expiresAt = DateTime.UtcNow.AddMinutes(
            _configuration.GetValue<int>("JwtSettings:AccessTokenExpirationMinutes"));

        return new RefreshTokenResponse(accessToken, newRefreshToken, expiresAt);
    }

    public async Task RevokeRefreshTokenAsync(string token)
    {
        var storedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == token);

        if (storedToken != null)
        {
            storedToken.IsRevoked = true;
            storedToken.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task ChangePasswordAsync(string userId, ChangePasswordRequest request)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Current password is incorrect");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _context.SaveChangesAsync();
    }

    public async Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive);

        if (user != null)
        {
            // Generate a secure random token
            var tokenBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(tokenBytes);
            }
            var resetToken = Convert.ToBase64String(tokenBytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");

            // Set token expiration (default 24 hours)
            var expirationHours = _configuration.GetValue<int>("App:PasswordResetTokenExpirationHours", 24);
            user.PasswordResetToken = resetToken;
            user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(expirationHours);

            await _context.SaveChangesAsync();

            // Build reset link
            var frontendUrl = _configuration.GetValue<string>("App:FrontendUrl") ?? "http://localhost:8000";
            var resetLink = $"{frontendUrl}/reset-password.html?token={resetToken}";

            // Send email
            try
            {
                await _emailService.SendPasswordResetAsync(user.Email, user.Username ?? user.Email, resetLink);
                _logger.LogInformation("Password reset email sent to {Email}", user.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password reset email to {Email}", user.Email);
                // Don't throw - we don't want to reveal if email exists
            }
        }

        // Always return same message to prevent email enumeration
        return new ForgotPasswordResponse("If an account exists with this email, a password reset link will be sent.");
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request)
    {
        if (string.IsNullOrEmpty(request.Token) || string.IsNullOrEmpty(request.NewPassword))
        {
            throw new ArgumentException("Token and new password are required");
        }

        var user = await _context.Users.FirstOrDefaultAsync(u =>
            u.PasswordResetToken == request.Token &&
            u.PasswordResetTokenExpiresAt > DateTime.UtcNow &&
            u.IsActive);

        if (user == null)
        {
            throw new InvalidOperationException("Invalid or expired reset token");
        }

        // Update password
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

        // Clear reset token
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiresAt = null;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Password reset successful for user {UserId}", user.Id);
    }

    public async Task<UserDto?> GetCurrentUserAsync(string userId)
    {
        var user = await _context.Users.FindAsync(userId);
        return user != null ? MapToDto(user) : null;
    }

    public async Task<UserDto> UpdateUserAsync(string userId, UpdateUserRequest request)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        if (request.FirstName != null) user.FirstName = request.FirstName;
        if (request.LastName != null) user.LastName = request.LastName;
        if (request.AvatarUrl != null) user.AvatarUrl = request.AvatarUrl;
        if (request.NotificationPreferences != null) user.NotificationPreferences = request.NotificationPreferences;

        await _context.SaveChangesAsync();

        return MapToDto(user);
    }

    public async Task UpdateUserStatusAsync(string userId, UpdateUserStatusRequest request)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        user.Status = request.Status;
        user.LastSeenAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task<SupportLoginResponse> SupportLoginAsync(SupportLoginRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username && u.IsActive);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid username or password");
        }

        // Get all site IDs for the user
        var siteIds = await _context.UserSites
            .Where(us => us.UserId == user.Id)
            .Select(us => us.SiteId)
            .ToListAsync();

        if (siteIds.Count == 0)
        {
            throw new UnauthorizedAccessException("User does not have access to any site");
        }

        // If SiteId is provided, verify access; otherwise use the first available site
        if (!string.IsNullOrEmpty(request.SiteId))
        {
            if (!siteIds.Contains(request.SiteId))
            {
                throw new UnauthorizedAccessException("User does not have access to this site");
            }
        }

        user.LastLoginAt = DateTime.UtcNow;
        user.LoginCount++;

        var accessToken = GenerateAccessToken(user);
        var refreshToken = await GenerateRefreshTokenAsync(user.Id);

        await _context.SaveChangesAsync();

        var expiresIn = _configuration.GetValue<int>("JwtSettings:AccessTokenExpirationMinutes") * 60;

        return new SupportLoginResponse(
            accessToken,
            refreshToken,
            expiresIn,
            new SupportUserDto(user.Id, user.Username, user.Email, user.Role, siteIds)
        );
    }

    public async Task<ValidateTokenResponse> ValidateTokenAsync(string userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null || !user.IsActive)
        {
            return new ValidateTokenResponse(false, null);
        }

        return new ValidateTokenResponse(
            true,
            new ValidateUserDto(user.Id, user.Username, user.Role)
        );
    }

    public async Task<bool> CheckEmailExistsAsync(string email)
    {
        return await _context.Users.AnyAsync(u => u.Email.ToLower() == email.ToLower());
    }

    public async Task<bool> CheckUsernameExistsAsync(string username)
    {
        return await _context.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower());
    }

    private string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["JwtSettings:Secret"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["JwtSettings:Issuer"],
            audience: _configuration["JwtSettings:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(
                _configuration.GetValue<int>("JwtSettings:AccessTokenExpirationMinutes")),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<string> GenerateRefreshTokenAsync(string userId)
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        var token = Convert.ToBase64String(randomBytes);

        var refreshToken = new RefreshToken
        {
            UserId = userId,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddDays(
                _configuration.GetValue<int>("JwtSettings:RefreshTokenExpirationDays"))
        };

        _context.RefreshTokens.Add(refreshToken);

        return token;
    }

    private static UserDto MapToDto(User user) => new(
        user.Id,
        user.Username,
        user.Email,
        user.FirstName,
        user.LastName,
        user.AvatarUrl,
        user.Role,
        user.Status,
        user.IsActive,
        user.EmailVerified,
        user.LastSeenAt,
        user.CreatedAt
    );
}
