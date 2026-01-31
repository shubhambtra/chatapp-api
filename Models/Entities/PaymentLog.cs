namespace ChatApp.API.Models.Entities;

public class PaymentLog
{
    public int Id { get; set; }

    public string? SiteId { get; set; }
    public Site? Site { get; set; }

    public string? SubscriptionId { get; set; }
    public Subscription? Subscription { get; set; }

    public string? PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }

    public string? PaymentId { get; set; }
    public Payment? Payment { get; set; }

    // Log details
    public string Action { get; set; } = string.Empty; // create_order, verify_payment, auto_pay_attempt, auto_pay_success, auto_pay_failure, refund, etc.
    public string Gateway { get; set; } = string.Empty; // razorpay, paypal, stripe
    public string Status { get; set; } = string.Empty; // initiated, processing, success, failed, error

    // Request/Response logging
    public string? RequestData { get; set; } // JSON - masked sensitive data
    public string? ResponseData { get; set; } // JSON
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public string? StackTrace { get; set; }

    // Transaction references
    public string? TransactionId { get; set; }
    public string? OrderId { get; set; }
    public string? PaymentReference { get; set; } // Links registration payments to sites
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }

    // Context
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? UserId { get; set; }

    // Metadata
    public string? Metadata { get; set; } // JSON for additional context

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? DurationMs { get; set; } // Time taken for the operation
}
