namespace ChatApp.API.Models.Entities;

public class SubscriptionPlan : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Pricing (USD)
    public decimal MonthlyPrice { get; set; }
    public decimal? AnnualPrice { get; set; }
    public string Currency { get; set; } = "USD";

    // Pricing (INR) - for India
    public decimal? MonthlyPriceInr { get; set; }
    public decimal? AnnualPriceInr { get; set; }
    public bool InrEnabled { get; set; } = false;

    // Limits
    public int? MaxAgents { get; set; }
    public int? MaxConversationsPerMonth { get; set; }
    public int? MaxMessagesPerMonth { get; set; }
    public int? MaxFileSizeMb { get; set; }
    public int? MaxStorageMb { get; set; }
    public int? MessageHistoryDays { get; set; }
    public int TrialDays { get; set; }

    // AI Features
    public bool AiAnalysisEnabled { get; set; }
    public bool AiAutoReplyEnabled { get; set; }
    public int? MaxAiAnalysesPerMonth { get; set; }
    public int? MaxAiAutoRepliesPerMonth { get; set; }

    // Status
    public bool IsActive { get; set; } = true;
    public bool IsPublic { get; set; } = true;
    public int SortOrder { get; set; }

    // Stripe
    public string? StripeMonthlyPriceId { get; set; }
    public string? StripeAnnualPriceId { get; set; }

    // Navigation properties
    public ICollection<PlanFeature> PlanFeatures { get; set; } = new List<PlanFeature>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}

public class Feature : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = "general";

    // Navigation properties
    public ICollection<PlanFeature> PlanFeatures { get; set; } = new List<PlanFeature>();
}

public class PlanFeature : BaseEntityWithIntId
{
    public string PlanId { get; set; } = string.Empty;
    public SubscriptionPlan Plan { get; set; } = null!;

    public string FeatureId { get; set; } = string.Empty;
    public Feature Feature { get; set; } = null!;

    public bool IsEnabled { get; set; } = true;
    public string? LimitValue { get; set; }
}

public class Subscription : BaseEntity
{
    public string SiteId { get; set; } = string.Empty;
    public Site Site { get; set; } = null!;

    public string PlanId { get; set; } = string.Empty;
    public SubscriptionPlan Plan { get; set; } = null!;

    public string Status { get; set; } = "active"; // active, canceled, past_due, paused, trialing
    public string BillingCycle { get; set; } = "monthly"; // monthly, annual

    public DateTime CurrentPeriodStart { get; set; }
    public DateTime CurrentPeriodEnd { get; set; }
    public DateTime? TrialStart { get; set; }
    public DateTime? TrialEnd { get; set; }
    public DateTime? CanceledAt { get; set; }
    public DateTime? CancelAt { get; set; }
    public bool CancelAtPeriodEnd { get; set; }

    // Auto-pay settings
    public bool AutoPayEnabled { get; set; }
    public string? PreferredPaymentGateway { get; set; } // razorpay, paypal
    public string? DefaultPaymentMethodId { get; set; }

    // Stripe
    public string? StripeSubscriptionId { get; set; }

    // Navigation properties
    public ICollection<SubscriptionHistory> History { get; set; } = new List<SubscriptionHistory>();
    public ICollection<UsageRecord> UsageRecords { get; set; } = new List<UsageRecord>();
}

public class SubscriptionHistory : BaseEntityWithIntId
{
    public string SubscriptionId { get; set; } = string.Empty;
    public Subscription Subscription { get; set; } = null!;

    public string Action { get; set; } = string.Empty; // created, upgraded, downgraded, canceled, reactivated
    public string? FromPlanId { get; set; }
    public string? ToPlanId { get; set; }
    public string? Reason { get; set; }
    public string? Metadata { get; set; }
}

public class UsageRecord : BaseEntityWithIntId
{
    public string SubscriptionId { get; set; } = string.Empty;
    public Subscription Subscription { get; set; } = null!;

    public string SiteId { get; set; } = string.Empty;
    public Site Site { get; set; } = null!;

    public string MetricName { get; set; } = string.Empty; // conversations, messages, agents, storage_mb
    public int Quantity { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
}
