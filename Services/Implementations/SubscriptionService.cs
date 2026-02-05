using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ChatApp.API.Data;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Models.Entities;
using ChatApp.API.Services.Interfaces;

namespace ChatApp.API.Services.Implementations;

public class SubscriptionService : ISubscriptionService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public SubscriptionService(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<List<SubscriptionPlanDto>> GetPlansAsync(bool includeInactive = false)
    {
        var query = _context.SubscriptionPlans
            .Include(p => p.PlanFeatures)
            .ThenInclude(pf => pf.Feature)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive && p.IsPublic);
        }

        var plans = await query.OrderBy(p => p.SortOrder).ToListAsync();

        return plans.Select(MapPlanToDto).ToList();
    }

    public async Task<SubscriptionPlanDto?> GetPlanAsync(string planId)
    {
        var plan = await _context.SubscriptionPlans
            .Include(p => p.PlanFeatures)
            .ThenInclude(pf => pf.Feature)
            .FirstOrDefaultAsync(p => p.Id == planId);

        return plan != null ? MapPlanToDto(plan) : null;
    }

    public async Task<SubscriptionDto> CreateSubscriptionAsync(string siteId, CreateSubscriptionRequest request)
    {
        var plan = await _context.SubscriptionPlans.FindAsync(request.PlanId);
        if (plan == null) throw new KeyNotFoundException("Plan not found");

        // Check for existing active subscription
        var existingSubscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.SiteId == siteId && s.Status == "active");

        if (existingSubscription != null)
        {
            throw new InvalidOperationException("Site already has an active subscription");
        }

        var subscription = new Subscription
        {
            SiteId = siteId,
            PlanId = request.PlanId,
            Status = "active",
            BillingCycle = request.BillingCycle,
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = request.BillingCycle == "annual"
                ? DateTime.UtcNow.AddYears(1)
                : DateTime.UtcNow.AddMonths(1)
        };

        _context.Subscriptions.Add(subscription);

        // Record history
        _context.SubscriptionHistories.Add(new SubscriptionHistory
        {
            SubscriptionId = subscription.Id,
            Action = "created",
            ToPlanId = request.PlanId
        });

        await _context.SaveChangesAsync();

        return await GetSubscriptionDtoAsync(subscription);
    }

    public async Task<SubscriptionDto?> GetSubscriptionAsync(string siteId)
    {
        var subscription = await _context.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.SiteId == siteId &&
                (s.Status == "active" || s.Status == "trialing" || s.Status == "expired"));

        return subscription != null ? await GetSubscriptionDtoAsync(subscription) : null;
    }

    public async Task<SubscriptionDto> UpdateSubscriptionAsync(string siteId, UpdateSubscriptionRequest request)
    {
        var subscription = await _context.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.SiteId == siteId && (s.Status == "active" || s.Status == "trialing"));

        if (subscription == null) throw new KeyNotFoundException("No active subscription found");

        var oldPlanId = subscription.PlanId;

        if (request.PlanId != null && request.PlanId != subscription.PlanId)
        {
            var newPlan = await _context.SubscriptionPlans.FindAsync(request.PlanId);
            if (newPlan == null) throw new KeyNotFoundException("Plan not found");

            subscription.PlanId = request.PlanId;

            // Activate trialing subscriptions when upgrading to a paid plan
            if (subscription.Status == "trialing")
            {
                subscription.Status = "active";
                var now = DateTime.UtcNow;
                var billingCycle = request.BillingCycle ?? subscription.BillingCycle;
                subscription.CurrentPeriodStart = now;
                subscription.CurrentPeriodEnd = billingCycle == "yearly" || billingCycle == "annual"
                    ? now.AddYears(1)
                    : now.AddMonths(1);
                subscription.TrialEnd = now;
            }

            var action = newPlan.MonthlyPrice > subscription.Plan.MonthlyPrice ? "upgraded" : "downgraded";
            _context.SubscriptionHistories.Add(new SubscriptionHistory
            {
                SubscriptionId = subscription.Id,
                Action = action,
                FromPlanId = oldPlanId,
                ToPlanId = request.PlanId
            });
        }

        if (request.BillingCycle != null)
        {
            subscription.BillingCycle = request.BillingCycle;
        }

        if (request.CancelAtPeriodEnd.HasValue)
        {
            subscription.CancelAtPeriodEnd = request.CancelAtPeriodEnd.Value;
            subscription.CancelAt = request.CancelAtPeriodEnd.Value ? subscription.CurrentPeriodEnd : null;
        }

        await _context.SaveChangesAsync();

        return await GetSubscriptionDtoAsync(subscription);
    }

    public async Task<SubscriptionDto> CancelSubscriptionAsync(string siteId, CancelSubscriptionRequest request)
    {
        var subscription = await _context.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.SiteId == siteId && s.Status == "active");

        if (subscription == null) throw new KeyNotFoundException("No active subscription found");

        if (request.Immediate)
        {
            subscription.Status = "canceled";
            subscription.CanceledAt = DateTime.UtcNow;
        }
        else
        {
            subscription.CancelAtPeriodEnd = true;
            subscription.CancelAt = subscription.CurrentPeriodEnd;
        }

        _context.SubscriptionHistories.Add(new SubscriptionHistory
        {
            SubscriptionId = subscription.Id,
            Action = "canceled",
            Reason = request.Reason
        });

        await _context.SaveChangesAsync();

        return await GetSubscriptionDtoAsync(subscription);
    }

    public async Task<SubscriptionDto> ReactivateSubscriptionAsync(string siteId, ReactivateSubscriptionRequest request)
    {
        var subscription = await _context.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.SiteId == siteId &&
                (s.Status == "canceled" || s.CancelAtPeriodEnd));

        if (subscription == null) throw new KeyNotFoundException("No canceled subscription found");

        subscription.Status = "active";
        subscription.CancelAtPeriodEnd = false;
        subscription.CancelAt = null;
        subscription.CanceledAt = null;

        if (request.PlanId != null)
        {
            subscription.PlanId = request.PlanId;
        }

        _context.SubscriptionHistories.Add(new SubscriptionHistory
        {
            SubscriptionId = subscription.Id,
            Action = "reactivated"
        });

        await _context.SaveChangesAsync();

        return await GetSubscriptionDtoAsync(subscription);
    }

    public async Task<SubscriptionDto> UpgradeSubscriptionAsync(string siteId, UpgradeSubscriptionRequest request)
    {
        // Get current subscription
        var subscription = await _context.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.SiteId == siteId && s.Status == "active");

        if (subscription == null)
            throw new KeyNotFoundException("No active subscription found");

        // Get the new plan
        var newPlan = await _context.SubscriptionPlans.FindAsync(request.PlanId);
        if (newPlan == null)
            throw new KeyNotFoundException("Plan not found");

        if (!newPlan.IsActive)
            throw new InvalidOperationException("Selected plan is not available");

        var oldPlanId = subscription.PlanId;
        var oldPlan = subscription.Plan;

        // Validate this is actually an upgrade (or allow same-tier plan changes)
        if (newPlan.Id == oldPlanId)
            throw new InvalidOperationException("Already subscribed to this plan");

        // Process payment (in a real system, this would integrate with a payment provider)
        // For now, we'll simulate payment processing with test cards
        if (request.PaymentMethod != null)
        {
            var cardNumber = request.PaymentMethod.CardNumber?.Replace(" ", "") ?? "";

            // Validate card number (basic validation)
            if (string.IsNullOrEmpty(cardNumber) || cardNumber.Length < 15)
            {
                throw new InvalidOperationException("Invalid card number");
            }

            // Validate expiry date
            if (string.IsNullOrEmpty(request.PaymentMethod.ExpiryDate) ||
                !request.PaymentMethod.ExpiryDate.Contains("/"))
            {
                throw new InvalidOperationException("Invalid expiry date format. Use MM/YY");
            }

            // Validate CVV
            if (string.IsNullOrEmpty(request.PaymentMethod.Cvv) ||
                request.PaymentMethod.Cvv.Length < 3)
            {
                throw new InvalidOperationException("Invalid CVV");
            }

            // Test card scenarios (similar to Stripe test cards)
            // Success cards: 4242424242424242, 5555555555554444, 378282246310005
            // Decline cards: 4000000000000002
            // Insufficient funds: 4000000000009995
            // Expired card: 4000000000000069

            if (cardNumber == "4000000000000002")
            {
                throw new InvalidOperationException("Payment declined. Please use a different card.");
            }
            else if (cardNumber == "4000000000009995")
            {
                throw new InvalidOperationException("Insufficient funds. Please use a different card.");
            }
            else if (cardNumber == "4000000000000069")
            {
                throw new InvalidOperationException("Card has expired. Please use a different card.");
            }
            else if (!IsValidTestCard(cardNumber))
            {
                throw new InvalidOperationException("Invalid card number. For testing, use: 4242 4242 4242 4242");
            }

            // Payment successful - in production, you would:
            // 1. Tokenize the card with Stripe/PayPal
            // 2. Create a charge for the prorated amount
            // 3. Update the payment method on file
        }
        else
        {
            throw new InvalidOperationException("Payment method is required for plan upgrade");
        }

        // Calculate new period based on billing cycle
        var billingCycle = request.BillingCycle ?? subscription.BillingCycle;
        var periodEnd = billingCycle == "yearly" || billingCycle == "annual"
            ? DateTime.UtcNow.AddYears(1)
            : DateTime.UtcNow.AddMonths(1);

        // Update subscription
        subscription.PlanId = request.PlanId;
        subscription.BillingCycle = billingCycle;
        subscription.CurrentPeriodStart = DateTime.UtcNow;
        subscription.CurrentPeriodEnd = periodEnd;

        // Determine if this is an upgrade or downgrade based on price
        var isUpgrade = newPlan.MonthlyPrice > (oldPlan?.MonthlyPrice ?? 0);
        var action = isUpgrade ? "upgraded" : "plan_changed";

        // Record history
        _context.SubscriptionHistories.Add(new SubscriptionHistory
        {
            SubscriptionId = subscription.Id,
            Action = action,
            FromPlanId = oldPlanId,
            ToPlanId = request.PlanId,
            Reason = $"Plan changed from {oldPlan?.Name ?? "Unknown"} to {newPlan.Name}"
        });

        await _context.SaveChangesAsync();

        return await GetSubscriptionDtoAsync(subscription);
    }

    public async Task<List<SubscriptionUsageDto>> GetUsageAsync(string siteId)
    {
        var subscription = await _context.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.SiteId == siteId && s.Status == "active");

        if (subscription == null) return new List<SubscriptionUsageDto>();

        var usageRecords = await _context.UsageRecords
            .Where(ur => ur.SiteId == siteId &&
                        ur.PeriodStart >= subscription.CurrentPeriodStart &&
                        ur.PeriodEnd <= subscription.CurrentPeriodEnd)
            .GroupBy(ur => ur.MetricName)
            .Select(g => new { MetricName = g.Key, Total = g.Sum(ur => ur.Quantity) })
            .ToListAsync();

        var usageDtos = new List<SubscriptionUsageDto>();

        foreach (var usage in usageRecords)
        {
            int? limit = usage.MetricName switch
            {
                "conversations" => subscription.Plan.MaxConversationsPerMonth,
                "messages" => subscription.Plan.MaxMessagesPerMonth,
                "agents" => subscription.Plan.MaxAgents,
                "storage_mb" => subscription.Plan.MaxStorageMb,
                "ai_analyses" => subscription.Plan.AiAnalysisEnabled ? subscription.Plan.MaxAiAnalysesPerMonth : 0,
                "ai_auto_replies" => subscription.Plan.AiAutoReplyEnabled ? subscription.Plan.MaxAiAutoRepliesPerMonth : 0,
                _ => null
            };

            usageDtos.Add(new SubscriptionUsageDto(
                usage.MetricName,
                usage.Total,
                limit,
                limit.HasValue && limit.Value > 0 ? (double)usage.Total / limit.Value * 100 : null,
                subscription.CurrentPeriodStart,
                subscription.CurrentPeriodEnd
            ));
        }

        // Add AI usage metrics even if no usage records exist (to show the limits)
        var plan = subscription.Plan;
        if (plan.AiAnalysisEnabled && !usageDtos.Any(u => u.MetricName == "ai_analyses"))
        {
            usageDtos.Add(new SubscriptionUsageDto(
                "ai_analyses",
                0,
                plan.MaxAiAnalysesPerMonth,
                plan.MaxAiAnalysesPerMonth.HasValue ? 0 : null,
                subscription.CurrentPeriodStart,
                subscription.CurrentPeriodEnd
            ));
        }
        if (plan.AiAutoReplyEnabled && !usageDtos.Any(u => u.MetricName == "ai_auto_replies"))
        {
            usageDtos.Add(new SubscriptionUsageDto(
                "ai_auto_replies",
                0,
                plan.MaxAiAutoRepliesPerMonth,
                plan.MaxAiAutoRepliesPerMonth.HasValue ? 0 : null,
                subscription.CurrentPeriodStart,
                subscription.CurrentPeriodEnd
            ));
        }

        return usageDtos;
    }

    public async Task RecordUsageAsync(string siteId, string metricName, int quantity)
    {
        var subscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.SiteId == siteId && s.Status == "active");

        if (subscription == null) return;

        var usageRecord = new UsageRecord
        {
            SubscriptionId = subscription.Id,
            SiteId = siteId,
            MetricName = metricName,
            Quantity = quantity,
            PeriodStart = subscription.CurrentPeriodStart,
            PeriodEnd = subscription.CurrentPeriodEnd
        };

        _context.UsageRecords.Add(usageRecord);
        await _context.SaveChangesAsync();
    }

    public async Task<List<SubscriptionHistoryDto>> GetHistoryAsync(string siteId)
    {
        var subscription = await _context.Subscriptions
            .Include(s => s.History)
            .FirstOrDefaultAsync(s => s.SiteId == siteId);

        if (subscription == null) return new List<SubscriptionHistoryDto>();

        var planIds = subscription.History
            .SelectMany(h => new[] { h.FromPlanId, h.ToPlanId })
            .Where(id => id != null)
            .Distinct()
            .ToList();

        var plans = await _context.SubscriptionPlans
            .Where(p => planIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name);

        return subscription.History
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => new SubscriptionHistoryDto(
                h.Action,
                h.FromPlanId != null && plans.TryGetValue(h.FromPlanId, out var fromName) ? fromName : null,
                h.ToPlanId != null && plans.TryGetValue(h.ToPlanId, out var toName) ? toName : null,
                h.Reason,
                h.CreatedAt
            ))
            .ToList();
    }

    // Admin Plan Management
    public async Task<List<PlanDetailDto>> GetAllPlansAsync()
    {
        var plans = await _context.SubscriptionPlans
            .OrderBy(p => p.SortOrder)
            .Select(p => new PlanDetailDto(
                p.Id,
                p.Name,
                p.Description,
                p.MonthlyPrice,
                p.AnnualPrice,
                p.Currency,
                p.MonthlyPriceInr,
                p.AnnualPriceInr,
                p.InrEnabled,
                p.MaxAgents,
                p.MaxConversationsPerMonth,
                p.MaxMessagesPerMonth,
                p.MaxFileSizeMb,
                p.MaxStorageMb,
                p.MessageHistoryDays,
                p.IsActive,
                p.IsPublic,
                p.SortOrder,
                p.TrialDays,
                p.AiAnalysisEnabled,
                p.AiAutoReplyEnabled,
                p.MaxAiAnalysesPerMonth,
                p.MaxAiAutoRepliesPerMonth,
                p.Subscriptions.Count(s => s.Status == "active"),
                p.CreatedAt
            ))
            .ToListAsync();

        return plans;
    }

    public async Task<PlanDetailDto?> GetPlanDetailAsync(string planId)
    {
        var plan = await _context.SubscriptionPlans
            .Where(p => p.Id == planId)
            .Select(p => new PlanDetailDto(
                p.Id,
                p.Name,
                p.Description,
                p.MonthlyPrice,
                p.AnnualPrice,
                p.Currency,
                p.MonthlyPriceInr,
                p.AnnualPriceInr,
                p.InrEnabled,
                p.MaxAgents,
                p.MaxConversationsPerMonth,
                p.MaxMessagesPerMonth,
                p.MaxFileSizeMb,
                p.MaxStorageMb,
                p.MessageHistoryDays,
                p.IsActive,
                p.IsPublic,
                p.SortOrder,
                p.TrialDays,
                p.AiAnalysisEnabled,
                p.AiAutoReplyEnabled,
                p.MaxAiAnalysesPerMonth,
                p.MaxAiAutoRepliesPerMonth,
                p.Subscriptions.Count(s => s.Status == "active"),
                p.CreatedAt
            ))
            .FirstOrDefaultAsync();

        return plan;
    }

    public async Task<PlanDetailDto> CreatePlanAsync(CreatePlanRequest request)
    {
        var plan = new SubscriptionPlan
        {
            Name = request.Name,
            Description = request.Description,
            MonthlyPrice = request.MonthlyPrice,
            AnnualPrice = request.AnnualPrice,
            Currency = request.Currency,
            MonthlyPriceInr = request.MonthlyPriceInr,
            AnnualPriceInr = request.AnnualPriceInr,
            InrEnabled = request.InrEnabled,
            MaxAgents = request.MaxAgents,
            MaxConversationsPerMonth = request.MaxConversationsPerMonth,
            MaxMessagesPerMonth = request.MaxMessagesPerMonth,
            MaxFileSizeMb = request.MaxFileSizeMb,
            MaxStorageMb = request.MaxStorageMb,
            MessageHistoryDays = request.MessageHistoryDays,
            IsActive = request.IsActive,
            IsPublic = request.IsPublic,
            SortOrder = request.SortOrder,
            TrialDays = request.TrialDays,
            AiAnalysisEnabled = request.AiAnalysisEnabled,
            AiAutoReplyEnabled = request.AiAutoReplyEnabled,
            MaxAiAnalysesPerMonth = request.MaxAiAnalysesPerMonth,
            MaxAiAutoRepliesPerMonth = request.MaxAiAutoRepliesPerMonth
        };

        _context.SubscriptionPlans.Add(plan);
        await _context.SaveChangesAsync();

        return new PlanDetailDto(
            plan.Id,
            plan.Name,
            plan.Description,
            plan.MonthlyPrice,
            plan.AnnualPrice,
            plan.Currency,
            plan.MonthlyPriceInr,
            plan.AnnualPriceInr,
            plan.InrEnabled,
            plan.MaxAgents,
            plan.MaxConversationsPerMonth,
            plan.MaxMessagesPerMonth,
            plan.MaxFileSizeMb,
            plan.MaxStorageMb,
            plan.MessageHistoryDays,
            plan.IsActive,
            plan.IsPublic,
            plan.SortOrder,
            plan.TrialDays,
            plan.AiAnalysisEnabled,
            plan.AiAutoReplyEnabled,
            plan.MaxAiAnalysesPerMonth,
            plan.MaxAiAutoRepliesPerMonth,
            0,
            plan.CreatedAt
        );
    }

    public async Task<PlanDetailDto> UpdatePlanAsync(string planId, UpdatePlanRequest request)
    {
        var plan = await _context.SubscriptionPlans.FindAsync(planId);
        if (plan == null) throw new KeyNotFoundException("Plan not found");

        if (request.Name != null) plan.Name = request.Name;
        if (request.Description != null) plan.Description = request.Description;
        if (request.MonthlyPrice.HasValue) plan.MonthlyPrice = request.MonthlyPrice.Value;
        if (request.AnnualPrice.HasValue) plan.AnnualPrice = request.AnnualPrice.Value;
        if (request.Currency != null) plan.Currency = request.Currency;
        if (request.MonthlyPriceInr.HasValue) plan.MonthlyPriceInr = request.MonthlyPriceInr.Value;
        if (request.AnnualPriceInr.HasValue) plan.AnnualPriceInr = request.AnnualPriceInr.Value;
        if (request.InrEnabled.HasValue) plan.InrEnabled = request.InrEnabled.Value;
        if (request.MaxAgents.HasValue) plan.MaxAgents = request.MaxAgents.Value;
        if (request.MaxConversationsPerMonth.HasValue) plan.MaxConversationsPerMonth = request.MaxConversationsPerMonth.Value;
        if (request.MaxMessagesPerMonth.HasValue) plan.MaxMessagesPerMonth = request.MaxMessagesPerMonth.Value;
        if (request.MaxFileSizeMb.HasValue) plan.MaxFileSizeMb = request.MaxFileSizeMb.Value;
        if (request.MaxStorageMb.HasValue) plan.MaxStorageMb = request.MaxStorageMb.Value;
        if (request.MessageHistoryDays.HasValue) plan.MessageHistoryDays = request.MessageHistoryDays.Value;
        if (request.IsActive.HasValue) plan.IsActive = request.IsActive.Value;
        if (request.IsPublic.HasValue) plan.IsPublic = request.IsPublic.Value;
        if (request.SortOrder.HasValue) plan.SortOrder = request.SortOrder.Value;
        if (request.TrialDays.HasValue) plan.TrialDays = request.TrialDays.Value;
        if (request.AiAnalysisEnabled.HasValue) plan.AiAnalysisEnabled = request.AiAnalysisEnabled.Value;
        if (request.AiAutoReplyEnabled.HasValue) plan.AiAutoReplyEnabled = request.AiAutoReplyEnabled.Value;
        if (request.MaxAiAnalysesPerMonth.HasValue) plan.MaxAiAnalysesPerMonth = request.MaxAiAnalysesPerMonth.Value;
        if (request.MaxAiAutoRepliesPerMonth.HasValue) plan.MaxAiAutoRepliesPerMonth = request.MaxAiAutoRepliesPerMonth.Value;

        await _context.SaveChangesAsync();

        var subscribersCount = await _context.Subscriptions.CountAsync(s => s.PlanId == planId && s.Status == "active");

        return new PlanDetailDto(
            plan.Id,
            plan.Name,
            plan.Description,
            plan.MonthlyPrice,
            plan.AnnualPrice,
            plan.Currency,
            plan.MonthlyPriceInr,
            plan.AnnualPriceInr,
            plan.InrEnabled,
            plan.MaxAgents,
            plan.MaxConversationsPerMonth,
            plan.MaxMessagesPerMonth,
            plan.MaxFileSizeMb,
            plan.MaxStorageMb,
            plan.MessageHistoryDays,
            plan.IsActive,
            plan.IsPublic,
            plan.SortOrder,
            plan.TrialDays,
            plan.AiAnalysisEnabled,
            plan.AiAutoReplyEnabled,
            plan.MaxAiAnalysesPerMonth,
            plan.MaxAiAutoRepliesPerMonth,
            subscribersCount,
            plan.CreatedAt
        );
    }

    public async Task DeletePlanAsync(string planId)
    {
        var plan = await _context.SubscriptionPlans.FindAsync(planId);
        if (plan == null) throw new KeyNotFoundException("Plan not found");

        // Check if plan has active subscribers
        var hasSubscribers = await _context.Subscriptions.AnyAsync(s => s.PlanId == planId && s.Status == "active");
        if (hasSubscribers)
        {
            throw new InvalidOperationException("Cannot delete plan with active subscribers");
        }

        _context.SubscriptionPlans.Remove(plan);
        await _context.SaveChangesAsync();
    }

    private static SubscriptionPlanDto MapPlanToDto(SubscriptionPlan plan) => new(
        plan.Id,
        plan.Name,
        plan.Description,
        plan.MonthlyPrice,
        plan.AnnualPrice,
        plan.Currency,
        plan.MonthlyPriceInr,
        plan.AnnualPriceInr,
        plan.InrEnabled,
        plan.MaxAgents,
        plan.MaxConversationsPerMonth,
        plan.MaxMessagesPerMonth,
        plan.MaxFileSizeMb,
        plan.MaxStorageMb,
        plan.PlanFeatures.Select(pf => new PlanFeatureDto(
            pf.FeatureId,
            pf.Feature.Name,
            pf.Feature.Code,
            pf.Feature.Category,
            pf.IsEnabled,
            pf.LimitValue
        )).ToList(),
        plan.IsActive,
        plan.SortOrder,
        plan.TrialDays,
        plan.AiAnalysisEnabled,
        plan.AiAutoReplyEnabled,
        plan.MaxAiAnalysesPerMonth,
        plan.MaxAiAutoRepliesPerMonth
    );

    private async Task<SubscriptionDto> GetSubscriptionDtoAsync(Subscription subscription)
    {
        var plan = subscription.Plan ?? await _context.SubscriptionPlans.FindAsync(subscription.PlanId);

        return new SubscriptionDto(
            subscription.Id,
            subscription.SiteId,
            subscription.PlanId,
            plan?.Name ?? "",
            subscription.Status,
            subscription.BillingCycle,
            subscription.CurrentPeriodStart,
            subscription.CurrentPeriodEnd,
            subscription.TrialStart,
            subscription.TrialEnd,
            subscription.CanceledAt,
            subscription.CancelAt,
            subscription.CancelAtPeriodEnd,
            subscription.AutoPayEnabled,
            subscription.PreferredPaymentGateway,
            subscription.DefaultPaymentMethodId,
            subscription.CreatedAt
        );
    }

    // Auto-pay Management Methods
    public async Task<AutoPaySettingsDto> GetAutoPaySettingsAsync(string siteId)
    {
        var subscription = await _context.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.SiteId == siteId && s.Status == "active");

        if (subscription == null)
            throw new KeyNotFoundException("No active subscription found");

        PaymentMethod? paymentMethod = null;
        if (!string.IsNullOrEmpty(subscription.DefaultPaymentMethodId))
        {
            paymentMethod = await _context.PaymentMethods
                .FirstOrDefaultAsync(pm => pm.Id == subscription.DefaultPaymentMethodId);
        }

        var nextChargeAmount = subscription.BillingCycle == "annual"
            ? subscription.Plan?.AnnualPrice ?? subscription.Plan?.MonthlyPrice
            : subscription.Plan?.MonthlyPrice;

        return new AutoPaySettingsDto(
            subscription.AutoPayEnabled,
            subscription.PreferredPaymentGateway,
            subscription.DefaultPaymentMethodId,
            paymentMethod?.Last4,
            paymentMethod?.Brand,
            subscription.AutoPayEnabled ? subscription.CurrentPeriodEnd : null,
            subscription.AutoPayEnabled ? nextChargeAmount : null
        );
    }

    public async Task<AutoPaySettingsDto> SetAutoPayAsync(string siteId, SetAutoPayRequest request)
    {
        var subscription = await _context.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.SiteId == siteId && s.Status == "active");

        if (subscription == null)
            throw new KeyNotFoundException("No active subscription found");

        // If enabling auto-pay, validate payment method exists
        if (request.Enabled)
        {
            var paymentMethodId = request.PaymentMethodId;

            // If no payment method specified, use default
            if (string.IsNullOrEmpty(paymentMethodId))
            {
                var defaultMethod = await _context.PaymentMethods
                    .FirstOrDefaultAsync(pm => pm.SiteId == siteId && pm.IsDefault);

                if (defaultMethod == null)
                    throw new InvalidOperationException("No payment method found. Please add a payment method before enabling auto-pay.");

                paymentMethodId = defaultMethod.Id;
            }
            else
            {
                // Verify the specified payment method exists and belongs to this site
                var paymentMethod = await _context.PaymentMethods
                    .FirstOrDefaultAsync(pm => pm.Id == paymentMethodId && pm.SiteId == siteId);

                if (paymentMethod == null)
                    throw new InvalidOperationException("Payment method not found");
            }

            subscription.AutoPayEnabled = true;
            subscription.DefaultPaymentMethodId = paymentMethodId;
            subscription.PreferredPaymentGateway = request.PaymentGateway;

            // If auto-pay is enabled, ensure cancel at period end is false
            subscription.CancelAtPeriodEnd = false;
            subscription.CancelAt = null;
        }
        else
        {
            subscription.AutoPayEnabled = false;
        }

        await _context.SaveChangesAsync();

        return await GetAutoPaySettingsAsync(siteId);
    }

    public async Task<List<Subscription>> GetSubscriptionsForAutoPayAsync()
    {
        var now = DateTime.UtcNow;
        var checkWindow = now.AddDays(1); // Check subscriptions expiring in the next 24 hours

        return await _context.Subscriptions
            .Include(s => s.Site)
                .ThenInclude(site => site.OwnerUser)
            .Include(s => s.Plan)
            .Where(s => s.Status == "active"
                        && s.AutoPayEnabled
                        && !string.IsNullOrEmpty(s.DefaultPaymentMethodId)
                        && s.CurrentPeriodEnd <= checkWindow
                        && s.CurrentPeriodEnd > now
                        && !s.CancelAtPeriodEnd
                        && s.Plan.MonthlyPrice > 0) // Only paid plans
            .ToListAsync();
    }

    public async Task RenewSubscriptionAsync(string subscriptionId, DateTime newPeriodEnd)
    {
        var subscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.Id == subscriptionId);

        if (subscription == null)
            throw new KeyNotFoundException("Subscription not found");

        subscription.CurrentPeriodStart = subscription.CurrentPeriodEnd;
        subscription.CurrentPeriodEnd = newPeriodEnd;

        _context.SubscriptionHistories.Add(new SubscriptionHistory
        {
            SubscriptionId = subscription.Id,
            Action = "renewed",
            ToPlanId = subscription.PlanId,
            Reason = "Auto-pay renewal"
        });

        await _context.SaveChangesAsync();
    }

    // Limit Checking Methods
    public async Task<string> GetOrCreateFreePlanIdAsync()
    {
        // Look for existing Free plan
        var freePlan = await _context.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Name == "Free" && p.IsActive);

        if (freePlan != null) return freePlan.Id;

        // Create Free plan if it doesn't exist
        freePlan = new SubscriptionPlan
        {
            Name = "Free",
            Description = "Free plan with limited features",
            MonthlyPrice = 0,
            AnnualPrice = 0,
            Currency = "USD",
            MaxAgents = 2,
            MaxConversationsPerMonth = 100,
            MaxMessagesPerMonth = 500,
            MaxFileSizeMb = 5,
            MaxStorageMb = 100,
            MessageHistoryDays = 30,
            IsActive = true,
            IsPublic = true,
            SortOrder = 0,
            TrialDays = 0
        };

        _context.SubscriptionPlans.Add(freePlan);
        await _context.SaveChangesAsync();

        return freePlan.Id;
    }

    public async Task AssignFreePlanToSiteAsync(string siteId)
    {
        // Check if site already has active or trialing subscription
        var existing = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.SiteId == siteId && (s.Status == "active" || s.Status == "trialing"));

        if (existing != null) return; // Already has subscription

        var freePlanId = await GetOrCreateFreePlanIdAsync();
        var freePlan = await _context.SubscriptionPlans.FindAsync(freePlanId);

        var now = DateTime.UtcNow;
        // Default to 14-day trial if TrialDays is not configured
        var trialDays = freePlan != null && freePlan.TrialDays > 0
            ? freePlan.TrialDays
            : 14;

        var subscription = new Subscription
        {
            SiteId = siteId,
            PlanId = freePlanId,
            Status = "trialing",
            BillingCycle = "monthly",
            CurrentPeriodStart = now,
            CurrentPeriodEnd = now.AddDays(trialDays),
            TrialStart = now,
            TrialEnd = now.AddDays(trialDays)
        };

        _context.Subscriptions.Add(subscription);

        _context.SubscriptionHistories.Add(new SubscriptionHistory
        {
            SubscriptionId = subscription.Id,
            Action = "created",
            ToPlanId = freePlanId,
            Reason = $"Auto-assigned Free plan trial ({trialDays} days) on site creation"
        });

        await _context.SaveChangesAsync();
    }

    public async Task<(bool allowed, string? reason, int? limit, int? current)> CheckLimitAsync(string siteId, string metricName)
    {
        var subscription = await _context.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.SiteId == siteId && s.Status == "active");

        // No subscription = no limits (allow but this shouldn't happen)
        if (subscription == null)
            return (true, null, null, null);

        var plan = subscription.Plan;

        // Get current usage for this period
        var currentUsage = await _context.UsageRecords
            .Where(ur => ur.SiteId == siteId &&
                        ur.MetricName == metricName &&
                        ur.PeriodStart >= subscription.CurrentPeriodStart &&
                        ur.PeriodEnd <= subscription.CurrentPeriodEnd)
            .SumAsync(ur => ur.Quantity);

        // For agents, count current agents instead of usage records (excluding site owner)
        if (metricName == "agents")
        {
            var site = await _context.Sites.FindAsync(siteId);
            currentUsage = await _context.UserSites.CountAsync(us => us.SiteId == siteId && us.UserId != site.OwnerUserId);
        }

        int? limit = metricName switch
        {
            "conversations" => plan.MaxConversationsPerMonth,
            "messages" => plan.MaxMessagesPerMonth,
            "agents" => plan.MaxAgents,
            "storage_mb" => plan.MaxStorageMb,
            "ai_analyses" => plan.AiAnalysisEnabled ? plan.MaxAiAnalysesPerMonth : 0,
            "ai_auto_replies" => plan.AiAutoReplyEnabled ? plan.MaxAiAutoRepliesPerMonth : 0,
            _ => null
        };

        // For AI features, check if they're enabled first
        if (metricName == "ai_analyses" && !plan.AiAnalysisEnabled)
        {
            return (false, "AI Analysis is not available in your current plan. Please upgrade to access this feature.", 0, 0);
        }
        if (metricName == "ai_auto_replies" && !plan.AiAutoReplyEnabled)
        {
            return (false, "AI Auto Reply is not available in your current plan. Please upgrade to access this feature.", 0, 0);
        }

        // No limit set = unlimited
        if (!limit.HasValue)
            return (true, null, null, currentUsage);

        if (currentUsage >= limit.Value)
        {
            var friendlyName = metricName switch
            {
                "conversations" => "conversations",
                "messages" => "messages",
                "agents" => "agents",
                "storage_mb" => "storage (MB)",
                "ai_analyses" => "AI analyses",
                "ai_auto_replies" => "AI auto replies",
                _ => metricName
            };
            return (false, $"You have reached your plan's limit of {limit.Value} {friendlyName}. Please upgrade your plan.", limit.Value, currentUsage);
        }

        return (true, null, limit.Value, currentUsage);
    }

    public async Task<SubscriptionPlanDto?> GetSitePlanAsync(string siteId)
    {
        var subscription = await _context.Subscriptions
            .Include(s => s.Plan)
            .ThenInclude(p => p.PlanFeatures)
            .ThenInclude(pf => pf.Feature)
            .FirstOrDefaultAsync(s => s.SiteId == siteId &&
                (s.Status == "active" || s.Status == "trialing" || s.Status == "expired"));

        if (subscription?.Plan == null) return null;

        return MapPlanToDto(subscription.Plan);
    }

    // Helper method for test card validation
    private static bool IsValidTestCard(string cardNumber)
    {
        // Accept these test card numbers (similar to Stripe test cards)
        var validTestCards = new HashSet<string>
        {
            "4242424242424242",  // Visa
            "5555555555554444",  // Mastercard
            "378282246310005",   // Amex
            "6011111111111117",  // Discover
            "4000056655665556",  // Visa (debit)
            "5200828282828210",  // Mastercard (debit)
        };

        if (validTestCards.Contains(cardNumber))
            return true;

        // Also accept any card starting with 4242 for easier testing
        if (cardNumber.StartsWith("4242") && cardNumber.Length >= 16)
            return true;

        // Accept any 16-digit number starting with 4, 5, or 3 for testing
        if (cardNumber.Length >= 15 && cardNumber.Length <= 16)
        {
            if (cardNumber.StartsWith("4") || cardNumber.StartsWith("5") || cardNumber.StartsWith("3"))
                return true;
        }

        return false;
    }
}
