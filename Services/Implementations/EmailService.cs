using System.Net;
using System.Net.Mail;
using ChatApp.API.Data;
using ChatApp.API.Models.Entities;
using ChatApp.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ChatApp.API.Services.Implementations;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private readonly ApplicationDbContext _dbContext;

    // Cached SMTP settings
    private string _smtpHost = string.Empty;
    private int _smtpPort;
    private string _smtpUsername = string.Empty;
    private string _smtpPassword = string.Empty;
    private string _fromEmail = string.Empty;
    private string _fromName = string.Empty;
    private bool _enableSsl;
    private bool _settingsLoaded = false;

    // Cached brand settings
    private string _brandName = "Assistica AI";
    private string _supportEmail = "support@assistica.com";
    private string _frontendUrl = "";

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger, ApplicationDbContext dbContext)
    {
        _configuration = configuration;
        _logger = logger;
        _dbContext = dbContext;
    }

    private async Task LoadSmtpSettingsAsync()
    {
        // Always load fresh settings from database (no caching)
        // This allows SMTP settings to be updated without restarting the API

        // Try to load from database first
        var dbSettings = await _dbContext.SmtpSettings.FirstOrDefaultAsync();

        if (dbSettings != null && dbSettings.IsActive)
        {
            _smtpHost = dbSettings.SmtpHost;
            _smtpPort = dbSettings.SmtpPort;
            _smtpUsername = dbSettings.SmtpUsername;
            _smtpPassword = dbSettings.SmtpPassword;
            _fromEmail = dbSettings.FromEmail;
            _fromName = dbSettings.FromName;
            _enableSsl = dbSettings.EnableSsl;
            _logger.LogInformation("SMTP settings loaded from database");
        }
        else
        {
            // Fall back to appsettings.json
            _smtpHost = _configuration["Email:SmtpHost"] ?? "smtp.gmail.com";
            _smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
            _smtpUsername = _configuration["Email:SmtpUsername"] ?? "";
            _smtpPassword = _configuration["Email:SmtpPassword"] ?? "";
            _fromEmail = _configuration["Email:FromEmail"] ?? "noreply@assistica.com";
            _fromName = _configuration["Email:FromName"] ?? "Assistica AI";
            _enableSsl = bool.Parse(_configuration["Email:EnableSsl"] ?? "true");
            _logger.LogInformation("SMTP settings loaded from appsettings.json (DB settings not active or not found)");
        }

        // Load brand settings from SiteSettings
        await LoadBrandSettingsAsync();
    }

    private async Task LoadBrandSettingsAsync()
    {
        try
        {
            var siteSettings = await _dbContext.SiteSettings.FirstOrDefaultAsync();
            if (siteSettings != null)
            {
                _brandName = siteSettings.SiteName ?? "Assistica AI";
                _supportEmail = siteSettings.SupportEmail ?? "support@assistica.com";
            }

            var appConfig = await _dbContext.AppConfigurations.FirstOrDefaultAsync();
            if (appConfig != null && !string.IsNullOrEmpty(appConfig.AppFrontendUrl))
            {
                _frontendUrl = appConfig.AppFrontendUrl;
            }
            else
            {
                _frontendUrl = _configuration["App:FrontendUrl"] ?? _frontendUrl;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load brand settings, using defaults");
        }
    }

    public async Task SendEmailAsync(string to, string subject, string htmlBody, string? plainTextBody = null, string? emailType = null, string? siteId = null, string? userId = null)
    {
        await SendEmailAsync(new List<string> { to }, subject, htmlBody, plainTextBody, emailType, siteId, userId);
    }

    public async Task SendEmailAsync(List<string> to, string subject, string htmlBody, string? plainTextBody = null, string? emailType = null, string? siteId = null, string? userId = null)
    {
        // Load SMTP settings from DB or config
        await LoadSmtpSettingsAsync();

        foreach (var recipient in to)
        {
            var emailLog = new EmailLog
            {
                FromEmail = _fromEmail,
                FromName = _fromName,
                ToEmail = recipient,
                Subject = subject,
                Body = htmlBody,
                IsHtml = true,
                Status = "pending",
                EmailType = emailType,
                SiteId = siteId,
                UserId = userId
            };

            try
            {
                // Bypass SSL certificate validation for SMTP servers with mismatched certificates
                System.Net.ServicePointManager.ServerCertificateValidationCallback =
                    (sender, certificate, chain, sslPolicyErrors) => true;

                using var client = new SmtpClient(_smtpHost, _smtpPort)
                {
                    Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
                    EnableSsl = _enableSsl
                };

                var message = new MailMessage
                {
                    From = new MailAddress(_fromEmail, _fromName),
                    Subject = subject,
                    IsBodyHtml = true,
                    Body = htmlBody
                };

                message.To.Add(recipient);

                // Add plain text alternative if provided
                if (!string.IsNullOrEmpty(plainTextBody))
                {
                    var plainView = AlternateView.CreateAlternateViewFromString(plainTextBody, null, "text/plain");
                    var htmlView = AlternateView.CreateAlternateViewFromString(htmlBody, null, "text/html");
                    message.AlternateViews.Add(plainView);
                    message.AlternateViews.Add(htmlView);
                }

                await client.SendMailAsync(message);

                emailLog.Status = "sent";
                emailLog.SentAt = DateTime.UtcNow;
                _logger.LogInformation("Email sent successfully to {Recipient}", recipient);
            }
            catch (Exception ex)
            {
                emailLog.Status = "failed";
                emailLog.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Failed to send email to {Recipient}", recipient);
                throw;
            }
            finally
            {
                // Always log the email attempt
                _dbContext.EmailLogs.Add(emailLog);
                await _dbContext.SaveChangesAsync();
            }
        }
    }

    public async Task SendWelcomeEmailAsync(string email, string username, string siteName, string? siteId = null, string? userId = null)
    {
        await LoadBrandSettingsAsync();
        var subject = $"Welcome to {_brandName} - {siteName} is ready!";
        var htmlBody = GetWelcomeEmailTemplate(username, siteName);
        await SendEmailAsync(email, subject, htmlBody, null, "welcome", siteId, userId);
    }

    public async Task SendTrialExpirationWarningAsync(string email, string username, string siteName, int daysRemaining, string? siteId = null, string? userId = null)
    {
        await LoadBrandSettingsAsync();
        var subject = daysRemaining == 1
            ? $"Your {siteName} trial expires tomorrow!"
            : $"Your {siteName} trial expires in {daysRemaining} days";
        var htmlBody = GetTrialExpirationWarningTemplate(username, siteName, daysRemaining);
        await SendEmailAsync(email, subject, htmlBody, null, "trial_expiration_warning", siteId, userId);
    }

    public async Task SendTrialExpiredEmailAsync(string email, string username, string siteName, string? siteId = null, string? userId = null)
    {
        await LoadBrandSettingsAsync();
        var subject = $"Your {siteName} trial has expired";
        var htmlBody = GetTrialExpiredTemplate(username, siteName);
        await SendEmailAsync(email, subject, htmlBody, null, "trial_expired", siteId, userId);
    }

    public async Task SendSubscriptionConfirmationAsync(string email, string username, string siteName, string planName, decimal amount, string? siteId = null, string? userId = null)
    {
        await LoadBrandSettingsAsync();
        var subject = $"Subscription Confirmed - {planName} Plan for {siteName}";
        var htmlBody = GetSubscriptionConfirmationTemplate(username, siteName, planName, amount);
        await SendEmailAsync(email, subject, htmlBody, null, "subscription_confirmation", siteId, userId);
    }

    public async Task SendSubscriptionCancelledAsync(string email, string username, string siteName, DateTime endDate, string? siteId = null, string? userId = null)
    {
        await LoadBrandSettingsAsync();
        var subject = $"Subscription Cancelled - {siteName}";
        var htmlBody = GetSubscriptionCancelledTemplate(username, siteName, endDate);
        await SendEmailAsync(email, subject, htmlBody, null, "subscription_cancelled", siteId, userId);
    }

    public async Task SendPaymentFailedAsync(string email, string username, string siteName, string reason, string? siteId = null, string? userId = null)
    {
        await LoadBrandSettingsAsync();
        var subject = $"Payment Failed - Action Required for {siteName}";
        var htmlBody = GetPaymentFailedTemplate(username, siteName, reason);
        await SendEmailAsync(email, subject, htmlBody, null, "payment_failed", siteId, userId);
    }

    public async Task SendPasswordResetAsync(string email, string username, string resetLink, string? userId = null)
    {
        await LoadBrandSettingsAsync();
        var subject = $"Reset Your {_brandName} Password";
        var htmlBody = GetPasswordResetTemplate(username, resetLink);
        await SendEmailAsync(email, subject, htmlBody, null, "password_reset", null, userId);
    }

    public async Task SendPlanExpirationWarningAsync(string email, string username, string siteName, string planName, int daysRemaining, bool isCancelled, string? siteId = null, string? userId = null)
    {
        await LoadBrandSettingsAsync();
        var subject = daysRemaining == 1
            ? $"Your {planName} plan for {siteName} expires tomorrow!"
            : $"Your {planName} plan for {siteName} expires in {daysRemaining} days";
        var htmlBody = GetPlanExpirationWarningTemplate(username, siteName, planName, daysRemaining, isCancelled);
        await SendEmailAsync(email, subject, htmlBody, null, "plan_expiration_warning", siteId, userId);
    }

    public async Task SendPlanExpiredEmailAsync(string email, string username, string siteName, string planName, string? siteId = null, string? userId = null)
    {
        await LoadBrandSettingsAsync();
        var subject = $"Your {planName} plan for {siteName} has expired";
        var htmlBody = GetPlanExpiredTemplate(username, siteName, planName);
        await SendEmailAsync(email, subject, htmlBody, null, "plan_expired", siteId, userId);
    }

    // Email Templates
    private string GetBaseTemplate(string content)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>{_brandName}</title>
</head>
<body style='margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, ""Helvetica Neue"", Arial, sans-serif; background-color: #f4f4f5;'>
    <table role='presentation' cellspacing='0' cellpadding='0' border='0' width='100%' style='background-color: #f4f4f5;'>
        <tr>
            <td style='padding: 40px 20px;'>
                <table role='presentation' cellspacing='0' cellpadding='0' border='0' width='600' style='margin: 0 auto; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);'>
                    <!-- Header -->
                    <tr>
                        <td style='background: linear-gradient(135deg, #1e3a5f 0%, #0f172a 100%); padding: 30px 40px; text-align: center;'>
                            <h1 style='margin: 0; color: #ffffff; font-size: 28px; font-weight: 700;'>{_brandName}</h1>
                        </td>
                    </tr>
                    <!-- Content -->
                    <tr>
                        <td style='padding: 40px;'>
                            {content}
                        </td>
                    </tr>
                    <!-- Footer -->
                    <tr>
                        <td style='background-color: #f8fafc; padding: 24px 40px; text-align: center; border-top: 1px solid #e2e8f0;'>
                            <p style='margin: 0 0 8px 0; color: #64748b; font-size: 14px;'>
                                Need help? <a href='mailto:{_supportEmail}' style='color: #2563eb; text-decoration: none;'>Contact Support</a>
                            </p>
                            <p style='margin: 0; color: #94a3b8; font-size: 12px;'>
                                &copy; {DateTime.Now.Year} {_brandName}. All rights reserved.
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    private string GetWelcomeEmailTemplate(string username, string siteName)
    {
        var content = $@"
            <h2 style='margin: 0 0 16px 0; color: #1e293b; font-size: 24px;'>Welcome to {_brandName}, {username}!</h2>
            <p style='margin: 0 0 24px 0; color: #475569; font-size: 16px; line-height: 1.6;'>
                Your site <strong>{siteName}</strong> has been created and is ready to use. You can now start adding the chat widget to your website and engage with your customers in real-time.
            </p>
            <h3 style='margin: 0 0 12px 0; color: #1e293b; font-size: 18px;'>Next Steps:</h3>
            <ul style='margin: 0 0 24px 0; padding-left: 24px; color: #475569; font-size: 16px; line-height: 1.8;'>
                <li>Add the chat widget to your website</li>
                <li>Customize your widget appearance</li>
                <li>Set up welcome messages</li>
                <li>Invite team members</li>
            </ul>
            <a href='{_frontendUrl}/site-admin-overview.html' style='display: inline-block; background: linear-gradient(135deg, #0ea5e9, #2563eb); color: #ffffff; text-decoration: none; padding: 14px 32px; border-radius: 8px; font-weight: 600; font-size: 16px;'>Go to Dashboard</a>";
        return GetBaseTemplate(content);
    }

    private string GetTrialExpirationWarningTemplate(string username, string siteName, int daysRemaining)
    {
        var urgencyColor = daysRemaining <= 1 ? "#dc2626" : daysRemaining <= 3 ? "#f59e0b" : "#2563eb";
        var content = $@"
            <h2 style='margin: 0 0 16px 0; color: #1e293b; font-size: 24px;'>Hi {username},</h2>
            <div style='background-color: #fef3c7; border-left: 4px solid {urgencyColor}; padding: 16px 20px; margin-bottom: 24px; border-radius: 0 8px 8px 0;'>
                <p style='margin: 0; color: #92400e; font-size: 16px; font-weight: 600;'>
                    Your trial for <strong>{siteName}</strong> expires in <span style='color: {urgencyColor};'>{daysRemaining} day{(daysRemaining > 1 ? "s" : "")}</span>
                </p>
            </div>
            <p style='margin: 0 0 24px 0; color: #475569; font-size: 16px; line-height: 1.6;'>
                Don't lose access to your conversations and customer data. Upgrade now to continue using all features without interruption.
            </p>
            <h3 style='margin: 0 0 12px 0; color: #1e293b; font-size: 18px;'>What you'll lose without upgrading:</h3>
            <ul style='margin: 0 0 24px 0; padding-left: 24px; color: #475569; font-size: 16px; line-height: 1.8;'>
                <li>Real-time chat with customers</li>
                <li>AI-powered response suggestions</li>
                <li>Conversation history and analytics</li>
                <li>Custom widget branding</li>
            </ul>
            <a href='{_frontendUrl}/site-admin-subscription.html' style='display: inline-block; background: linear-gradient(135deg, #0ea5e9, #2563eb); color: #ffffff; text-decoration: none; padding: 14px 32px; border-radius: 8px; font-weight: 600; font-size: 16px;'>Upgrade Now</a>";
        return GetBaseTemplate(content);
    }

    private string GetTrialExpiredTemplate(string username, string siteName)
    {
        var content = $@"
            <h2 style='margin: 0 0 16px 0; color: #1e293b; font-size: 24px;'>Hi {username},</h2>
            <div style='background-color: #fee2e2; border-left: 4px solid #dc2626; padding: 16px 20px; margin-bottom: 24px; border-radius: 0 8px 8px 0;'>
                <p style='margin: 0; color: #991b1b; font-size: 16px; font-weight: 600;'>
                    Your trial for <strong>{siteName}</strong> has expired
                </p>
            </div>
            <p style='margin: 0 0 24px 0; color: #475569; font-size: 16px; line-height: 1.6;'>
                Your site is now on the Free plan with limited features. Upgrade today to restore full access and continue delivering excellent customer support.
            </p>
            <p style='margin: 0 0 24px 0; color: #475569; font-size: 16px; line-height: 1.6;'>
                Don't worry - all your data is safe and will be available once you upgrade.
            </p>
            <a href='{_frontendUrl}/site-admin-subscription.html' style='display: inline-block; background: linear-gradient(135deg, #0ea5e9, #2563eb); color: #ffffff; text-decoration: none; padding: 14px 32px; border-radius: 8px; font-weight: 600; font-size: 16px;'>Upgrade Now</a>";
        return GetBaseTemplate(content);
    }

    private string GetSubscriptionConfirmationTemplate(string username, string siteName, string planName, decimal amount)
    {
        var content = $@"
            <h2 style='margin: 0 0 16px 0; color: #1e293b; font-size: 24px;'>Payment Confirmed!</h2>
            <div style='background-color: #dcfce7; border-left: 4px solid #22c55e; padding: 16px 20px; margin-bottom: 24px; border-radius: 0 8px 8px 0;'>
                <p style='margin: 0; color: #166534; font-size: 16px; font-weight: 600;'>
                    Thank you for upgrading to the <strong>{planName}</strong> plan!
                </p>
            </div>
            <p style='margin: 0 0 24px 0; color: #475569; font-size: 16px; line-height: 1.6;'>
                Hi {username}, your subscription for <strong>{siteName}</strong> has been activated.
            </p>
            <table style='width: 100%; border-collapse: collapse; margin-bottom: 24px;'>
                <tr>
                    <td style='padding: 12px 0; border-bottom: 1px solid #e2e8f0; color: #64748b;'>Plan</td>
                    <td style='padding: 12px 0; border-bottom: 1px solid #e2e8f0; color: #1e293b; text-align: right; font-weight: 600;'>{planName}</td>
                </tr>
                <tr>
                    <td style='padding: 12px 0; border-bottom: 1px solid #e2e8f0; color: #64748b;'>Amount</td>
                    <td style='padding: 12px 0; border-bottom: 1px solid #e2e8f0; color: #1e293b; text-align: right; font-weight: 600;'>${amount:F2}</td>
                </tr>
                <tr>
                    <td style='padding: 12px 0; color: #64748b;'>Site</td>
                    <td style='padding: 12px 0; color: #1e293b; text-align: right; font-weight: 600;'>{siteName}</td>
                </tr>
            </table>
            <a href='{_frontendUrl}/site-admin-overview.html' style='display: inline-block; background: linear-gradient(135deg, #0ea5e9, #2563eb); color: #ffffff; text-decoration: none; padding: 14px 32px; border-radius: 8px; font-weight: 600; font-size: 16px;'>Go to Dashboard</a>";
        return GetBaseTemplate(content);
    }

    private string GetSubscriptionCancelledTemplate(string username, string siteName, DateTime endDate)
    {
        var content = $@"
            <h2 style='margin: 0 0 16px 0; color: #1e293b; font-size: 24px;'>Subscription Cancelled</h2>
            <p style='margin: 0 0 24px 0; color: #475569; font-size: 16px; line-height: 1.6;'>
                Hi {username}, we're sorry to see you go. Your subscription for <strong>{siteName}</strong> has been cancelled.
            </p>
            <div style='background-color: #f1f5f9; padding: 20px; border-radius: 8px; margin-bottom: 24px;'>
                <p style='margin: 0; color: #475569; font-size: 16px;'>
                    You'll continue to have access to all features until <strong style='color: #1e293b;'>{endDate:MMMM d, yyyy}</strong>
                </p>
            </div>
            <p style='margin: 0 0 24px 0; color: #475569; font-size: 16px; line-height: 1.6;'>
                Changed your mind? You can reactivate your subscription anytime before the end date.
            </p>
            <a href='{_frontendUrl}/site-admin-subscription.html' style='display: inline-block; background: linear-gradient(135deg, #0ea5e9, #2563eb); color: #ffffff; text-decoration: none; padding: 14px 32px; border-radius: 8px; font-weight: 600; font-size: 16px;'>Reactivate Subscription</a>";
        return GetBaseTemplate(content);
    }

    private string GetPaymentFailedTemplate(string username, string siteName, string reason)
    {
        var content = $@"
            <h2 style='margin: 0 0 16px 0; color: #1e293b; font-size: 24px;'>Payment Failed</h2>
            <div style='background-color: #fee2e2; border-left: 4px solid #dc2626; padding: 16px 20px; margin-bottom: 24px; border-radius: 0 8px 8px 0;'>
                <p style='margin: 0; color: #991b1b; font-size: 16px; font-weight: 600;'>
                    We couldn't process your payment for <strong>{siteName}</strong>
                </p>
            </div>
            <p style='margin: 0 0 16px 0; color: #475569; font-size: 16px; line-height: 1.6;'>
                Hi {username}, your recent payment attempt failed. Reason: <strong>{reason}</strong>
            </p>
            <p style='margin: 0 0 24px 0; color: #475569; font-size: 16px; line-height: 1.6;'>
                Please update your payment method to avoid service interruption.
            </p>
            <a href='{_frontendUrl}/site-admin-billing.html' style='display: inline-block; background: linear-gradient(135deg, #0ea5e9, #2563eb); color: #ffffff; text-decoration: none; padding: 14px 32px; border-radius: 8px; font-weight: 600; font-size: 16px;'>Update Payment Method</a>";
        return GetBaseTemplate(content);
    }

    private string GetPasswordResetTemplate(string username, string resetLink)
    {
        var content = $@"
            <h2 style='margin: 0 0 16px 0; color: #1e293b; font-size: 24px;'>Reset Your Password</h2>
            <p style='margin: 0 0 24px 0; color: #475569; font-size: 16px; line-height: 1.6;'>
                Hi {username}, we received a request to reset your password. Click the button below to create a new password.
            </p>
            <a href='{resetLink}' style='display: inline-block; background: linear-gradient(135deg, #0ea5e9, #2563eb); color: #ffffff; text-decoration: none; padding: 14px 32px; border-radius: 8px; font-weight: 600; font-size: 16px;'>Reset Password</a>
            <p style='margin: 24px 0 0 0; color: #64748b; font-size: 14px; line-height: 1.6;'>
                This link will expire in 24 hours. If you didn't request this, you can safely ignore this email.
            </p>";
        return GetBaseTemplate(content);
    }

    private string GetPlanExpirationWarningTemplate(string username, string siteName, string planName, int daysRemaining, bool isCancelled)
    {
        var urgencyColor = daysRemaining <= 1 ? "#dc2626" : daysRemaining <= 3 ? "#f59e0b" : "#2563eb";
        var headerMessage = isCancelled
            ? $"Your cancelled <strong>{planName}</strong> plan for <strong>{siteName}</strong> ends in <span style='color: {urgencyColor};'>{daysRemaining} day{(daysRemaining > 1 ? "s" : "")}</span>"
            : $"Your <strong>{planName}</strong> plan for <strong>{siteName}</strong> expires in <span style='color: {urgencyColor};'>{daysRemaining} day{(daysRemaining > 1 ? "s" : "")}</span>";

        var actionMessage = isCancelled
            ? "You previously cancelled your subscription. After this date, your site will be downgraded to the Free plan."
            : "Your subscription period is ending soon. Renew now to continue enjoying all premium features.";

        var buttonText = isCancelled ? "Reactivate Subscription" : "Renew Now";

        var content = $@"
            <h2 style='margin: 0 0 16px 0; color: #1e293b; font-size: 24px;'>Hi {username},</h2>
            <div style='background-color: #fef3c7; border-left: 4px solid {urgencyColor}; padding: 16px 20px; margin-bottom: 24px; border-radius: 0 8px 8px 0;'>
                <p style='margin: 0; color: #92400e; font-size: 16px; font-weight: 600;'>
                    {headerMessage}
                </p>
            </div>
            <p style='margin: 0 0 24px 0; color: #475569; font-size: 16px; line-height: 1.6;'>
                {actionMessage}
            </p>
            <h3 style='margin: 0 0 12px 0; color: #1e293b; font-size: 18px;'>What you'll lose without your {planName} plan:</h3>
            <ul style='margin: 0 0 24px 0; padding-left: 24px; color: #475569; font-size: 16px; line-height: 1.8;'>
                <li>Premium conversation limits</li>
                <li>Advanced AI-powered features</li>
                <li>Extended message history</li>
                <li>Priority support</li>
            </ul>
            <a href='{_frontendUrl}/site-admin-subscription.html' style='display: inline-block; background: linear-gradient(135deg, #0ea5e9, #2563eb); color: #ffffff; text-decoration: none; padding: 14px 32px; border-radius: 8px; font-weight: 600; font-size: 16px;'>{buttonText}</a>";
        return GetBaseTemplate(content);
    }

    private string GetPlanExpiredTemplate(string username, string siteName, string planName)
    {
        var content = $@"
            <h2 style='margin: 0 0 16px 0; color: #1e293b; font-size: 24px;'>Hi {username},</h2>
            <div style='background-color: #fee2e2; border-left: 4px solid #dc2626; padding: 16px 20px; margin-bottom: 24px; border-radius: 0 8px 8px 0;'>
                <p style='margin: 0; color: #991b1b; font-size: 16px; font-weight: 600;'>
                    Your <strong>{planName}</strong> plan for <strong>{siteName}</strong> has expired
                </p>
            </div>
            <p style='margin: 0 0 24px 0; color: #475569; font-size: 16px; line-height: 1.6;'>
                Your subscription has ended and your site has been downgraded to the Free plan with limited features.
            </p>
            <p style='margin: 0 0 24px 0; color: #475569; font-size: 16px; line-height: 1.6;'>
                Don't worry - all your data is safe. Upgrade anytime to restore full access to premium features.
            </p>
            <h3 style='margin: 0 0 12px 0; color: #1e293b; font-size: 18px;'>Restore your premium features:</h3>
            <ul style='margin: 0 0 24px 0; padding-left: 24px; color: #475569; font-size: 16px; line-height: 1.8;'>
                <li>Higher conversation and message limits</li>
                <li>AI analysis and auto-reply</li>
                <li>Extended message history</li>
                <li>Priority customer support</li>
            </ul>
            <a href='{_frontendUrl}/site-admin-subscription.html' style='display: inline-block; background: linear-gradient(135deg, #0ea5e9, #2563eb); color: #ffffff; text-decoration: none; padding: 14px 32px; border-radius: 8px; font-weight: 600; font-size: 16px;'>Upgrade Now</a>";
        return GetBaseTemplate(content);
    }

    public async Task SendAutoPaySuccessEmailAsync(string email, string username, string siteName, string planName, decimal amount, string currency, DateTime nextPeriodEnd, string? siteId = null, string? userId = null)
    {
        await LoadBrandSettingsAsync();
        var subject = $"Payment Successful - {planName} subscription renewed for {siteName}";
        var htmlBody = GetAutoPaySuccessTemplate(username, siteName, planName, amount, currency, nextPeriodEnd);
        await SendEmailAsync(email, subject, htmlBody, null, "autopay_success", siteId, userId);
    }

    public async Task SendAutoPayFailedEmailAsync(string email, string username, string siteName, string planName, string reason, DateTime periodEnd, string? siteId = null, string? userId = null)
    {
        await LoadBrandSettingsAsync();
        var subject = $"Action Required: Auto-pay failed for {siteName}";
        var htmlBody = GetAutoPayFailedTemplate(username, siteName, planName, reason, periodEnd);
        await SendEmailAsync(email, subject, htmlBody, null, "autopay_failed", siteId, userId);
    }

    private string GetAutoPaySuccessTemplate(string username, string siteName, string planName, decimal amount, string currency, DateTime nextPeriodEnd)
    {
        var content = $@"
            <h2 style='margin: 0 0 16px 0; color: #1e293b; font-size: 24px;'>Hi {username},</h2>
            <div style='background-color: #dcfce7; border-left: 4px solid #16a34a; padding: 16px 20px; margin-bottom: 24px; border-radius: 0 8px 8px 0;'>
                <p style='margin: 0; color: #166534; font-size: 16px; font-weight: 600;'>
                    Your subscription has been automatically renewed!
                </p>
            </div>
            <p style='margin: 0 0 24px 0; color: #475569; font-size: 16px; line-height: 1.6;'>
                Your <strong>{planName}</strong> plan for <strong>{siteName}</strong> has been successfully renewed via auto-pay.
            </p>
            <div style='background-color: #f8fafc; border-radius: 8px; padding: 20px; margin-bottom: 24px;'>
                <h3 style='margin: 0 0 16px 0; color: #1e293b; font-size: 16px;'>Payment Details</h3>
                <table style='width: 100%; border-collapse: collapse;'>
                    <tr>
                        <td style='padding: 8px 0; color: #64748b;'>Plan</td>
                        <td style='padding: 8px 0; color: #1e293b; text-align: right; font-weight: 600;'>{planName}</td>
                    </tr>
                    <tr>
                        <td style='padding: 8px 0; color: #64748b;'>Amount Charged</td>
                        <td style='padding: 8px 0; color: #1e293b; text-align: right; font-weight: 600;'>{currency} {amount:F2}</td>
                    </tr>
                    <tr>
                        <td style='padding: 8px 0; color: #64748b;'>Next Renewal</td>
                        <td style='padding: 8px 0; color: #1e293b; text-align: right; font-weight: 600;'>{nextPeriodEnd:MMMM d, yyyy}</td>
                    </tr>
                </table>
            </div>
            <p style='margin: 0 0 24px 0; color: #475569; font-size: 14px; line-height: 1.6;'>
                Thank you for continuing to use {_brandName}! Your subscription will automatically renew on the date shown above.
            </p>
            <a href='{_frontendUrl}/site-admin-overview.html' style='display: inline-block; background: linear-gradient(135deg, #0ea5e9, #2563eb); color: #ffffff; text-decoration: none; padding: 14px 32px; border-radius: 8px; font-weight: 600; font-size: 16px;'>View Dashboard</a>";
        return GetBaseTemplate(content);
    }

    private string GetAutoPayFailedTemplate(string username, string siteName, string planName, string reason, DateTime periodEnd)
    {
        var content = $@"
            <h2 style='margin: 0 0 16px 0; color: #1e293b; font-size: 24px;'>Hi {username},</h2>
            <div style='background-color: #fee2e2; border-left: 4px solid #dc2626; padding: 16px 20px; margin-bottom: 24px; border-radius: 0 8px 8px 0;'>
                <p style='margin: 0; color: #991b1b; font-size: 16px; font-weight: 600;'>
                    Auto-pay failed for your {planName} subscription
                </p>
            </div>
            <p style='margin: 0 0 24px 0; color: #475569; font-size: 16px; line-height: 1.6;'>
                We were unable to process the automatic payment for your <strong>{planName}</strong> plan for <strong>{siteName}</strong>.
            </p>
            <div style='background-color: #fef3c7; border-radius: 8px; padding: 16px 20px; margin-bottom: 24px;'>
                <p style='margin: 0; color: #92400e; font-size: 14px;'>
                    <strong>Reason:</strong> {reason}
                </p>
            </div>
            <p style='margin: 0 0 16px 0; color: #475569; font-size: 16px; line-height: 1.6;'>
                <strong>Your subscription will expire on {periodEnd:MMMM d, yyyy}</strong> unless you take action.
            </p>
            <p style='margin: 0 0 24px 0; color: #475569; font-size: 16px; line-height: 1.6;'>
                To avoid service interruption, please:
            </p>
            <ul style='margin: 0 0 24px 0; padding-left: 24px; color: #475569; font-size: 16px; line-height: 1.8;'>
                <li>Update your payment method</li>
                <li>Ensure sufficient funds are available</li>
                <li>Re-enable auto-pay after updating</li>
            </ul>
            <a href='{_frontendUrl}/site-admin-billing.html' style='display: inline-block; background: linear-gradient(135deg, #0ea5e9, #2563eb); color: #ffffff; text-decoration: none; padding: 14px 32px; border-radius: 8px; font-weight: 600; font-size: 16px;'>Update Payment Method</a>
            <p style='margin: 24px 0 0 0; color: #64748b; font-size: 14px; line-height: 1.6;'>
                Auto-pay has been temporarily disabled for this subscription. You can re-enable it after updating your payment method.
            </p>";
        return GetBaseTemplate(content);
    }

    public Task SendEmailAsync(string to, string subject, string htmlBody, string? plainTextBody = null)
    {
        return SendEmailAsync(to, subject, htmlBody, plainTextBody, null, null, null);
    }

    public Task SendEmailAsync(List<string> to, string subject, string htmlBody, string? plainTextBody = null)
    {
        return SendEmailAsync(to, subject, htmlBody, plainTextBody, null, null, null);
    }

    public Task SendWelcomeEmailAsync(string email, string username, string siteName)
    {
        return SendWelcomeEmailAsync(email, username, siteName, null, null);
    }

    public Task SendTrialExpirationWarningAsync(string email, string username, string siteName, int daysRemaining)
    {
        return SendTrialExpirationWarningAsync(email, username, siteName, daysRemaining, null, null);
    }

    public Task SendTrialExpiredEmailAsync(string email, string username, string siteName)
    {
        return SendTrialExpiredEmailAsync(email, username, siteName, null, null);
    }

    public Task SendSubscriptionConfirmationAsync(string email, string username, string siteName, string planName, decimal amount)
    {
        return SendSubscriptionConfirmationAsync(email, username, siteName, planName, amount, null, null);
    }

    public Task SendSubscriptionCancelledAsync(string email, string username, string siteName, DateTime endDate)
    {
        return SendSubscriptionCancelledAsync(email, username, siteName, endDate, null, null);
    }

    public Task SendPaymentFailedAsync(string email, string username, string siteName, string reason)
    {
        return SendPaymentFailedAsync(email, username, siteName, reason, null, null);
    }

    public Task SendPasswordResetAsync(string email, string username, string resetLink)
    {
        return SendPasswordResetAsync(email, username, resetLink, null);
    }

    public Task SendPlanExpirationWarningAsync(string email, string username, string siteName, string planName, int daysRemaining, bool isCancelled)
    {
        return SendPlanExpirationWarningAsync(email, username, siteName, planName, daysRemaining, isCancelled, null, null);
    }

    public Task SendPlanExpiredEmailAsync(string email, string username, string siteName, string planName)
    {
        return SendPlanExpiredEmailAsync(email, username, siteName, planName, null, null);
    }

    public Task SendAutoPaySuccessEmailAsync(string email, string username, string siteName, string planName, decimal amount, string currency, DateTime nextPeriodEnd)
    {
        return SendAutoPaySuccessEmailAsync(email, username, siteName, planName, amount, currency, nextPeriodEnd, null, null);
    }

    public Task SendAutoPayFailedEmailAsync(string email, string username, string siteName, string planName, string reason, DateTime periodEnd)
    {
        return SendAutoPayFailedEmailAsync(email, username, siteName, planName, reason, periodEnd, null, null);
    }

    public async Task SendPaymentReceiptEmailAsync(string email, string username, string siteName, string planName,
        string invoiceNumber, string transactionId, decimal amount, string currency,
        string billingCycle, DateTime periodStart, DateTime periodEnd, string paymentGateway,
        string? siteId = null, string? userId = null)
    {
        await LoadBrandSettingsAsync();
        var subject = $"Payment Receipt - {invoiceNumber} for {siteName}";
        var htmlBody = GetPaymentReceiptTemplate(username, siteName, planName, invoiceNumber, transactionId,
            amount, currency, billingCycle, periodStart, periodEnd, paymentGateway);
        await SendEmailAsync(email, subject, htmlBody, null, "payment_receipt", siteId, userId);
    }

    public Task SendPaymentReceiptEmailAsync(string email, string username, string siteName, string planName,
        string invoiceNumber, string transactionId, decimal amount, string currency,
        string billingCycle, DateTime periodStart, DateTime periodEnd, string paymentGateway)
    {
        return SendPaymentReceiptEmailAsync(email, username, siteName, planName, invoiceNumber, transactionId,
            amount, currency, billingCycle, periodStart, periodEnd, paymentGateway, null, null);
    }

    private string GetPaymentReceiptTemplate(string username, string siteName, string planName,
        string invoiceNumber, string transactionId, decimal amount, string currency,
        string billingCycle, DateTime periodStart, DateTime periodEnd, string paymentGateway)
    {
        var currencySymbol = currency.ToUpper() switch
        {
            "INR" => "\u20B9",
            "EUR" => "\u20AC",
            "GBP" => "\u00A3",
            _ => "$"
        };

        var billingCycleDisplay = billingCycle.ToLower() switch
        {
            "yearly" or "annual" => "Annual",
            _ => "Monthly"
        };

        var content = $@"
            <h2 style='margin: 0 0 16px 0; color: #1e293b; font-size: 24px;'>Payment Receipt</h2>
            <div style='background-color: #dcfce7; border-left: 4px solid #22c55e; padding: 16px 20px; margin-bottom: 24px; border-radius: 0 8px 8px 0;'>
                <p style='margin: 0; color: #166534; font-size: 16px; font-weight: 600;'>
                    Thank you for your payment!
                </p>
            </div>
            <p style='margin: 0 0 24px 0; color: #475569; font-size: 16px; line-height: 1.6;'>
                Hi {username}, here's your payment receipt for <strong>{siteName}</strong>.
            </p>

            <!-- Invoice Box -->
            <div style='background-color: #f8fafc; border: 1px solid #e2e8f0; border-radius: 12px; padding: 24px; margin-bottom: 24px;'>
                <div style='display: flex; justify-content: space-between; margin-bottom: 20px;'>
                    <div>
                        <p style='margin: 0 0 4px 0; color: #64748b; font-size: 12px; text-transform: uppercase;'>Invoice Number</p>
                        <p style='margin: 0; color: #1e293b; font-size: 16px; font-weight: 600;'>{invoiceNumber}</p>
                    </div>
                    <div style='text-align: right;'>
                        <p style='margin: 0 0 4px 0; color: #64748b; font-size: 12px; text-transform: uppercase;'>Date</p>
                        <p style='margin: 0; color: #1e293b; font-size: 16px; font-weight: 600;'>{DateTime.UtcNow:MMMM d, yyyy}</p>
                    </div>
                </div>

                <hr style='border: none; border-top: 1px solid #e2e8f0; margin: 20px 0;'>

                <table style='width: 100%; border-collapse: collapse;'>
                    <tr>
                        <td style='padding: 12px 0; color: #64748b; font-size: 14px;'>Plan</td>
                        <td style='padding: 12px 0; color: #1e293b; text-align: right; font-weight: 600;'>{planName} ({billingCycleDisplay})</td>
                    </tr>
                    <tr>
                        <td style='padding: 12px 0; color: #64748b; font-size: 14px;'>Site</td>
                        <td style='padding: 12px 0; color: #1e293b; text-align: right; font-weight: 600;'>{siteName}</td>
                    </tr>
                    <tr>
                        <td style='padding: 12px 0; color: #64748b; font-size: 14px;'>Billing Period</td>
                        <td style='padding: 12px 0; color: #1e293b; text-align: right; font-weight: 600;'>{periodStart:MMM d, yyyy} - {periodEnd:MMM d, yyyy}</td>
                    </tr>
                    <tr>
                        <td style='padding: 12px 0; color: #64748b; font-size: 14px;'>Payment Method</td>
                        <td style='padding: 12px 0; color: #1e293b; text-align: right; font-weight: 600;'>{paymentGateway}</td>
                    </tr>
                    <tr>
                        <td style='padding: 12px 0; color: #64748b; font-size: 14px;'>Transaction ID</td>
                        <td style='padding: 12px 0; color: #1e293b; text-align: right; font-size: 12px; font-family: monospace;'>{transactionId}</td>
                    </tr>
                </table>

                <hr style='border: none; border-top: 1px solid #e2e8f0; margin: 20px 0;'>

                <table style='width: 100%; border-collapse: collapse;'>
                    <tr>
                        <td style='padding: 12px 0; color: #1e293b; font-size: 18px; font-weight: 700;'>Total Paid</td>
                        <td style='padding: 12px 0; color: #22c55e; text-align: right; font-size: 24px; font-weight: 700;'>{currencySymbol}{amount:F2}</td>
                    </tr>
                </table>
            </div>

            <p style='margin: 0 0 24px 0; color: #64748b; font-size: 14px; line-height: 1.6;'>
                This receipt serves as confirmation of your payment. Please keep this for your records.
            </p>

            <a href='{_frontendUrl}/site-admin-overview.html' style='display: inline-block; background: linear-gradient(135deg, #0ea5e9, #2563eb); color: #ffffff; text-decoration: none; padding: 14px 32px; border-radius: 8px; font-weight: 600; font-size: 16px;'>Go to Dashboard</a>

            <p style='margin: 24px 0 0 0; color: #94a3b8; font-size: 12px; line-height: 1.6;'>
                If you have any questions about this payment, please contact our support team.
            </p>";

        return GetBaseTemplate(content);
    }
}
