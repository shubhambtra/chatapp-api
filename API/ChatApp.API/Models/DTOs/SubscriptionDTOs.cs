namespace ChatApp.API.Models.DTOs;

public record SubscriptionPlanDto(
    string Id,
    string Name,
    string? Description,
    decimal MonthlyPrice,
    decimal? AnnualPrice,
    string Currency,
    decimal? MonthlyPriceInr,
    decimal? AnnualPriceInr,
    bool InrEnabled,
    int? MaxAgents,
    int? MaxConversationsPerMonth,
    int? MaxMessagesPerMonth,
    int? MaxFileSizeMb,
    int? MaxStorageMb,
    List<PlanFeatureDto>? Features,
    bool IsActive,
    int SortOrder,
    int TrialDays,
    bool AiAnalysisEnabled,
    bool AiAutoReplyEnabled,
    int? MaxAiAnalysesPerMonth,
    int? MaxAiAutoRepliesPerMonth
);

public record PlanFeatureDto(
    string FeatureId,
    string FeatureName,
    string FeatureCode,
    string? Category,
    bool IsEnabled,
    string? LimitValue
);

public record SubscriptionDto(
    string Id,
    string SiteId,
    string PlanId,
    string PlanName,
    string Status,
    string BillingCycle,
    DateTime CurrentPeriodStart,
    DateTime CurrentPeriodEnd,
    DateTime? TrialStart,
    DateTime? TrialEnd,
    DateTime? CanceledAt,
    DateTime? CancelAt,
    bool CancelAtPeriodEnd,
    bool AutoPayEnabled,
    string? PreferredPaymentGateway,
    string? DefaultPaymentMethodId,
    DateTime CreatedAt
);

public record CreateSubscriptionRequest(
    string PlanId,
    string BillingCycle = "monthly",
    string? PaymentMethodId = null,
    string? CouponCode = null
);

public record UpdateSubscriptionRequest(
    string? PlanId,
    string? BillingCycle,
    bool? CancelAtPeriodEnd
);

public record CancelSubscriptionRequest(
    bool Immediate = false,
    string? Reason = null
);

public record ReactivateSubscriptionRequest(
    string? PlanId = null
);

public record UpgradeSubscriptionRequest(
    string PlanId,
    string BillingCycle = "monthly",
    PaymentCardDto? PaymentMethod = null
);

public record PaymentCardDto(
    string CardNumber,
    string ExpiryDate,
    string Cvv,
    string CardholderName
);

public record SubscriptionUsageDto(
    string MetricName,
    int Used,
    int? Limit,
    double? PercentageUsed,
    DateTime PeriodStart,
    DateTime PeriodEnd
);

public record SubscriptionHistoryDto(
    string Action,
    string? FromPlanName,
    string? ToPlanName,
    string? Reason,
    DateTime CreatedAt
);

// Admin Plan Management DTOs
public record CreatePlanRequest(
    string Name,
    string? Description,
    decimal MonthlyPrice,
    decimal? AnnualPrice,
    string Currency = "USD",
    decimal? MonthlyPriceInr = null,
    decimal? AnnualPriceInr = null,
    bool InrEnabled = false,
    int? MaxAgents = null,
    int? MaxConversationsPerMonth = null,
    int? MaxMessagesPerMonth = null,
    int? MaxFileSizeMb = null,
    int? MaxStorageMb = null,
    int? MessageHistoryDays = null,
    bool IsActive = true,
    bool IsPublic = true,
    int SortOrder = 0,
    int TrialDays = 0,
    bool AiAnalysisEnabled = false,
    bool AiAutoReplyEnabled = false,
    int? MaxAiAnalysesPerMonth = null,
    int? MaxAiAutoRepliesPerMonth = null
);

public record UpdatePlanRequest(
    string? Name = null,
    string? Description = null,
    decimal? MonthlyPrice = null,
    decimal? AnnualPrice = null,
    string? Currency = null,
    decimal? MonthlyPriceInr = null,
    decimal? AnnualPriceInr = null,
    bool? InrEnabled = null,
    int? MaxAgents = null,
    int? MaxConversationsPerMonth = null,
    int? MaxMessagesPerMonth = null,
    int? MaxFileSizeMb = null,
    int? MaxStorageMb = null,
    int? MessageHistoryDays = null,
    bool? IsActive = null,
    bool? IsPublic = null,
    int? SortOrder = null,
    int? TrialDays = null,
    bool? AiAnalysisEnabled = null,
    bool? AiAutoReplyEnabled = null,
    int? MaxAiAnalysesPerMonth = null,
    int? MaxAiAutoRepliesPerMonth = null
);

public record PlanDetailDto(
    string Id,
    string Name,
    string? Description,
    decimal MonthlyPrice,
    decimal? AnnualPrice,
    string Currency,
    decimal? MonthlyPriceInr,
    decimal? AnnualPriceInr,
    bool InrEnabled,
    int? MaxAgents,
    int? MaxConversationsPerMonth,
    int? MaxMessagesPerMonth,
    int? MaxFileSizeMb,
    int? MaxStorageMb,
    int? MessageHistoryDays,
    bool IsActive,
    bool IsPublic,
    int SortOrder,
    int TrialDays,
    bool AiAnalysisEnabled,
    bool AiAutoReplyEnabled,
    int? MaxAiAnalysesPerMonth,
    int? MaxAiAutoRepliesPerMonth,
    int SubscribersCount,
    DateTime CreatedAt
);

// Combined plan and usage overview for site-admin dashboard
public record SitePlanOverviewDto(
    SubscriptionPlanDto? Plan,
    SubscriptionDto? Subscription,
    List<SubscriptionUsageDto> Usage,
    bool HasActivePlan
);

// Auto-pay DTOs
public record SetAutoPayRequest(
    bool Enabled,
    string? PaymentGateway = null, // razorpay, paypal
    string? PaymentMethodId = null
);

public record AutoPaySettingsDto(
    bool Enabled,
    string? PaymentGateway,
    string? PaymentMethodId,
    string? PaymentMethodLast4,
    string? PaymentMethodBrand,
    DateTime? NextChargeDate,
    decimal? NextChargeAmount
);

public record AutoPayResultDto(
    bool Success,
    string? PaymentId,
    string? InvoiceId,
    string? TransactionId,
    string? Message,
    DateTime? NextPeriodEnd
);
