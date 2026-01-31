namespace ChatApp.API.Models.Entities;

public class PaymentMethod : BaseEntity
{
    public string SiteId { get; set; } = string.Empty;
    public Site Site { get; set; } = null!;

    public string Type { get; set; } = "card"; // card, bank_account
    public string? Last4 { get; set; }
    public string? Brand { get; set; }
    public int? ExpMonth { get; set; }
    public int? ExpYear { get; set; }
    public string? BankName { get; set; }

    public bool IsDefault { get; set; }

    // Payment Gateway
    public string? Gateway { get; set; } // razorpay, paypal, stripe

    // Stripe
    public string? StripePaymentMethodId { get; set; }

    // Razorpay (for recurring)
    public string? RazorpayCustomerId { get; set; }
    public string? RazorpayTokenId { get; set; }

    // PayPal (for recurring)
    public string? PayPalBillingAgreementId { get; set; }
    public string? PayPalPayerId { get; set; }
}

public class Coupon : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }

    public string DiscountType { get; set; } = "percentage"; // percentage, fixed
    public decimal DiscountValue { get; set; }
    public string? Currency { get; set; }

    public int? MaxRedemptions { get; set; }
    public int TimesRedeemed { get; set; }

    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidUntil { get; set; }

    public bool IsActive { get; set; } = true;

    // Stripe
    public string? StripeCouponId { get; set; }

    // Navigation properties
    public ICollection<CouponRedemption> Redemptions { get; set; } = new List<CouponRedemption>();
}

public class CouponRedemption : BaseEntityWithIntId
{
    public string CouponId { get; set; } = string.Empty;
    public Coupon Coupon { get; set; } = null!;

    public string SiteId { get; set; } = string.Empty;
    public Site Site { get; set; } = null!;

    public string? SubscriptionId { get; set; }
    public Subscription? Subscription { get; set; }

    public decimal DiscountAmount { get; set; }
    public DateTime RedeemedAt { get; set; } = DateTime.UtcNow;
}

public class Invoice : BaseEntity
{
    public string SiteId { get; set; } = string.Empty;
    public Site Site { get; set; } = null!;

    public string? SubscriptionId { get; set; }
    public Subscription? Subscription { get; set; }

    public string InvoiceNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "draft"; // draft, open, paid, void, uncollectible

    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal AmountDue { get; set; }
    public string Currency { get; set; } = "USD";

    public DateTime? DueDate { get; set; }
    public DateTime? PaidAt { get; set; }

    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    public string? Notes { get; set; }

    // Stripe
    public string? StripeInvoiceId { get; set; }
    public string? StripeInvoiceUrl { get; set; }
    public string? StripeInvoicePdf { get; set; }

    // Navigation properties
    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public class InvoiceItem : BaseEntityWithIntId
{
    public string InvoiceId { get; set; } = string.Empty;
    public Invoice Invoice { get; set; } = null!;

    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
}

public class Payment : BaseEntity
{
    public string SiteId { get; set; } = string.Empty;
    public Site Site { get; set; } = null!;

    public string? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public string? PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = "pending"; // pending, succeeded, failed, refunded, partially_refunded

    public string? FailureReason { get; set; }

    // Stripe
    public string? StripePaymentIntentId { get; set; }
    public string? StripeChargeId { get; set; }

    // Navigation properties
    public ICollection<PaymentRefund> Refunds { get; set; } = new List<PaymentRefund>();
}

public class PaymentRefund : BaseEntityWithIntId
{
    public string PaymentId { get; set; } = string.Empty;
    public Payment Payment { get; set; } = null!;

    public decimal Amount { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = "pending"; // pending, succeeded, failed

    // Stripe
    public string? StripeRefundId { get; set; }

    public DateTime RefundedAt { get; set; } = DateTime.UtcNow;
}
