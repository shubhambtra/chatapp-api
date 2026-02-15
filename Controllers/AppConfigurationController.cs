using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChatApp.API.Data;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Models.Entities;

namespace ChatApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "super_admin")]
public class AppConfigurationController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public AppConfigurationController(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    /// <summary>
    /// Get app configuration (secrets are masked)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<AppConfigurationDto>>> GetSettings()
    {
        var settings = await _context.AppConfigurations.FirstOrDefaultAsync();

        if (settings == null)
        {
            return Ok(ApiResponse<AppConfigurationDto>.Ok(BuildDefaultDto()));
        }

        return Ok(ApiResponse<AppConfigurationDto>.Ok(MapToDto(settings)));
    }

    /// <summary>
    /// Get payment gateway enable/disable status (public, no secrets exposed)
    /// </summary>
    [HttpGet("payment-gateways")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> GetPaymentGatewayStatus()
    {
        var settings = await _context.AppConfigurations.FirstOrDefaultAsync();
        var result = new
        {
            razorpayEnabled = settings?.RazorpayEnabled ?? false,
            paypalEnabled = settings?.PayPalEnabled ?? false
        };
        return Ok(ApiResponse<object>.Ok(result));
    }

    /// <summary>
    /// Update app configuration (partial updates supported)
    /// </summary>
    [HttpPut]
    public async Task<ActionResult<ApiResponse<AppConfigurationDto>>> UpdateSettings([FromBody] UpdateAppConfigurationRequest request)
    {
        var settings = await _context.AppConfigurations.FirstOrDefaultAsync();

        if (settings == null)
        {
            settings = new AppConfiguration();
            _context.AppConfigurations.Add(settings);
        }

        // OpenAI
        if (request.OpenAiApiKey != null) settings.OpenAiApiKey = request.OpenAiApiKey;
        if (request.OpenAiModel != null) settings.OpenAiModel = request.OpenAiModel;

        // Razorpay
        if (request.RazorpayKeyId != null) settings.RazorpayKeyId = request.RazorpayKeyId;
        if (request.RazorpayKeySecret != null) settings.RazorpayKeySecret = request.RazorpayKeySecret;
        if (request.RazorpayEnabled.HasValue) settings.RazorpayEnabled = request.RazorpayEnabled.Value;

        // PayPal
        if (request.PayPalClientId != null) settings.PayPalClientId = request.PayPalClientId;
        if (request.PayPalClientSecret != null) settings.PayPalClientSecret = request.PayPalClientSecret;
        if (request.PayPalMode != null) settings.PayPalMode = request.PayPalMode;
        if (request.PayPalEnabled.HasValue) settings.PayPalEnabled = request.PayPalEnabled.Value;

        // FTP
        if (request.FtpHost != null) settings.FtpHost = request.FtpHost;
        if (request.FtpUsername != null) settings.FtpUsername = request.FtpUsername;
        if (request.FtpPassword != null) settings.FtpPassword = request.FtpPassword;

        // JWT
        if (request.JwtSecret != null) settings.JwtSecret = request.JwtSecret;
        if (request.JwtIssuer != null) settings.JwtIssuer = request.JwtIssuer;
        if (request.JwtAudience != null) settings.JwtAudience = request.JwtAudience;
        if (request.JwtAccessTokenExpirationMinutes.HasValue) settings.JwtAccessTokenExpirationMinutes = request.JwtAccessTokenExpirationMinutes.Value;
        if (request.JwtRefreshTokenExpirationDays.HasValue) settings.JwtRefreshTokenExpirationDays = request.JwtRefreshTokenExpirationDays.Value;

        // File Upload
        if (request.FileUploadMaxFileSizeMB.HasValue) settings.FileUploadMaxFileSizeMB = request.FileUploadMaxFileSizeMB.Value;
        if (request.FileUploadAllowedExtensions != null) settings.FileUploadAllowedExtensions = request.FileUploadAllowedExtensions;
        if (request.FileUploadPath != null) settings.FileUploadPath = request.FileUploadPath;

        // CORS
        if (request.CorsAllowedOrigins != null) settings.CorsAllowedOrigins = request.CorsAllowedOrigins;

        // App
        if (request.AppFrontendUrl != null) settings.AppFrontendUrl = request.AppFrontendUrl;
        if (request.AppPasswordResetTokenExpirationHours.HasValue) settings.AppPasswordResetTokenExpirationHours = request.AppPasswordResetTokenExpirationHours.Value;

        // Trial Settings
        if (request.TrialDaysBeforeExpirationToNotify != null) settings.TrialDaysBeforeExpirationToNotify = request.TrialDaysBeforeExpirationToNotify;
        if (request.TrialCheckIntervalHours.HasValue) settings.TrialCheckIntervalHours = request.TrialCheckIntervalHours.Value;

        // Subscription Settings
        if (request.SubscriptionDaysBeforeExpirationToNotify != null) settings.SubscriptionDaysBeforeExpirationToNotify = request.SubscriptionDaysBeforeExpirationToNotify;

        // AutoPay Settings
        if (request.AutoPayCheckIntervalHours.HasValue) settings.AutoPayCheckIntervalHours = request.AutoPayCheckIntervalHours.Value;
        if (request.AutoPayHoursBeforeExpirationToProcess.HasValue) settings.AutoPayHoursBeforeExpirationToProcess = request.AutoPayHoursBeforeExpirationToProcess.Value;

        // IsActive
        if (request.IsActive.HasValue) settings.IsActive = request.IsActive.Value;

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<AppConfigurationDto>.Ok(MapToDto(settings), "App configuration updated successfully"));
    }

    private static string MaskSecret(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Length <= 4) return new string('*', value.Length);
        return new string('*', value.Length - 4) + value[^4..];
    }

    private AppConfigurationDto BuildDefaultDto()
    {
        return new AppConfigurationDto
        {
            // OpenAI
            OpenAiApiKey = "",
            OpenAiModel = "gpt-4o-mini",
            // Razorpay
            RazorpayKeyId = MaskSecret(_configuration["Razorpay:KeyId"]),
            RazorpayKeySecret = MaskSecret(_configuration["Razorpay:KeySecret"]),
            RazorpayEnabled = false,
            // PayPal
            PayPalClientId = MaskSecret(_configuration["PayPal:ClientId"]),
            PayPalClientSecret = MaskSecret(_configuration["PayPal:ClientSecret"]),
            PayPalMode = _configuration["PayPal:Mode"] ?? "sandbox",
            PayPalEnabled = false,
            // FTP
            FtpHost = _configuration["FTP:Host"] ?? "",
            FtpUsername = MaskSecret(_configuration["FTP:FTPHostUser"]),
            FtpPassword = MaskSecret(_configuration["FTP:FTPHostpassword"]),
            // JWT
            JwtSecret = MaskSecret(_configuration["JwtSettings:Secret"]),
            JwtIssuer = _configuration["JwtSettings:Issuer"] ?? "ChatApp.API",
            JwtAudience = _configuration["JwtSettings:Audience"] ?? "ChatApp.Client",
            JwtAccessTokenExpirationMinutes = int.TryParse(_configuration["JwtSettings:AccessTokenExpirationMinutes"], out var atExp) ? atExp : 60,
            JwtRefreshTokenExpirationDays = int.TryParse(_configuration["JwtSettings:RefreshTokenExpirationDays"], out var rtExp) ? rtExp : 7,
            // File Upload
            FileUploadMaxFileSizeMB = int.TryParse(_configuration["FileUpload:MaxFileSizeMB"], out var maxSize) ? maxSize : 10,
            FileUploadAllowedExtensions = _configuration["FileUpload:AllowedExtensions"] ?? ".jpg,.jpeg,.png,.gif,.webp,.pdf,.doc,.docx,.txt,.zip",
            FileUploadPath = _configuration["FileUpload:UploadPath"] ?? "uploads",
            // CORS
            CorsAllowedOrigins = string.Join(",", _configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>()),
            // App
            AppFrontendUrl = _configuration["App:FrontendUrl"] ?? "",
            AppPasswordResetTokenExpirationHours = int.TryParse(_configuration["App:PasswordResetTokenExpirationHours"], out var prExp) ? prExp : 24,
            // Trial
            TrialDaysBeforeExpirationToNotify = string.Join(",", _configuration.GetSection("TrialSettings:DaysBeforeExpirationToNotify").Get<int[]>() ?? new[] { 7, 3, 1 }),
            TrialCheckIntervalHours = int.TryParse(_configuration["TrialSettings:CheckIntervalHours"], out var tci) ? tci : 24,
            // Subscription
            SubscriptionDaysBeforeExpirationToNotify = string.Join(",", _configuration.GetSection("SubscriptionSettings:DaysBeforeExpirationToNotify").Get<int[]>() ?? new[] { 7, 3, 1 }),
            // AutoPay
            AutoPayCheckIntervalHours = int.TryParse(_configuration["AutoPaySettings:CheckIntervalHours"], out var apci) ? apci : 6,
            AutoPayHoursBeforeExpirationToProcess = int.TryParse(_configuration["AutoPaySettings:HoursBeforeExpirationToProcess"], out var aphb) ? aphb : 24,
            // Status
            IsActive = false
        };
    }

    private static AppConfigurationDto MapToDto(AppConfiguration settings)
    {
        return new AppConfigurationDto
        {
            Id = settings.Id,
            // OpenAI
            OpenAiApiKey = settings.OpenAiApiKey,
            OpenAiModel = settings.OpenAiModel,
            // Razorpay
            RazorpayKeyId = settings.RazorpayKeyId,
            RazorpayKeySecret = settings.RazorpayKeySecret,
            RazorpayEnabled = settings.RazorpayEnabled,
            // PayPal
            PayPalClientId = settings.PayPalClientId,
            PayPalClientSecret = settings.PayPalClientSecret,
            PayPalMode = settings.PayPalMode,
            PayPalEnabled = settings.PayPalEnabled,
            // FTP
            FtpHost = settings.FtpHost,
            FtpUsername = settings.FtpUsername,
            FtpPassword = settings.FtpPassword,
            // JWT
            JwtSecret = settings.JwtSecret,
            JwtIssuer = settings.JwtIssuer,
            JwtAudience = settings.JwtAudience,
            JwtAccessTokenExpirationMinutes = settings.JwtAccessTokenExpirationMinutes,
            JwtRefreshTokenExpirationDays = settings.JwtRefreshTokenExpirationDays,
            // File Upload
            FileUploadMaxFileSizeMB = settings.FileUploadMaxFileSizeMB,
            FileUploadAllowedExtensions = settings.FileUploadAllowedExtensions,
            FileUploadPath = settings.FileUploadPath,
            // CORS
            CorsAllowedOrigins = settings.CorsAllowedOrigins,
            // App
            AppFrontendUrl = settings.AppFrontendUrl,
            AppPasswordResetTokenExpirationHours = settings.AppPasswordResetTokenExpirationHours,
            // Trial
            TrialDaysBeforeExpirationToNotify = settings.TrialDaysBeforeExpirationToNotify,
            TrialCheckIntervalHours = settings.TrialCheckIntervalHours,
            // Subscription
            SubscriptionDaysBeforeExpirationToNotify = settings.SubscriptionDaysBeforeExpirationToNotify,
            // AutoPay
            AutoPayCheckIntervalHours = settings.AutoPayCheckIntervalHours,
            AutoPayHoursBeforeExpirationToProcess = settings.AutoPayHoursBeforeExpirationToProcess,
            // Status
            IsActive = settings.IsActive,
            UpdatedAt = settings.UpdatedAt
        };
    }
}

// DTOs
public class AppConfigurationDto
{
    public string? Id { get; set; }

    // OpenAI
    public string? OpenAiApiKey { get; set; }
    public string? OpenAiModel { get; set; }

    // Razorpay
    public string? RazorpayKeyId { get; set; }
    public string? RazorpayKeySecret { get; set; }
    public bool RazorpayEnabled { get; set; }

    // PayPal
    public string? PayPalClientId { get; set; }
    public string? PayPalClientSecret { get; set; }
    public string? PayPalMode { get; set; }
    public bool PayPalEnabled { get; set; }

    // FTP
    public string? FtpHost { get; set; }
    public string? FtpUsername { get; set; }
    public string? FtpPassword { get; set; }

    // JWT
    public string? JwtSecret { get; set; }
    public string? JwtIssuer { get; set; }
    public string? JwtAudience { get; set; }
    public int? JwtAccessTokenExpirationMinutes { get; set; }
    public int? JwtRefreshTokenExpirationDays { get; set; }

    // File Upload
    public int? FileUploadMaxFileSizeMB { get; set; }
    public string? FileUploadAllowedExtensions { get; set; }
    public string? FileUploadPath { get; set; }

    // CORS
    public string? CorsAllowedOrigins { get; set; }

    // App
    public string? AppFrontendUrl { get; set; }
    public int? AppPasswordResetTokenExpirationHours { get; set; }

    // Trial Settings
    public string? TrialDaysBeforeExpirationToNotify { get; set; }
    public int? TrialCheckIntervalHours { get; set; }

    // Subscription Settings
    public string? SubscriptionDaysBeforeExpirationToNotify { get; set; }

    // AutoPay Settings
    public int? AutoPayCheckIntervalHours { get; set; }
    public int? AutoPayHoursBeforeExpirationToProcess { get; set; }

    // Status
    public bool IsActive { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class UpdateAppConfigurationRequest
{
    // OpenAI
    public string? OpenAiApiKey { get; set; }
    public string? OpenAiModel { get; set; }

    // Razorpay
    public string? RazorpayKeyId { get; set; }
    public string? RazorpayKeySecret { get; set; }
    public bool? RazorpayEnabled { get; set; }

    // PayPal
    public string? PayPalClientId { get; set; }
    public string? PayPalClientSecret { get; set; }
    public string? PayPalMode { get; set; }
    public bool? PayPalEnabled { get; set; }

    // FTP
    public string? FtpHost { get; set; }
    public string? FtpUsername { get; set; }
    public string? FtpPassword { get; set; }

    // JWT
    public string? JwtSecret { get; set; }
    public string? JwtIssuer { get; set; }
    public string? JwtAudience { get; set; }
    public int? JwtAccessTokenExpirationMinutes { get; set; }
    public int? JwtRefreshTokenExpirationDays { get; set; }

    // File Upload
    public int? FileUploadMaxFileSizeMB { get; set; }
    public string? FileUploadAllowedExtensions { get; set; }
    public string? FileUploadPath { get; set; }

    // CORS
    public string? CorsAllowedOrigins { get; set; }

    // App
    public string? AppFrontendUrl { get; set; }
    public int? AppPasswordResetTokenExpirationHours { get; set; }

    // Trial Settings
    public string? TrialDaysBeforeExpirationToNotify { get; set; }
    public int? TrialCheckIntervalHours { get; set; }

    // Subscription Settings
    public string? SubscriptionDaysBeforeExpirationToNotify { get; set; }

    // AutoPay Settings
    public int? AutoPayCheckIntervalHours { get; set; }
    public int? AutoPayHoursBeforeExpirationToProcess { get; set; }

    // Status
    public bool? IsActive { get; set; }
}
