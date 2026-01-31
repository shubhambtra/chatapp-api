using ChatApp.API.Models.DTOs;

namespace ChatApp.API.Services.Interfaces;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request);
    Task<SupportLoginResponse> SupportLoginAsync(SupportLoginRequest request);
    Task<LoginResponse> RegisterAsync(RegisterRequest request);
    Task<RefreshTokenResponse> RefreshTokenAsync(RefreshTokenRequest request);
    Task RevokeRefreshTokenAsync(string token);
    Task ChangePasswordAsync(string userId, ChangePasswordRequest request);
    Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request);
    Task ResetPasswordAsync(ResetPasswordRequest request);
    Task<UserDto?> GetCurrentUserAsync(string userId);
    Task<UserDto> UpdateUserAsync(string userId, UpdateUserRequest request);
    Task UpdateUserStatusAsync(string userId, UpdateUserStatusRequest request);
    Task<ValidateTokenResponse> ValidateTokenAsync(string userId);
    Task<bool> CheckEmailExistsAsync(string email);
    Task<bool> CheckUsernameExistsAsync(string username);
}
