using ChatApp.API.Models.DTOs;

namespace ChatApp.API.Services.Interfaces;

public interface IPaymentService
{
    // Payment Methods
    Task<List<PaymentMethodDto>> GetPaymentMethodsAsync(string siteId);
    Task<PaymentMethodDto> AddPaymentMethodAsync(string siteId, AddPaymentMethodRequest request);
    Task SetDefaultPaymentMethodAsync(string siteId, SetDefaultPaymentMethodRequest request);
    Task DeletePaymentMethodAsync(string siteId, string paymentMethodId);

    // Invoices
    Task<PagedResponse<InvoiceDto>> GetInvoicesAsync(string siteId, InvoiceListRequest request);
    Task<InvoiceDto?> GetInvoiceAsync(string invoiceId);

    // Payments
    Task<PagedResponse<PaymentDto>> GetPaymentsAsync(string siteId, PaymentListRequest request);
    Task<PaymentDto> CreatePaymentAsync(string siteId, CreatePaymentRequest request);
    Task<PaymentDto> RefundPaymentAsync(string paymentId, RefundPaymentRequest request);

    // Coupons
    Task<ValidateCouponResponse> ValidateCouponAsync(string code);
    Task<CouponDto> ApplyCouponAsync(string siteId, ApplyCouponRequest request);

    // Razorpay
    Task<RazorpayOrderResponse> CreateRazorpayOrderAsync(string siteId, decimal amount, string currency, string planId, string billingCycle);
    Task<RazorpayOrderResponse> CreateRazorpayRegistrationOrderAsync(string paymentReference, decimal amount, string currency, string planId, string billingCycle);
    Task<PaymentVerificationResult> VerifyRazorpayPaymentAsync(RazorpayPaymentVerification verification);

    // PayPal
    Task<PayPalOrderResponse> CreatePayPalOrderAsync(string siteId, decimal amount, string currency, string planId, string billingCycle, string returnUrl);
    Task<PayPalOrderResponse> CreatePayPalRegistrationOrderAsync(string paymentReference, decimal amount, string currency, string planId, string billingCycle, string returnUrl);
    Task<PaymentVerificationResult> CapturePayPalPaymentAsync(string orderId, string siteId, string planId, string billingCycle);
}

// Razorpay DTOs
public record RazorpayOrderResponse(
    string OrderId,
    decimal Amount,
    string Currency,
    string KeyId,
    string PlanId,
    string SiteId
);

public record RazorpayPaymentVerification(
    string RazorpayOrderId,
    string RazorpayPaymentId,
    string RazorpaySignature,
    string SiteId,
    string PlanId,
    string BillingCycle
);

// PayPal DTOs
public record PayPalOrderResponse(
    string OrderId,
    string ApprovalUrl,
    decimal Amount,
    string Currency,
    string PlanId,
    string SiteId
);

public record PaymentVerificationResult(
    bool Success,
    string? PaymentId,
    string? Message,
    string? TransactionId
);
