using System.ComponentModel.DataAnnotations.Schema;

namespace ChatApp.API.Models.Entities;

/// <summary>
/// Platform-wide application configuration (single row table)
/// When IsActive is true, DB values override appsettings.json
/// </summary>
public class AppConfiguration : BaseEntity
{
    // OpenAI
    [Column("openai_api_key")]
    public string? OpenAiApiKey { get; set; }

    [Column("openai_model")]
    public string? OpenAiModel { get; set; }

    // Razorpay
    [Column("razorpay_key_id")]
    public string? RazorpayKeyId { get; set; }

    [Column("razorpay_key_secret")]
    public string? RazorpayKeySecret { get; set; }

    // PayPal
    [Column("paypal_client_id")]
    public string? PayPalClientId { get; set; }

    [Column("paypal_client_secret")]
    public string? PayPalClientSecret { get; set; }

    [Column("paypal_mode")]
    public string? PayPalMode { get; set; }

    // FTP
    [Column("ftp_host")]
    public string? FtpHost { get; set; }

    [Column("ftp_username")]
    public string? FtpUsername { get; set; }

    [Column("ftp_password")]
    public string? FtpPassword { get; set; }

    // JWT
    [Column("jwt_secret")]
    public string? JwtSecret { get; set; }

    [Column("jwt_issuer")]
    public string? JwtIssuer { get; set; }

    [Column("jwt_audience")]
    public string? JwtAudience { get; set; }

    [Column("jwt_access_token_expiration_minutes")]
    public int? JwtAccessTokenExpirationMinutes { get; set; }

    [Column("jwt_refresh_token_expiration_days")]
    public int? JwtRefreshTokenExpirationDays { get; set; }

    // File Upload
    [Column("file_upload_max_file_size_mb")]
    public int? FileUploadMaxFileSizeMB { get; set; }

    [Column("file_upload_allowed_extensions")]
    public string? FileUploadAllowedExtensions { get; set; }

    [Column("file_upload_path")]
    public string? FileUploadPath { get; set; }

    // CORS
    [Column("cors_allowed_origins")]
    public string? CorsAllowedOrigins { get; set; }

    // App
    [Column("app_frontend_url")]
    public string? AppFrontendUrl { get; set; }

    [Column("app_password_reset_token_expiration_hours")]
    public int? AppPasswordResetTokenExpirationHours { get; set; }

    // Trial Settings
    [Column("trial_days_before_expiration_to_notify")]
    public string? TrialDaysBeforeExpirationToNotify { get; set; }

    [Column("trial_check_interval_hours")]
    public int? TrialCheckIntervalHours { get; set; }

    // Subscription Settings
    [Column("subscription_days_before_expiration_to_notify")]
    public string? SubscriptionDaysBeforeExpirationToNotify { get; set; }

    // AutoPay Settings
    [Column("autopay_check_interval_hours")]
    public int? AutoPayCheckIntervalHours { get; set; }

    [Column("autopay_hours_before_expiration_to_process")]
    public int? AutoPayHoursBeforeExpirationToProcess { get; set; }

    // Active flag - when true, use DB values; when false, fallback to appsettings.json
    [Column("is_active")]
    public bool IsActive { get; set; } = false;
}
