using Microsoft.EntityFrameworkCore;
using ChatApp.API.Data;
using ChatApp.API.Services.Interfaces;

namespace ChatApp.API.Services.Implementations;

public class TrialExpirationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TrialExpirationBackgroundService> _logger;
    private readonly IConfiguration _configuration;

    public TrialExpirationBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<TrialExpirationBackgroundService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Subscription Expiration Background Service starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckTrialExpirationsAsync(stoppingToken);
                await CheckSubscriptionExpirationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while checking expirations");
                try
                {
                    using var errorScope = _serviceProvider.CreateScope();
                    var errorLogService = errorScope.ServiceProvider.GetRequiredService<IErrorLogService>();
                    await errorLogService.LogErrorAsync(ex, null, "Error");
                }
                catch { /* error logging should never crash the app */ }
            }

            // Wait for the configured interval before checking again
            var checkIntervalHours = _configuration.GetValue<int>("TrialSettings:CheckIntervalHours", 24);
            await Task.Delay(TimeSpan.FromHours(checkIntervalHours), stoppingToken);
        }

        _logger.LogInformation("Subscription Expiration Background Service stopping");
    }

    private async Task CheckTrialExpirationsAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Checking for trial expirations...");

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var daysToNotify = _configuration.GetSection("TrialSettings:DaysBeforeExpirationToNotify")
            .Get<int[]>() ?? new[] { 7, 3, 1 };

        var now = DateTime.UtcNow;

        // Get all trialing subscriptions
        var trialSubscriptions = await dbContext.Subscriptions
            .Include(s => s.Site)
                .ThenInclude(site => site.OwnerUser)
            .Include(s => s.Plan)
            .Where(s => s.Status == "trialing" && s.TrialEnd != null)
            .ToListAsync(stoppingToken);

        _logger.LogInformation("Found {Count} active trial subscriptions", trialSubscriptions.Count);

        foreach (var subscription in trialSubscriptions)
        {
            if (stoppingToken.IsCancellationRequested) break;

            var trialEnd = subscription.TrialEnd!.Value;
            var daysRemaining = (int)Math.Ceiling((trialEnd - now).TotalDays);

            // Check if trial has already expired
            if (daysRemaining <= 0)
            {
                await HandleExpiredTrialAsync(subscription, emailService, dbContext);
                continue;
            }

            // Check if we should send a warning notification
            if (daysToNotify.Contains(daysRemaining))
            {
                await SendTrialWarningAsync(subscription, daysRemaining, emailService, dbContext);
            }
        }

        await dbContext.SaveChangesAsync(stoppingToken);
        _logger.LogInformation("Trial expiration check completed");
    }

    private async Task HandleExpiredTrialAsync(
        ChatApp.API.Models.Entities.Subscription subscription,
        IEmailService emailService,
        ApplicationDbContext dbContext)
    {
        var site = subscription.Site;
        var user = site.OwnerUser;

        if (user == null || string.IsNullOrEmpty(user.Email))
        {
            _logger.LogWarning("Cannot send trial expired email for site {SiteId} - no owner email found", site.Id);
            return;
        }

        // Check if we already sent an expired notification (check metadata or a tracking table)
        var notificationKey = $"trial_expired_{subscription.Id}";
        var alreadySent = await dbContext.Notifications
            .AnyAsync(n => n.UserId == user.Id && n.Type == notificationKey);

        if (alreadySent)
        {
            return;
        }

        try
        {
            // Send email
            await emailService.SendTrialExpiredEmailAsync(user.Email, user.Username, site.Name);

            // Update subscription status
            subscription.Status = "expired";

            // Create notification record to track that we sent this
            dbContext.Notifications.Add(new ChatApp.API.Models.Entities.Notification
            {
                UserId = user.Id,
                SiteId = site.Id,
                Type = notificationKey,
                Title = "Trial Expired",
                Message = $"Your trial for {site.Name} has expired.",
                IsRead = false
            });

            _logger.LogInformation("Sent trial expired notification for site {SiteName} to {Email}",
                site.Name, user.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send trial expired email to {Email}", user.Email);
            try
            {
                using var errorScope = _serviceProvider.CreateScope();
                var errorLogService = errorScope.ServiceProvider.GetRequiredService<IErrorLogService>();
                await errorLogService.LogErrorAsync(ex, null, "Warning");
            }
            catch { }
        }
    }

    private async Task SendTrialWarningAsync(
        ChatApp.API.Models.Entities.Subscription subscription,
        int daysRemaining,
        IEmailService emailService,
        ApplicationDbContext dbContext)
    {
        var site = subscription.Site;
        var user = site.OwnerUser;

        if (user == null || string.IsNullOrEmpty(user.Email))
        {
            _logger.LogWarning("Cannot send trial warning email for site {SiteId} - no owner email found", site.Id);
            return;
        }

        // Check if we already sent this specific warning (e.g., 7-day warning)
        var notificationKey = $"trial_warning_{subscription.Id}_{daysRemaining}days";
        var alreadySent = await dbContext.Notifications
            .AnyAsync(n => n.UserId == user.Id && n.Type == notificationKey);

        if (alreadySent)
        {
            return;
        }

        try
        {
            // Send email
            await emailService.SendTrialExpirationWarningAsync(user.Email, user.Username, site.Name, daysRemaining);

            // Create notification record to track that we sent this
            dbContext.Notifications.Add(new ChatApp.API.Models.Entities.Notification
            {
                UserId = user.Id,
                SiteId = site.Id,
                Type = notificationKey,
                Title = "Trial Expiring Soon",
                Message = $"Your trial for {site.Name} expires in {daysRemaining} day{(daysRemaining == 1 ? "" : "s")}.",
                IsRead = false
            });

            _logger.LogInformation("Sent {Days}-day trial warning for site {SiteName} to {Email}",
                daysRemaining, site.Name, user.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send trial warning email to {Email}", user.Email);
            try
            {
                using var errorScope = _serviceProvider.CreateScope();
                var errorLogService = errorScope.ServiceProvider.GetRequiredService<IErrorLogService>();
                await errorLogService.LogErrorAsync(ex, null, "Warning");
            }
            catch { }
        }
    }

    private async Task CheckSubscriptionExpirationsAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Checking for subscription expirations...");

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var daysToNotify = _configuration.GetSection("SubscriptionSettings:DaysBeforeExpirationToNotify")
            .Get<int[]>() ?? new[] { 7, 3, 1 };

        var now = DateTime.UtcNow;

        // Get all active or cancelled (pending end) paid subscriptions
        // Exclude: trialing (handled separately), expired, Free plan (MonthlyPrice = 0)
        var paidSubscriptions = await dbContext.Subscriptions
            .Include(s => s.Site)
                .ThenInclude(site => site.OwnerUser)
            .Include(s => s.Plan)
            .Where(s => (s.Status == "active" || s.Status == "canceled")
                        && s.Plan.MonthlyPrice > 0
                        && s.CancelAtPeriodEnd == true)
            .ToListAsync(stoppingToken);

        _logger.LogInformation("Found {Count} paid subscriptions pending cancellation", paidSubscriptions.Count);

        foreach (var subscription in paidSubscriptions)
        {
            if (stoppingToken.IsCancellationRequested) break;

            var periodEnd = subscription.CurrentPeriodEnd;
            var daysRemaining = (int)Math.Ceiling((periodEnd - now).TotalDays);

            // Check if subscription period has already ended
            if (daysRemaining <= 0)
            {
                await HandleExpiredSubscriptionAsync(subscription, emailService, dbContext);
                continue;
            }

            // Check if we should send a warning notification
            if (daysToNotify.Contains(daysRemaining))
            {
                await SendSubscriptionWarningAsync(subscription, daysRemaining, emailService, dbContext);
            }
        }

        await dbContext.SaveChangesAsync(stoppingToken);
        _logger.LogInformation("Subscription expiration check completed");
    }

    private async Task HandleExpiredSubscriptionAsync(
        ChatApp.API.Models.Entities.Subscription subscription,
        IEmailService emailService,
        ApplicationDbContext dbContext)
    {
        var site = subscription.Site;
        var user = site.OwnerUser;
        var planName = subscription.Plan.Name;

        if (user == null || string.IsNullOrEmpty(user.Email))
        {
            _logger.LogWarning("Cannot send subscription expired email for site {SiteId} - no owner email found", site.Id);
            return;
        }

        // Check if we already sent an expired notification
        var notificationKey = $"subscription_expired_{subscription.Id}";
        var alreadySent = await dbContext.Notifications
            .AnyAsync(n => n.UserId == user.Id && n.Type == notificationKey);

        if (alreadySent)
        {
            return;
        }

        try
        {
            // Send email
            await emailService.SendPlanExpiredEmailAsync(user.Email, user.Username, site.Name, planName);

            // Update subscription status
            subscription.Status = "expired";
            subscription.CancelAtPeriodEnd = false;

            // Create notification record to track that we sent this
            dbContext.Notifications.Add(new ChatApp.API.Models.Entities.Notification
            {
                UserId = user.Id,
                SiteId = site.Id,
                Type = notificationKey,
                Title = "Subscription Expired",
                Message = $"Your {planName} plan for {site.Name} has expired.",
                IsRead = false
            });

            _logger.LogInformation("Sent subscription expired notification for site {SiteName} ({PlanName}) to {Email}",
                site.Name, planName, user.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send subscription expired email to {Email}", user.Email);
            try
            {
                using var errorScope = _serviceProvider.CreateScope();
                var errorLogService = errorScope.ServiceProvider.GetRequiredService<IErrorLogService>();
                await errorLogService.LogErrorAsync(ex, null, "Warning");
            }
            catch { }
        }
    }

    private async Task SendSubscriptionWarningAsync(
        ChatApp.API.Models.Entities.Subscription subscription,
        int daysRemaining,
        IEmailService emailService,
        ApplicationDbContext dbContext)
    {
        var site = subscription.Site;
        var user = site.OwnerUser;
        var planName = subscription.Plan.Name;
        var isCancelled = subscription.CancelAtPeriodEnd;

        if (user == null || string.IsNullOrEmpty(user.Email))
        {
            _logger.LogWarning("Cannot send subscription warning email for site {SiteId} - no owner email found", site.Id);
            return;
        }

        // Check if we already sent this specific warning
        var notificationKey = $"subscription_warning_{subscription.Id}_{daysRemaining}days";
        var alreadySent = await dbContext.Notifications
            .AnyAsync(n => n.UserId == user.Id && n.Type == notificationKey);

        if (alreadySent)
        {
            return;
        }

        try
        {
            // Send email
            await emailService.SendPlanExpirationWarningAsync(user.Email, user.Username, site.Name, planName, daysRemaining, isCancelled);

            // Create notification record to track that we sent this
            dbContext.Notifications.Add(new ChatApp.API.Models.Entities.Notification
            {
                UserId = user.Id,
                SiteId = site.Id,
                Type = notificationKey,
                Title = "Subscription Expiring Soon",
                Message = $"Your {planName} plan for {site.Name} expires in {daysRemaining} day{(daysRemaining == 1 ? "" : "s")}.",
                IsRead = false
            });

            _logger.LogInformation("Sent {Days}-day subscription warning for site {SiteName} ({PlanName}) to {Email}",
                daysRemaining, site.Name, planName, user.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send subscription warning email to {Email}", user.Email);
            try
            {
                using var errorScope = _serviceProvider.CreateScope();
                var errorLogService = errorScope.ServiceProvider.GetRequiredService<IErrorLogService>();
                await errorLogService.LogErrorAsync(ex, null, "Warning");
            }
            catch { }
        }
    }
}
