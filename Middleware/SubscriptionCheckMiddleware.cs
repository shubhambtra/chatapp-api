using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ChatApp.API.Data;
using ChatApp.API.Models.DTOs;

namespace ChatApp.API.Middleware;

public class SubscriptionCheckMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SubscriptionCheckMiddleware> _logger;

    // Paths that are exempt from subscription checks (users must be able to upgrade/pay even when expired)
    private static readonly string[] ExemptPrefixes =
    {
        "/api/auth",
        "/api/subscriptions",
        "/api/payments",
        "/api/admin",
        "/api/issue-reports",
        "/hubs",
        "/swagger",
        "/health"
    };

    // Site-scoped sub-paths that are exempt (billing/payment within a site)
    private static readonly string[] ExemptSiteSubPaths =
    {
        "/payments",
        "/billing"
    };

    public SubscriptionCheckMiddleware(RequestDelegate next, ILogger<SubscriptionCheckMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Only check site-scoped API requests: /api/sites/{siteId}/...
        if (!IsSiteScopedRequest(path, out var siteId))
        {
            await _next(context);
            return;
        }

        // Check if the sub-path after /api/sites/{siteId} is exempt
        if (IsSiteSubPathExempt(path, siteId))
        {
            await _next(context);
            return;
        }

        // Check if any global exempt prefix matches
        foreach (var prefix in ExemptPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }
        }

        // Now check subscription status for this site
        using var scope = context.RequestServices.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var subscription = await dbContext.Subscriptions
            .AsNoTracking()
            .Where(s => s.SiteId == siteId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        if (subscription == null)
        {
            // No subscription at all - let through (shouldn't happen normally)
            await _next(context);
            return;
        }

        var now = DateTime.UtcNow;
        string? errorCode = null;
        string? errorMessage = null;

        switch (subscription.Status)
        {
            case "expired":
                errorCode = subscription.TrialEnd != null ? "TRIAL_EXPIRED" : "SUBSCRIPTION_EXPIRED";
                errorMessage = subscription.TrialEnd != null
                    ? "Your trial has expired. Please choose a plan to continue using the service."
                    : "Your subscription has expired. Please renew or choose a plan to continue.";
                break;

            case "canceled":
                errorCode = "SUBSCRIPTION_EXPIRED";
                errorMessage = "Your subscription has been canceled. Please choose a plan to continue using the service.";
                break;

            case "trialing":
                // Check if trial has actually ended (background service may not have run yet)
                if (subscription.TrialEnd.HasValue && subscription.TrialEnd.Value <= now)
                {
                    errorCode = "TRIAL_EXPIRED";
                    errorMessage = "Your trial has expired. Please choose a plan to continue using the service.";
                }
                break;

            case "active":
                // Active subscription - allow through
                break;
        }

        if (errorCode != null)
        {
            _logger.LogInformation(
                "Blocking request to {Path} for site {SiteId} - subscription status: {Status}, error: {ErrorCode}",
                path, siteId, subscription.Status, errorCode);

            context.Response.StatusCode = 403;
            context.Response.ContentType = "application/json";

            var response = new ErrorResponse(
                errorCode,
                errorMessage,
                errorCode,
                null
            );

            var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(jsonResponse);
            return;
        }

        await _next(context);
    }

    private static bool IsSiteScopedRequest(string path, out string siteId)
    {
        siteId = "";

        // Match /api/sites/{siteId}/... pattern
        const string prefix = "/api/sites/";
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var rest = path[prefix.Length..];
        var slashIndex = rest.IndexOf('/');

        if (slashIndex <= 0)
            return false; // No sub-path after siteId, or empty siteId

        siteId = rest[..slashIndex];
        return !string.IsNullOrEmpty(siteId);
    }

    private static bool IsSiteSubPathExempt(string path, string siteId)
    {
        // Get the sub-path after /api/sites/{siteId}
        var sitePrefix = $"/api/sites/{siteId}";
        if (!path.StartsWith(sitePrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var subPath = path[sitePrefix.Length..];

        foreach (var exemptSubPath in ExemptSiteSubPaths)
        {
            if (subPath.StartsWith(exemptSubPath, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
