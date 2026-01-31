namespace ChatApp.API.Models.DTOs;

public record PaymentMethodDto(
    string Id,
    string Type,
    string? Last4,
    string? Brand,
    int? ExpMonth,
    int? ExpYear,
    string? BankName,
    bool IsDefault,
    DateTime CreatedAt
);

public record AddPaymentMethodRequest(
    string PaymentMethodToken,
    bool SetAsDefault = false
);

public record SetDefaultPaymentMethodRequest(string PaymentMethodId);

public record InvoiceDto(
    string Id,
    string InvoiceNumber,
    string Status,
    decimal Subtotal,
    decimal Tax,
    decimal Discount,
    decimal Total,
    decimal AmountPaid,
    decimal AmountDue,
    string Currency,
    DateTime? DueDate,
    DateTime? PaidAt,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    List<InvoiceItemDto>? Items,
    string? StripeInvoiceUrl,
    string? StripeInvoicePdf,
    DateTime CreatedAt
);

public record InvoiceItemDto(
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal Amount
);

public record InvoiceListRequest(
    int Page = 1,
    int PageSize = 20,
    string? Status = "",
    DateTime? From = null,
    DateTime? To = null
);

public record PaymentDto(
    string Id,
    string? InvoiceId,
    decimal Amount,
    string Currency,
    string Status,
    string? FailureReason,
    string? PaymentMethodLast4,
    string? PaymentMethodBrand,
    DateTime CreatedAt
);

public record PaymentListRequest(
    int Page = 1,
    int PageSize = 20,
    string? Status="",
    DateTime? From=null,
    DateTime? To = null
);

public record CreatePaymentRequest(
    string InvoiceId,
    string? PaymentMethodId = null
);

public record RefundPaymentRequest(
    decimal? Amount = null,
    string? Reason = null
);

public record CouponDto(
    string Id,
    string Code,
    string? Description,
    string DiscountType,
    decimal DiscountValue,
    string? Currency,
    DateTime? ValidFrom,
    DateTime? ValidUntil,
    bool IsActive
);

public record ValidateCouponRequest(string Code);

public record ValidateCouponResponse(
    bool IsValid,
    CouponDto? Coupon,
    string? Message
);

public record ApplyCouponRequest(
    string Code,
    string? SubscriptionId = null
);
