using ChatApp.API.Models.DTOs;

namespace ChatApp.API.Services.Interfaces;

public interface ISubscriptionService
{
    // Plans
    Task<List<SubscriptionPlanDto>> GetPlansAsync(bool includeInactive = false);
    Task<SubscriptionPlanDto?> GetPlanAsync(string planId);

    // Admin Plan Management
    Task<List<PlanDetailDto>> GetAllPlansAsync();
    Task<PlanDetailDto?> GetPlanDetailAsync(string planId);
    Task<PlanDetailDto> CreatePlanAsync(CreatePlanRequest request);
    Task<PlanDetailDto> UpdatePlanAsync(string planId, UpdatePlanRequest request);
    Task DeletePlanAsync(string planId);

    // Subscriptions
    Task<SubscriptionDto> CreateSubscriptionAsync(string siteId, CreateSubscriptionRequest request);
    Task<SubscriptionDto?> GetSubscriptionAsync(string siteId);
    Task<SubscriptionDto> UpdateSubscriptionAsync(string siteId, UpdateSubscriptionRequest request);
    Task<SubscriptionDto> CancelSubscriptionAsync(string siteId, CancelSubscriptionRequest request);
    Task<SubscriptionDto> ReactivateSubscriptionAsync(string siteId, ReactivateSubscriptionRequest request);
    Task<SubscriptionDto> UpgradeSubscriptionAsync(string siteId, UpgradeSubscriptionRequest request);

    // Usage
    Task<List<SubscriptionUsageDto>> GetUsageAsync(string siteId);
    Task RecordUsageAsync(string siteId, string metricName, int quantity);

    // History
    Task<List<SubscriptionHistoryDto>> GetHistoryAsync(string siteId);

    // Limit Checking
    Task<string> GetOrCreateFreePlanIdAsync();
    Task AssignFreePlanToSiteAsync(string siteId);
    Task<(bool allowed, string? reason, int? limit, int? current)> CheckLimitAsync(string siteId, string metricName);
    Task<SubscriptionPlanDto?> GetSitePlanAsync(string siteId);

    // Auto-pay
    Task<AutoPaySettingsDto> GetAutoPaySettingsAsync(string siteId);
    Task<AutoPaySettingsDto> SetAutoPayAsync(string siteId, SetAutoPayRequest request);
    Task<List<ChatApp.API.Models.Entities.Subscription>> GetSubscriptionsForAutoPayAsync();
    Task RenewSubscriptionAsync(string subscriptionId, DateTime newPeriodEnd);
}
