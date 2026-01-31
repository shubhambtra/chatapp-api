using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChatApp.API.Data;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Services.Interfaces;

namespace ChatApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;

    public SubscriptionsController(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    [HttpGet("plans")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<SubscriptionPlanDto>>>> GetPlans()
    {
        var plans = await _subscriptionService.GetPlansAsync();
        return Ok(ApiResponse<List<SubscriptionPlanDto>>.Ok(plans));
    }

    [HttpGet("plans/{planId}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<SubscriptionPlanDto>>> GetPlan(string planId)
    {
        var plan = await _subscriptionService.GetPlanAsync(planId);
        if (plan == null)
        {
            return NotFound(ApiResponse<SubscriptionPlanDto>.Fail("Plan not found"));
        }

        return Ok(ApiResponse<SubscriptionPlanDto>.Ok(plan));
    }

    [HttpGet("sites/{siteId}")]
    public async Task<ActionResult<ApiResponse<SubscriptionDto>>> GetSubscription(string siteId)
    {
        var subscription = await _subscriptionService.GetSubscriptionAsync(siteId);
        if (subscription == null)
        {
            return NotFound(ApiResponse<SubscriptionDto>.Fail("No active subscription"));
        }

        return Ok(ApiResponse<SubscriptionDto>.Ok(subscription));
    }

    [HttpPost("sites/{siteId}")]
    public async Task<ActionResult<ApiResponse<SubscriptionDto>>> CreateSubscription(
        string siteId,
        [FromBody] CreateSubscriptionRequest request)
    {
        try
        {
            var subscription = await _subscriptionService.CreateSubscriptionAsync(siteId, request);
            return Ok(ApiResponse<SubscriptionDto>.Ok(subscription, "Subscription created"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<SubscriptionDto>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SubscriptionDto>.Fail(ex.Message));
        }
    }

    [HttpPut("sites/{siteId}")]
    public async Task<ActionResult<ApiResponse<SubscriptionDto>>> UpdateSubscription(
        string siteId,
        [FromBody] UpdateSubscriptionRequest request)
    {
        try
        {
            var subscription = await _subscriptionService.UpdateSubscriptionAsync(siteId, request);
            return Ok(ApiResponse<SubscriptionDto>.Ok(subscription));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<SubscriptionDto>.Fail(ex.Message));
        }
    }

    [HttpPost("sites/{siteId}/cancel")]
    public async Task<ActionResult<ApiResponse<SubscriptionDto>>> CancelSubscription(
        string siteId,
        [FromBody] CancelSubscriptionRequest request)
    {
        try
        {
            var subscription = await _subscriptionService.CancelSubscriptionAsync(siteId, request);
            return Ok(ApiResponse<SubscriptionDto>.Ok(subscription, "Subscription canceled"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<SubscriptionDto>.Fail(ex.Message));
        }
    }

    [HttpPost("sites/{siteId}/reactivate")]
    public async Task<ActionResult<ApiResponse<SubscriptionDto>>> ReactivateSubscription(
        string siteId,
        [FromBody] ReactivateSubscriptionRequest request)
    {
        try
        {
            var subscription = await _subscriptionService.ReactivateSubscriptionAsync(siteId, request);
            return Ok(ApiResponse<SubscriptionDto>.Ok(subscription, "Subscription reactivated"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<SubscriptionDto>.Fail(ex.Message));
        }
    }

    [HttpGet("sites/{siteId}/usage")]
    public async Task<ActionResult<ApiResponse<List<SubscriptionUsageDto>>>> GetUsage(string siteId)
    {
        var usage = await _subscriptionService.GetUsageAsync(siteId);
        return Ok(ApiResponse<List<SubscriptionUsageDto>>.Ok(usage));
    }

    [HttpPost("sites/{siteId}/upgrade")]
    public async Task<ActionResult<ApiResponse<SubscriptionDto>>> UpgradeSubscription(
        string siteId,
        [FromBody] UpgradeSubscriptionRequest request)
    {
        try
        {
            var subscription = await _subscriptionService.UpgradeSubscriptionAsync(siteId, request);
            return Ok(ApiResponse<SubscriptionDto>.Ok(subscription, "Subscription upgraded successfully"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<SubscriptionDto>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SubscriptionDto>.Fail(ex.Message));
        }
    }

    [HttpGet("sites/{siteId}/history")]
    public async Task<ActionResult<ApiResponse<List<SubscriptionHistoryDto>>>> GetHistory(string siteId)
    {
        var history = await _subscriptionService.GetHistoryAsync(siteId);
        return Ok(ApiResponse<List<SubscriptionHistoryDto>>.Ok(history));
    }

    /// <summary>
    /// Get auto-pay settings for a site
    /// </summary>
    [HttpGet("sites/{siteId}/auto-pay")]
    public async Task<ActionResult<ApiResponse<AutoPaySettingsDto>>> GetAutoPaySettings(string siteId)
    {
        try
        {
            var settings = await _subscriptionService.GetAutoPaySettingsAsync(siteId);
            return Ok(ApiResponse<AutoPaySettingsDto>.Ok(settings));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<AutoPaySettingsDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Enable or disable auto-pay for a site
    /// </summary>
    [HttpPost("sites/{siteId}/auto-pay")]
    public async Task<ActionResult<ApiResponse<AutoPaySettingsDto>>> SetAutoPay(
        string siteId,
        [FromBody] SetAutoPayRequest request)
    {
        try
        {
            var settings = await _subscriptionService.SetAutoPayAsync(siteId, request);
            var message = request.Enabled ? "Auto-pay enabled successfully" : "Auto-pay disabled";
            return Ok(ApiResponse<AutoPaySettingsDto>.Ok(settings, message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<AutoPaySettingsDto>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<AutoPaySettingsDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Get combined plan and usage overview for site-admin dashboard
    /// </summary>
    [HttpGet("sites/{siteId}/overview")]
    public async Task<ActionResult<ApiResponse<SitePlanOverviewDto>>> GetPlanOverview(string siteId)
    {
        var plan = await _subscriptionService.GetSitePlanAsync(siteId);
        var subscription = await _subscriptionService.GetSubscriptionAsync(siteId);
        var usage = await _subscriptionService.GetUsageAsync(siteId);

        var overview = new SitePlanOverviewDto(
            plan,
            subscription,
            usage,
            subscription != null && subscription.Status == "active"
        );

        return Ok(ApiResponse<SitePlanOverviewDto>.Ok(overview));
    }

    /// <summary>
    /// Check and record AI usage (analysis or auto-reply)
    /// POST /api/subscriptions/sites/{siteId}/ai-usage
    /// </summary>
    [HttpPost("sites/{siteId}/ai-usage")]
    [AllowAnonymous] // Called from Python server
    public async Task<ActionResult<ApiResponse<AiUsageResponse>>> RecordAiUsage(string siteId, [FromBody] AiUsageRequest request)
    {
        try
        {
            // Map feature type to metric name
            var metricName = request.FeatureType switch
            {
                "analysis" => "ai_analyses",
                "auto_reply" => "ai_auto_replies",
                _ => throw new ArgumentException($"Invalid feature type: {request.FeatureType}")
            };

            // Check if limit allows usage
            var (allowed, message, limit, used) = await _subscriptionService.CheckLimitAsync(siteId, metricName);

            if (!allowed)
            {
                return Ok(ApiResponse<AiUsageResponse>.Ok(new AiUsageResponse(
                    Allowed: false,
                    Message: message,
                    Used: used,
                    Limit: limit,
                    FeatureType: request.FeatureType
                )));
            }

            // Record the usage
            await _subscriptionService.RecordUsageAsync(siteId, metricName, 1);

            return Ok(ApiResponse<AiUsageResponse>.Ok(new AiUsageResponse(
                Allowed: true,
                Message: null,
                Used: (used ?? 0) + 1,
                Limit: limit,
                FeatureType: request.FeatureType
            )));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<AiUsageResponse>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Get current AI usage for a site
    /// GET /api/subscriptions/sites/{siteId}/ai-usage
    /// </summary>
    [HttpGet("sites/{siteId}/ai-usage")]
    [AllowAnonymous] // Called from Python server and frontend
    public async Task<ActionResult<ApiResponse<AiUsageSummary>>> GetAiUsage(string siteId)
    {
        try
        {
            var (analysisAllowed, analysisMsg, analysisLimit, analysisUsed) =
                await _subscriptionService.CheckLimitAsync(siteId, "ai_analyses");
            var (autoReplyAllowed, autoReplyMsg, autoReplyLimit, autoReplyUsed) =
                await _subscriptionService.CheckLimitAsync(siteId, "ai_auto_replies");

            return Ok(ApiResponse<AiUsageSummary>.Ok(new AiUsageSummary(
                AiAnalysis: new AiFeatureUsage(
                    Enabled: analysisAllowed || analysisMsg?.Contains("limit") == true,
                    Used: analysisUsed,
                    Limit: analysisLimit,
                    LimitReached: !analysisAllowed && analysisMsg?.Contains("limit") == true
                ),
                AiAutoReply: new AiFeatureUsage(
                    Enabled: autoReplyAllowed || autoReplyMsg?.Contains("limit") == true,
                    Used: autoReplyUsed,
                    Limit: autoReplyLimit,
                    LimitReached: !autoReplyAllowed && autoReplyMsg?.Contains("limit") == true
                )
            )));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<AiUsageSummary>.Fail(ex.Message));
        }
    }
}

/// <summary>
/// Admin endpoints for managing subscription plans (super_admin only)
/// </summary>
[ApiController]
[Route("api/admin/plans")]
[Authorize(Roles = "super_admin")]
public class AdminPlansController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;

    public AdminPlansController(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    /// <summary>
    /// Get all plans with full details including inactive ones
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<PlanDetailDto>>>> GetAllPlans()
    {
        var plans = await _subscriptionService.GetAllPlansAsync();
        return Ok(ApiResponse<List<PlanDetailDto>>.Ok(plans));
    }

    /// <summary>
    /// Get plan details by ID
    /// </summary>
    [HttpGet("{planId}")]
    public async Task<ActionResult<ApiResponse<PlanDetailDto>>> GetPlan(string planId)
    {
        var plan = await _subscriptionService.GetPlanDetailAsync(planId);
        if (plan == null)
        {
            return NotFound(ApiResponse<PlanDetailDto>.Fail("Plan not found"));
        }

        return Ok(ApiResponse<PlanDetailDto>.Ok(plan));
    }

    /// <summary>
    /// Create a new subscription plan
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<PlanDetailDto>>> CreatePlan([FromBody] CreatePlanRequest request)
    {
        var plan = await _subscriptionService.CreatePlanAsync(request);
        return Ok(ApiResponse<PlanDetailDto>.Ok(plan, "Plan created successfully"));
    }

    /// <summary>
    /// Update an existing plan
    /// </summary>
    [HttpPut("{planId}")]
    public async Task<ActionResult<ApiResponse<PlanDetailDto>>> UpdatePlan(
        string planId,
        [FromBody] UpdatePlanRequest request)
    {
        try
        {
            var plan = await _subscriptionService.UpdatePlanAsync(planId, request);
            return Ok(ApiResponse<PlanDetailDto>.Ok(plan, "Plan updated successfully"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<PlanDetailDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Delete a plan (only if no active subscribers)
    /// </summary>
    [HttpDelete("{planId}")]
    public async Task<ActionResult<ApiResponse<object>>> DeletePlan(string planId)
    {
        try
        {
            await _subscriptionService.DeletePlanAsync(planId);
            return Ok(ApiResponse<object>.Ok(null, "Plan deleted successfully"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}

/// <summary>
/// Admin endpoints for managing all subscriptions (super_admin only)
/// </summary>
[ApiController]
[Route("api/admin/subscriptions")]
[Authorize(Roles = "super_admin")]
public class AdminSubscriptionsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AdminSubscriptionsController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get all subscriptions with site info
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<AdminSubscriptionDto>>>> GetAllSubscriptions()
    {
        var subscriptions = await _context.Subscriptions
            .Include(s => s.Site)
            .Include(s => s.Plan)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new AdminSubscriptionDto(
                s.Id,
                s.SiteId,
                s.Site.Name,
                s.Site.Domain,
                s.PlanId,
                s.Plan.Name,
                s.Status,
                s.BillingCycle,
                s.CurrentPeriodStart,
                s.CurrentPeriodEnd,
                s.TrialStart,
                s.TrialEnd,
                s.CanceledAt,
                s.CreatedAt
            ))
            .ToListAsync();

        return Ok(ApiResponse<List<AdminSubscriptionDto>>.Ok(subscriptions));
    }

    /// <summary>
    /// Extend subscription period
    /// </summary>
    [HttpPost("{subscriptionId}/extend")]
    public async Task<ActionResult<ApiResponse<AdminSubscriptionDto>>> ExtendSubscription(
        string subscriptionId,
        [FromBody] ExtendSubscriptionRequest request)
    {
        var subscription = await _context.Subscriptions
            .Include(s => s.Site)
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId);

        if (subscription == null)
        {
            return NotFound(ApiResponse<AdminSubscriptionDto>.Fail("Subscription not found"));
        }

        // Extend the period
        subscription.CurrentPeriodEnd = subscription.CurrentPeriodEnd.AddDays(request.Days);
        subscription.UpdatedAt = DateTime.UtcNow;

        // If subscription was canceled, reactivate it
        if (subscription.Status == "canceled" || subscription.Status == "past_due")
        {
            subscription.Status = "active";
            subscription.CanceledAt = null;
            subscription.CancelAt = null;
            subscription.CancelAtPeriodEnd = false;
        }

        await _context.SaveChangesAsync();

        var dto = new AdminSubscriptionDto(
            subscription.Id,
            subscription.SiteId,
            subscription.Site.Name,
            subscription.Site.Domain,
            subscription.PlanId,
            subscription.Plan.Name,
            subscription.Status,
            subscription.BillingCycle,
            subscription.CurrentPeriodStart,
            subscription.CurrentPeriodEnd,
            subscription.TrialStart,
            subscription.TrialEnd,
            subscription.CanceledAt,
            subscription.CreatedAt
        );

        return Ok(ApiResponse<AdminSubscriptionDto>.Ok(dto, $"Subscription extended by {request.Days} days"));
    }
}

// DTOs for Admin Subscriptions
public record AdminSubscriptionDto(
    string Id,
    string SiteId,
    string SiteName,
    string SiteDomain,
    string PlanId,
    string PlanName,
    string Status,
    string BillingCycle,
    DateTime CurrentPeriodStart,
    DateTime CurrentPeriodEnd,
    DateTime? TrialStart,
    DateTime? TrialEnd,
    DateTime? CanceledAt,
    DateTime CreatedAt
);

public record ExtendSubscriptionRequest(int Days);

// DTOs for AI usage
public record AiUsageRequest(string FeatureType);
public record AiUsageResponse(bool Allowed, string? Message, int? Used, int? Limit, string FeatureType);
public record AiFeatureUsage(bool Enabled, int? Used, int? Limit, bool LimitReached);
public record AiUsageSummary(AiFeatureUsage AiAnalysis, AiFeatureUsage AiAutoReply);
