namespace ChatApp.API.Models.DTOs;

// Login
public record LoginRequest(string Email, string Password);

// Support Agent Login (site_id is optional - auto-fetched from user's sites)
public record SupportLoginRequest(string Username, string Password, string? SiteId = null);
public record SupportLoginResponse(
    string Token,
    string RefreshToken,
    int ExpiresIn,
    SupportUserDto User
);

public record SupportUserDto(
    string Id,
    string Username,
    string Email,
    string Role,
    List<string> SiteIds
);

public record LoginResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt, UserDto User);

// Validate Token
public record ValidateTokenResponse(bool Valid, ValidateUserDto? User);
public record ValidateUserDto(string Id, string Username, string Role);

// Register
public record RegisterRequest(
    string Email,
    string Password,
    string Username,
    string? FirstName,
    string? LastName
);

// Availability Check
public record AvailabilityResponse(bool Available, string? Message);

// Refresh Token
public record RefreshTokenRequest(string RefreshToken);
public record RefreshTokenResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt);

// Change Password
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

// Forgot Password
public record ForgotPasswordRequest(string Email);
public record ForgotPasswordResponse(string Message);

// Reset Password
public record ResetPasswordRequest(string Token, string NewPassword);

// User DTOs
public record UserDto(
    string Id,
    string Username,
    string Email,
    string? FirstName,
    string? LastName,
    string? AvatarUrl,
    string Role,
    string Status,
    bool IsActive,
    bool EmailVerified,
    DateTime? LastSeenAt,
    DateTime CreatedAt
);

public record UpdateUserRequest(
    string? FirstName,
    string? LastName,
    string? AvatarUrl,
    string? NotificationPreferences
);

public record UpdateUserStatusRequest(string Status);
