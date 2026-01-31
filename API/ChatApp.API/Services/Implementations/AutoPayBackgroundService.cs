using Microsoft.EntityFrameworkCore;
using ChatApp.API.Data;
using ChatApp.API.Models.Entities;
using ChatApp.API.Services.Interfaces;

namespace ChatApp.API.Services.Implementations;

public class AutoPayBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AutoPayBackgroundService> _logger;
    private readonly IConfiguration _configuration;

    public AutoPayBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<AutoPayBackgroundService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Auto-Pay Background Service starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAutoPaymentsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing auto-payments");
            }

            // Check every 6 hours (configurable)
            var checkIntervalHours = _configuration.GetValue<int>("AutoPaySettings:CheckIntervalHours", 6);
            await Task.Delay(TimeSpan.FromHours(checkIntervalHours), stoppingToken);
        }

        _logger.LogInformation("Auto-Pay Background Service stopping");
    }

    private async Task ProcessAutoPaymentsAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Processing auto-payments...");

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
        var subscriptionService = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var paymentLogService = scope.ServiceProvider.GetRequiredService<IPaymentLogService>();

        var now = DateTime.UtcNow;
        var checkWindow = now.AddHours(24); // Process payments for subscriptions expiring in next 24 hours

        // Get subscriptions with auto-pay enabled that are about to expire
        var subscriptionsToCharge = await dbContext.Subscriptions
            .Include(s => s.Site)
                .ThenInclude(site => site.OwnerUser)
            .Include(s => s.Plan)
            .Where(s => s.Status == "active"
                        && s.AutoPayEnabled
                        && !string.IsNullOrEmpty(s.DefaultPaymentMethodId)
                        && s.CurrentPeriodEnd <= checkWindow
                        && s.CurrentPeriodEnd > now
                        && !s.CancelAtPeriodEnd
                        && s.Plan.MonthlyPrice > 0)
            .ToListAsync(stoppingToken);

        _logger.LogInformation("Found {Count} subscriptions for auto-pay processing", subscriptionsToCharge.Count);

        foreach (var subscription in subscriptionsToCharge)
        {
            if (stoppingToken.IsCancellationRequested) break;

            // Check if we already processed this subscription recently
            var notificationKey = $"autopay_processed_{subscription.Id}_{subscription.CurrentPeriodEnd:yyyyMMdd}";
            var alreadyProcessed = await dbContext.Notifications
                .AnyAsync(n => n.Type == notificationKey, stoppingToken);

            if (alreadyProcessed)
            {
                _logger.LogDebug("Subscription {SubscriptionId} already processed for current period", subscription.Id);
                continue;
            }

            await ProcessSubscriptionAutoPayAsync(
                subscription,
                dbContext,
                paymentService,
                emailService,
                paymentLogService,
                notificationKey,
                stoppingToken);
        }

        await dbContext.SaveChangesAsync(stoppingToken);
        _logger.LogInformation("Auto-pay processing completed");
    }

    private async Task ProcessSubscriptionAutoPayAsync(
        Subscription subscription,
        ApplicationDbContext dbContext,
        IPaymentService paymentService,
        IEmailService emailService,
        IPaymentLogService paymentLogService,
        string notificationKey,
        CancellationToken stoppingToken)
    {
        var site = subscription.Site;
        var user = site?.OwnerUser;
        var plan = subscription.Plan;

        _logger.LogInformation("Processing auto-pay for subscription {SubscriptionId}, Site: {SiteName}, Plan: {PlanName}",
            subscription.Id, site?.Name, plan?.Name);

        // Calculate amount early for logging
        var amount = subscription.BillingCycle == "annual"
            ? plan.AnnualPrice ?? plan.MonthlyPrice
            : plan.MonthlyPrice;

        // Get payment method
        var paymentMethod = await dbContext.PaymentMethods
            .FirstOrDefaultAsync(pm => pm.Id == subscription.DefaultPaymentMethodId, stoppingToken);

        var gateway = subscription.PreferredPaymentGateway ?? paymentMethod?.Gateway ?? "razorpay";

        // Create payment log entry
        var paymentLog = await paymentLogService.LogAsync(new PaymentLogEntry
        {
            SiteId = subscription.SiteId,
            SubscriptionId = subscription.Id,
            PaymentMethodId = subscription.DefaultPaymentMethodId,
            Action = "auto_pay_attempt",
            Gateway = gateway,
            Amount = amount,
            Currency = plan.Currency,
            UserId = user?.Id,
            Metadata = new
            {
                PlanId = plan.Id,
                PlanName = plan.Name,
                BillingCycle = subscription.BillingCycle,
                PeriodEnd = subscription.CurrentPeriodEnd
            }
        });

        try
        {
            if (paymentMethod == null)
            {
                _logger.LogWarning("Payment method not found for subscription {SubscriptionId}", subscription.Id);
                await paymentLogService.LogFailureAsync(paymentLog, "Payment method not found", "PAYMENT_METHOD_NOT_FOUND");
                await HandleAutoPayFailureAsync(subscription, "Payment method not found", dbContext, emailService, notificationKey, user);
                return;
            }

            // Process payment based on gateway
            bool paymentSuccess;
            string? transactionId = null;
            string? failureReason = null;

            if (gateway == "razorpay")
            {
                (paymentSuccess, transactionId, failureReason) = await ProcessRazorpayAutoPayAsync(
                    paymentMethod, subscription, amount, dbContext, paymentLogService);
            }
            else if (gateway == "paypal")
            {
                (paymentSuccess, transactionId, failureReason) = await ProcessPayPalAutoPayAsync(
                    paymentMethod, subscription, amount, dbContext, paymentLogService);
            }
            else
            {
                _logger.LogWarning("Unknown payment gateway: {Gateway}", gateway);
                paymentSuccess = false;
                failureReason = $"Unknown payment gateway: {gateway}";
            }

            if (paymentSuccess)
            {
                await paymentLogService.LogSuccessAsync(paymentLog, null, transactionId);
                await HandleAutoPaySuccessAsync(subscription, transactionId, amount, dbContext, emailService, notificationKey, user);
            }
            else
            {
                await paymentLogService.LogFailureAsync(paymentLog, failureReason ?? "Payment failed", "PAYMENT_FAILED");
                await HandleAutoPayFailureAsync(subscription, failureReason ?? "Payment failed", dbContext, emailService, notificationKey, user);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing auto-pay for subscription {SubscriptionId}", subscription.Id);
            await paymentLogService.LogFailureAsync(paymentLog, ex.Message, "EXCEPTION", ex.StackTrace);
            await HandleAutoPayFailureAsync(subscription, ex.Message, dbContext, emailService, notificationKey, user);
        }
    }

    private async Task<(bool success, string? transactionId, string? error)> ProcessRazorpayAutoPayAsync(
        PaymentMethod paymentMethod,
        Subscription subscription,
        decimal amount,
        ApplicationDbContext dbContext,
        IPaymentLogService paymentLogService)
    {
        // For Razorpay recurring payments, we need a token (from emandate or card tokenization)
        if (string.IsNullOrEmpty(paymentMethod.RazorpayTokenId) || string.IsNullOrEmpty(paymentMethod.RazorpayCustomerId))
        {
            _logger.LogWarning("Razorpay token not available for payment method {PaymentMethodId}", paymentMethod.Id);
            return (false, null, "Razorpay recurring payment token not configured. Please re-add your payment method.");
        }

        try
        {
            // In production, you would call Razorpay's recurring payment API here
            // POST https://api.razorpay.com/v1/payments/create/recurring
            // For now, we'll simulate a successful payment

            _logger.LogInformation("Processing Razorpay auto-payment for {Amount} {Currency}",
                amount, subscription.Plan.Currency);

            // Simulate payment success (replace with actual Razorpay API call)
            var transactionId = $"rpay_{Guid.NewGuid():N}";

            // Record the payment
            var invoice = await CreateInvoiceAsync(subscription, amount, dbContext);
            var payment = await CreatePaymentAsync(subscription, invoice, paymentMethod, amount, transactionId, dbContext);

            return (true, transactionId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Razorpay auto-payment failed");
            return (false, null, ex.Message);
        }
    }

    private async Task<(bool success, string? transactionId, string? error)> ProcessPayPalAutoPayAsync(
        PaymentMethod paymentMethod,
        Subscription subscription,
        decimal amount,
        ApplicationDbContext dbContext,
        IPaymentLogService paymentLogService)
    {
        // For PayPal recurring payments, we need a billing agreement ID
        if (string.IsNullOrEmpty(paymentMethod.PayPalBillingAgreementId))
        {
            _logger.LogWarning("PayPal billing agreement not available for payment method {PaymentMethodId}", paymentMethod.Id);
            return (false, null, "PayPal billing agreement not configured. Please re-add your payment method.");
        }

        try
        {
            // In production, you would call PayPal's billing agreement API here
            // For reference transactions or billing agreements
            // For now, we'll simulate a successful payment

            _logger.LogInformation("Processing PayPal auto-payment for {Amount} {Currency}",
                amount, subscription.Plan.Currency);

            // Simulate payment success (replace with actual PayPal API call)
            var transactionId = $"pp_{Guid.NewGuid():N}";

            // Record the payment
            var invoice = await CreateInvoiceAsync(subscription, amount, dbContext);
            var payment = await CreatePaymentAsync(subscription, invoice, paymentMethod, amount, transactionId, dbContext);

            return (true, transactionId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayPal auto-payment failed");
            return (false, null, ex.Message);
        }
    }

    private async Task<Invoice> CreateInvoiceAsync(Subscription subscription, decimal amount, ApplicationDbContext dbContext)
    {
        var billingCycle = subscription.BillingCycle;
        var newPeriodEnd = billingCycle == "annual"
            ? subscription.CurrentPeriodEnd.AddYears(1)
            : subscription.CurrentPeriodEnd.AddMonths(1);

        var invoice = new Invoice
        {
            SiteId = subscription.SiteId,
            SubscriptionId = subscription.Id,
            InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}",
            Status = "paid",
            Subtotal = amount,
            Tax = 0,
            Discount = 0,
            Total = amount,
            AmountPaid = amount,
            AmountDue = 0,
            Currency = subscription.Plan.Currency,
            DueDate = DateTime.UtcNow,
            PaidAt = DateTime.UtcNow,
            PeriodStart = subscription.CurrentPeriodEnd,
            PeriodEnd = newPeriodEnd,
            Notes = "Auto-pay renewal"
        };

        invoice.Items.Add(new InvoiceItem
        {
            Description = $"{subscription.Plan.Name} Plan - {billingCycle} subscription (Auto-renewal)",
            Quantity = 1,
            UnitPrice = amount,
            Amount = amount
        });

        dbContext.Invoices.Add(invoice);
        return invoice;
    }

    private async Task<Payment> CreatePaymentAsync(
        Subscription subscription,
        Invoice invoice,
        PaymentMethod paymentMethod,
        decimal amount,
        string transactionId,
        ApplicationDbContext dbContext)
    {
        var payment = new Payment
        {
            SiteId = subscription.SiteId,
            InvoiceId = invoice.Id,
            PaymentMethodId = paymentMethod.Id,
            Amount = amount,
            Currency = subscription.Plan.Currency,
            Status = "succeeded",
            StripePaymentIntentId = transactionId // Using this field for transaction ID
        };

        dbContext.Payments.Add(payment);
        return payment;
    }

    private async Task HandleAutoPaySuccessAsync(
        Subscription subscription,
        string? transactionId,
        decimal amount,
        ApplicationDbContext dbContext,
        IEmailService emailService,
        string notificationKey,
        User? user)
    {
        var billingCycle = subscription.BillingCycle;
        var newPeriodEnd = billingCycle == "annual"
            ? subscription.CurrentPeriodEnd.AddYears(1)
            : subscription.CurrentPeriodEnd.AddMonths(1);

        // Update subscription period
        subscription.CurrentPeriodStart = subscription.CurrentPeriodEnd;
        subscription.CurrentPeriodEnd = newPeriodEnd;

        // Record history
        dbContext.SubscriptionHistories.Add(new SubscriptionHistory
        {
            SubscriptionId = subscription.Id,
            Action = "renewed",
            ToPlanId = subscription.PlanId,
            Reason = $"Auto-pay renewal. Transaction: {transactionId}"
        });

        // Create notification record to prevent duplicate processing
        dbContext.Notifications.Add(new Notification
        {
            UserId = user?.Id ?? subscription.Site.OwnerUserId,
            SiteId = subscription.SiteId,
            Type = notificationKey,
            Title = "Subscription Renewed",
            Message = $"Your {subscription.Plan.Name} subscription has been automatically renewed.",
            IsRead = false
        });

        _logger.LogInformation("Auto-pay successful for subscription {SubscriptionId}. New period end: {NewPeriodEnd}",
            subscription.Id, newPeriodEnd);

        // Send confirmation email
        if (user != null && !string.IsNullOrEmpty(user.Email))
        {
            try
            {
                await emailService.SendAutoPaySuccessEmailAsync(
                    user.Email,
                    user.Username,
                    subscription.Site.Name,
                    subscription.Plan.Name,
                    amount,
                    subscription.Plan.Currency,
                    newPeriodEnd);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send auto-pay success email to {Email}", user.Email);
            }
        }
    }

    private async Task HandleAutoPayFailureAsync(
        Subscription subscription,
        string reason,
        ApplicationDbContext dbContext,
        IEmailService emailService,
        string notificationKey,
        User? user)
    {
        // Disable auto-pay to prevent repeated failures
        subscription.AutoPayEnabled = false;

        // Create notification
        dbContext.Notifications.Add(new Notification
        {
            UserId = user?.Id ?? subscription.Site.OwnerUserId,
            SiteId = subscription.SiteId,
            Type = $"autopay_failed_{subscription.Id}_{DateTime.UtcNow:yyyyMMddHH}",
            Title = "Auto-pay Failed",
            Message = $"Auto-pay failed for your {subscription.Plan.Name} subscription: {reason}",
            IsRead = false
        });

        _logger.LogWarning("Auto-pay failed for subscription {SubscriptionId}. Reason: {Reason}",
            subscription.Id, reason);

        // Send failure email
        if (user != null && !string.IsNullOrEmpty(user.Email))
        {
            try
            {
                await emailService.SendAutoPayFailedEmailAsync(
                    user.Email,
                    user.Username,
                    subscription.Site.Name,
                    subscription.Plan.Name,
                    reason,
                    subscription.CurrentPeriodEnd);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send auto-pay failure email to {Email}", user.Email);
            }
        }
    }
}
