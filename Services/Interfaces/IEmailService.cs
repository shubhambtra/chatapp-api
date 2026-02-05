namespace ChatApp.API.Services.Interfaces;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string htmlBody, string? plainTextBody = null);
    Task SendEmailAsync(List<string> to, string subject, string htmlBody, string? plainTextBody = null);

    // Template-based emails
    Task SendWelcomeEmailAsync(string email, string username, string siteName);
    Task SendTrialExpirationWarningAsync(string email, string username, string siteName, int daysRemaining);
    Task SendTrialExpiredEmailAsync(string email, string username, string siteName);
    Task SendSubscriptionConfirmationAsync(string email, string username, string siteName, string planName, decimal amount);
    Task SendSubscriptionCancelledAsync(string email, string username, string siteName, DateTime endDate);
    Task SendPaymentFailedAsync(string email, string username, string siteName, string reason);
    Task SendPasswordResetAsync(string email, string username, string resetLink);
    Task SendAgentCredentialsEmailAsync(string email, string username, string password, string siteName);

    // Plan/Subscription expiration emails
    Task SendPlanExpirationWarningAsync(string email, string username, string siteName, string planName, int daysRemaining, bool isCancelled);
    Task SendPlanExpiredEmailAsync(string email, string username, string siteName, string planName);

    // Auto-pay emails
    Task SendAutoPaySuccessEmailAsync(string email, string username, string siteName, string planName, decimal amount, string currency, DateTime nextPeriodEnd);
    Task SendAutoPayFailedEmailAsync(string email, string username, string siteName, string planName, string reason, DateTime periodEnd);

    // Payment receipt/invoice email
    Task SendPaymentReceiptEmailAsync(string email, string username, string siteName, string planName,
        string invoiceNumber, string transactionId, decimal amount, string currency,
        string billingCycle, DateTime periodStart, DateTime periodEnd, string paymentGateway);
}
