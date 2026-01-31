using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChatApp.API.Data;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Models.Entities;
using ChatApp.API.Services.Interfaces;

namespace ChatApp.API.Controllers;

[ApiController]
[Route("api/sites/{siteId}/[controller]")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    // Payment Methods
    [HttpGet("methods")]
    public async Task<ActionResult<ApiResponse<List<PaymentMethodDto>>>> GetPaymentMethods(string siteId)
    {
        var methods = await _paymentService.GetPaymentMethodsAsync(siteId);
        return Ok(ApiResponse<List<PaymentMethodDto>>.Ok(methods));
    }

    [HttpPost("methods")]
    public async Task<ActionResult<ApiResponse<PaymentMethodDto>>> AddPaymentMethod(
        string siteId,
        [FromBody] AddPaymentMethodRequest request)
    {
        var method = await _paymentService.AddPaymentMethodAsync(siteId, request);
        return Ok(ApiResponse<PaymentMethodDto>.Ok(method, "Payment method added"));
    }

    [HttpPut("methods/default")]
    public async Task<ActionResult<ApiResponse<object>>> SetDefaultPaymentMethod(
        string siteId,
        [FromBody] SetDefaultPaymentMethodRequest request)
    {
        await _paymentService.SetDefaultPaymentMethodAsync(siteId, request);
        return Ok(ApiResponse<object>.Ok(null, "Default payment method updated"));
    }

    [HttpDelete("methods/{paymentMethodId}")]
    public async Task<ActionResult<ApiResponse<object>>> DeletePaymentMethod(string siteId, string paymentMethodId)
    {
        try
        {
            await _paymentService.DeletePaymentMethodAsync(siteId, paymentMethodId);
            return Ok(ApiResponse<object>.Ok(null, "Payment method deleted"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // Invoices
    [HttpGet("invoices")]
    public async Task<ActionResult<ApiResponse<PagedResponse<InvoiceDto>>>> GetInvoices(
        string siteId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var request = new InvoiceListRequest(page, pageSize, status, from, to);
        var result = await _paymentService.GetInvoicesAsync(siteId, request);
        return Ok(ApiResponse<PagedResponse<InvoiceDto>>.Ok(result));
    }

    [HttpGet("invoices/{invoiceId}")]
    public async Task<ActionResult<ApiResponse<InvoiceDto>>> GetInvoice(string siteId, string invoiceId)
    {
        var invoice = await _paymentService.GetInvoiceAsync(invoiceId);
        if (invoice == null)
        {
            return NotFound(ApiResponse<InvoiceDto>.Fail("Invoice not found"));
        }

        return Ok(ApiResponse<InvoiceDto>.Ok(invoice));
    }

    /// <summary>
    /// Download invoice as HTML (can be printed as PDF)
    /// </summary>
    [HttpGet("invoices/{invoiceId}/download")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadInvoice(string siteId, string invoiceId)
    {
        var invoice = await _paymentService.GetInvoiceAsync(invoiceId);
        if (invoice == null)
        {
            return NotFound("Invoice not found");
        }

        // If Stripe PDF exists, redirect to it
        if (!string.IsNullOrEmpty(invoice.StripeInvoicePdf))
        {
            return Redirect(invoice.StripeInvoicePdf);
        }

        // Generate HTML invoice
        var html = GenerateInvoiceHtml(invoice, siteId);
        return Content(html, "text/html");
    }

    private string GenerateInvoiceHtml(InvoiceDto invoice, string siteId)
    {
        var itemsHtml = "";
        if (invoice.Items != null && invoice.Items.Any())
        {
            foreach (var item in invoice.Items)
            {
                itemsHtml += $@"
                <tr>
                    <td style='padding: 12px; border-bottom: 1px solid #e5e7eb;'>{item.Description}</td>
                    <td style='padding: 12px; border-bottom: 1px solid #e5e7eb; text-align: center;'>{item.Quantity}</td>
                    <td style='padding: 12px; border-bottom: 1px solid #e5e7eb; text-align: right;'>{invoice.Currency} {item.UnitPrice:N2}</td>
                    <td style='padding: 12px; border-bottom: 1px solid #e5e7eb; text-align: right;'>{invoice.Currency} {item.Amount:N2}</td>
                </tr>";
            }
        }
        else
        {
            itemsHtml = $@"
            <tr>
                <td style='padding: 12px; border-bottom: 1px solid #e5e7eb;'>Subscription ({invoice.PeriodStart:MMM dd} - {invoice.PeriodEnd:MMM dd, yyyy})</td>
                <td style='padding: 12px; border-bottom: 1px solid #e5e7eb; text-align: center;'>1</td>
                <td style='padding: 12px; border-bottom: 1px solid #e5e7eb; text-align: right;'>{invoice.Currency} {invoice.Subtotal:N2}</td>
                <td style='padding: 12px; border-bottom: 1px solid #e5e7eb; text-align: right;'>{invoice.Currency} {invoice.Subtotal:N2}</td>
            </tr>";
        }

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <title>Invoice {invoice.InvoiceNumber}</title>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #f3f4f6; padding: 40px; }}
        .invoice {{ max-width: 800px; margin: 0 auto; background: white; border-radius: 12px; box-shadow: 0 4px 6px rgba(0,0,0,0.1); overflow: hidden; }}
        .header {{ background: linear-gradient(135deg, #2563eb, #1d4ed8); color: white; padding: 40px; }}
        .header h1 {{ font-size: 28px; margin-bottom: 8px; }}
        .header p {{ opacity: 0.9; }}
        .content {{ padding: 40px; }}
        .meta {{ display: flex; justify-content: space-between; margin-bottom: 40px; }}
        .meta-block h3 {{ color: #6b7280; font-size: 12px; text-transform: uppercase; margin-bottom: 8px; }}
        .meta-block p {{ color: #111827; font-size: 14px; line-height: 1.6; }}
        .status {{ display: inline-block; padding: 6px 12px; border-radius: 20px; font-size: 12px; font-weight: 600; }}
        .status-paid {{ background: #dcfce7; color: #166534; }}
        .status-open {{ background: #fef3c7; color: #92400e; }}
        .status-void {{ background: #fee2e2; color: #991b1b; }}
        table {{ width: 100%; border-collapse: collapse; margin-bottom: 30px; }}
        th {{ background: #f9fafb; padding: 12px; text-align: left; font-size: 12px; text-transform: uppercase; color: #6b7280; }}
        .totals {{ margin-left: auto; width: 300px; }}
        .totals tr td {{ padding: 8px 0; }}
        .totals tr:last-child {{ border-top: 2px solid #111827; font-weight: 700; font-size: 18px; }}
        .footer {{ text-align: center; padding: 30px; background: #f9fafb; color: #6b7280; font-size: 14px; }}
        @media print {{ body {{ background: white; padding: 0; }} .invoice {{ box-shadow: none; }} }}
    </style>
</head>
<body>
    <div class='invoice'>
        <div class='header'>
            <h1>INVOICE</h1>
            <p>ChatApp - AI-Powered Customer Support</p>
        </div>
        <div class='content'>
            <div class='meta'>
                <div class='meta-block'>
                    <h3>Invoice Number</h3>
                    <p style='font-size: 18px; font-weight: 600;'>{invoice.InvoiceNumber}</p>
                    <p style='margin-top: 8px;'>Date: {invoice.CreatedAt:MMMM dd, yyyy}</p>
                    {(invoice.DueDate.HasValue ? $"<p>Due: {invoice.DueDate:MMMM dd, yyyy}</p>" : "")}
                </div>
                <div class='meta-block' style='text-align: right;'>
                    <h3>Status</h3>
                    <span class='status status-{invoice.Status.ToLower()}'>{invoice.Status.ToUpper()}</span>
                    {(invoice.PaidAt.HasValue ? $"<p style='margin-top: 8px;'>Paid on {invoice.PaidAt:MMMM dd, yyyy}</p>" : "")}
                </div>
            </div>

            <table>
                <thead>
                    <tr>
                        <th>Description</th>
                        <th style='text-align: center;'>Qty</th>
                        <th style='text-align: right;'>Unit Price</th>
                        <th style='text-align: right;'>Amount</th>
                    </tr>
                </thead>
                <tbody>
                    {itemsHtml}
                </tbody>
            </table>

            <table class='totals'>
                <tr>
                    <td>Subtotal</td>
                    <td style='text-align: right;'>{invoice.Currency} {invoice.Subtotal:N2}</td>
                </tr>
                {(invoice.Discount > 0 ? $"<tr><td>Discount</td><td style='text-align: right; color: #059669;'>-{invoice.Currency} {invoice.Discount:N2}</td></tr>" : "")}
                {(invoice.Tax > 0 ? $"<tr><td>Tax</td><td style='text-align: right;'>{invoice.Currency} {invoice.Tax:N2}</td></tr>" : "")}
                <tr>
                    <td>Total</td>
                    <td style='text-align: right;'>{invoice.Currency} {invoice.Total:N2}</td>
                </tr>
            </table>
        </div>
        <div class='footer'>
            <p>Thank you for your business!</p>
            <p style='margin-top: 8px;'>Questions? Contact support@chatapp.com</p>
        </div>
    </div>
    <script>window.onload = function() {{ window.print(); }}</script>
</body>
</html>";
    }

    // Payments
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResponse<PaymentDto>>>> GetPayments(
        string siteId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var request = new PaymentListRequest(page, pageSize, status, from, to);
        var result = await _paymentService.GetPaymentsAsync(siteId, request);
        return Ok(ApiResponse<PagedResponse<PaymentDto>>.Ok(result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> CreatePayment(
        string siteId,
        [FromBody] CreatePaymentRequest request)
    {
        try
        {
            var payment = await _paymentService.CreatePaymentAsync(siteId, request);
            return Ok(ApiResponse<PaymentDto>.Ok(payment, "Payment successful"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<PaymentDto>.Fail(ex.Message));
        }
    }

    [HttpPost("{paymentId}/refund")]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> RefundPayment(
        string siteId,
        string paymentId,
        [FromBody] RefundPaymentRequest request)
    {
        try
        {
            var payment = await _paymentService.RefundPaymentAsync(paymentId, request);
            return Ok(ApiResponse<PaymentDto>.Ok(payment, "Refund processed"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<PaymentDto>.Fail(ex.Message));
        }
    }

    // Coupons
    [HttpGet("coupons/validate")]
    public async Task<ActionResult<ApiResponse<ValidateCouponResponse>>> ValidateCoupon([FromQuery] string code)
    {
        var result = await _paymentService.ValidateCouponAsync(code);
        return Ok(ApiResponse<ValidateCouponResponse>.Ok(result));
    }

    [HttpPost("coupons/apply")]
    public async Task<ActionResult<ApiResponse<CouponDto>>> ApplyCoupon(
        string siteId,
        [FromBody] ApplyCouponRequest request)
    {
        try
        {
            var coupon = await _paymentService.ApplyCouponAsync(siteId, request);
            return Ok(ApiResponse<CouponDto>.Ok(coupon, "Coupon applied"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<CouponDto>.Fail(ex.Message));
        }
    }

    // ==================== RAZORPAY ENDPOINTS ====================

    /// <summary>
    /// Create a Razorpay order for subscription upgrade
    /// </summary>
    [HttpPost("razorpay/create-order")]
    public async Task<ActionResult<ApiResponse<RazorpayOrderResponse>>> CreateRazorpayOrder(
        string siteId,
        [FromBody] CreatePaymentOrderRequest request)
    {
        try
        {
            var order = await _paymentService.CreateRazorpayOrderAsync(
                siteId,
                request.Amount,
                request.Currency ?? "INR",
                request.PlanId,
                request.BillingCycle ?? "monthly"
            );
            return Ok(ApiResponse<RazorpayOrderResponse>.Ok(order));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<RazorpayOrderResponse>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Verify Razorpay payment after customer completes payment
    /// </summary>
    [HttpPost("razorpay/verify")]
    public async Task<ActionResult<ApiResponse<PaymentVerificationResult>>> VerifyRazorpayPayment(
        string siteId,
        [FromBody] RazorpayPaymentVerification verification)
    {
        try
        {
            var result = await _paymentService.VerifyRazorpayPaymentAsync(verification);
            if (result.Success)
            {
                return Ok(ApiResponse<PaymentVerificationResult>.Ok(result, "Payment verified successfully"));
            }
            return BadRequest(ApiResponse<PaymentVerificationResult>.Fail(result.Message ?? "Payment verification failed"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<PaymentVerificationResult>.Fail(ex.Message));
        }
    }

    // ==================== PAYPAL ENDPOINTS ====================

    /// <summary>
    /// Create a PayPal order for subscription upgrade
    /// </summary>
    [HttpPost("paypal/create-order")]
    public async Task<ActionResult<ApiResponse<PayPalOrderResponse>>> CreatePayPalOrder(
        string siteId,
        [FromBody] CreatePaymentOrderRequest request)
    {
        try
        {
            var order = await _paymentService.CreatePayPalOrderAsync(
                siteId,
                request.Amount,
                request.Currency ?? "USD",
                request.PlanId,
                request.BillingCycle ?? "monthly",
                request.ReturnUrl ?? $"{HttpContext.Request.Headers["Origin"]}"
            );
            return Ok(ApiResponse<PayPalOrderResponse>.Ok(order));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<PayPalOrderResponse>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Capture PayPal payment after customer approves
    /// </summary>
    [HttpPost("paypal/capture")]
    public async Task<ActionResult<ApiResponse<PaymentVerificationResult>>> CapturePayPalPayment(
        string siteId,
        [FromBody] PayPalCaptureRequest request)
    {
        try
        {
            var result = await _paymentService.CapturePayPalPaymentAsync(
                request.OrderId,
                siteId,
                request.PlanId,
                request.BillingCycle ?? "monthly"
            );
            if (result.Success)
            {
                return Ok(ApiResponse<PaymentVerificationResult>.Ok(result, "Payment captured successfully"));
            }
            return BadRequest(ApiResponse<PaymentVerificationResult>.Fail(result.Message ?? "Payment capture failed"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<PaymentVerificationResult>.Fail(ex.Message));
        }
    }
}

// ==================== REGISTRATION PAYMENT CONTROLLER ====================

/// <summary>
/// Payment endpoints for registration flow (no auth required, no siteId needed)
/// </summary>
[ApiController]
[Route("api/payments")]
public class RegistrationPaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ApplicationDbContext _context;

    public RegistrationPaymentsController(IPaymentService paymentService, ApplicationDbContext context)
    {
        _paymentService = paymentService;
        _context = context;
    }

    /// <summary>
    /// Create a Razorpay order for registration (before account is created)
    /// </summary>
    [HttpPost("razorpay/create-registration-order")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<RegistrationOrderResponse>>> CreateRegistrationOrder(
        [FromBody] CreateRegistrationOrderRequest request)
    {
        try
        {
            // Generate a unique payment reference for this registration
            var paymentReference = Guid.NewGuid().ToString();

            var order = await _paymentService.CreateRazorpayRegistrationOrderAsync(
                paymentReference,
                request.Amount,
                request.Currency ?? "INR",
                request.PlanId,
                request.BillingCycle ?? "monthly"
            );

            // Return the order with payment reference
            return Ok(ApiResponse<RegistrationOrderResponse>.Ok(new RegistrationOrderResponse(
                order.OrderId,
                order.Amount,
                order.Currency,
                order.KeyId,
                order.PlanId,
                paymentReference
            )));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<RegistrationOrderResponse>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Create a PayPal order for registration (before account is created)
    /// </summary>
    [HttpPost("paypal/create-registration-order")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PayPalRegistrationOrderResponse>>> CreatePayPalRegistrationOrder(
        [FromBody] CreateRegistrationOrderRequest request)
    {
        try
        {
            // Generate a unique payment reference for this registration
            var paymentReference = Guid.NewGuid().ToString();

            var order = await _paymentService.CreatePayPalRegistrationOrderAsync(
                paymentReference,
                request.Amount,
                request.Currency ?? "USD",
                request.PlanId,
                request.BillingCycle ?? "monthly",
                request.ReturnUrl ?? $"{HttpContext.Request.Headers["Origin"]}/register.html"
            );

            // Return the order with payment reference
            return Ok(ApiResponse<PayPalRegistrationOrderResponse>.Ok(new PayPalRegistrationOrderResponse(
                order.OrderId,
                order.ApprovalUrl,
                paymentReference
            )));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<PayPalRegistrationOrderResponse>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Log payment events (failures, cancellations) from frontend
    /// </summary>
    [HttpPost("log-event")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> LogPaymentEvent(
        [FromBody] LogPaymentEventRequest request)
    {
        var log = new PaymentLog
        {
            SiteId = request.SiteId,
            Action = request.Action, // payment_failed, payment_cancelled, payment_dismissed
            Gateway = request.Gateway,
            Status = request.Status, // failed, cancelled
            OrderId = request.OrderId,
            TransactionId = request.TransactionId,
            Amount = request.Amount,
            Currency = request.Currency,
            ErrorMessage = request.ErrorMessage,
            ErrorCode = request.ErrorCode,
            RequestData = request.RequestData,
            Metadata = request.Metadata
        };

        _context.PaymentLogs.Add(log);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(null, "Payment event logged"));
    }
}

public record CreateRegistrationOrderRequest(
    decimal Amount,
    string PlanId,
    string Email,
    string? Currency = null,
    string? BillingCycle = null,
    string? ReturnUrl = null
);

public record RegistrationOrderResponse(
    string OrderId,
    decimal Amount,
    string Currency,
    string KeyId,
    string PlanId,
    string PaymentReference
);

public record PayPalRegistrationOrderResponse(
    string OrderId,
    string ApprovalUrl,
    string PaymentReference
);

public record LogPaymentEventRequest(
    string Action,        // payment_failed, payment_cancelled, payment_dismissed
    string Gateway,       // razorpay, paypal
    string Status,        // failed, cancelled
    string? SiteId = null,
    string? OrderId = null,
    string? TransactionId = null,
    decimal? Amount = null,
    string? Currency = null,
    string? ErrorMessage = null,
    string? ErrorCode = null,
    string? RequestData = null,
    string? Metadata = null
);

// Request DTOs for payment endpoints
public record CreatePaymentOrderRequest(
    decimal Amount,
    string PlanId,
    string? Currency = null,
    string? BillingCycle = null,
    string? ReturnUrl = null
);

public record PayPalCaptureRequest(
    string OrderId,
    string PlanId,
    string? BillingCycle = null
);

// ==================== ADMIN PAYMENTS CONTROLLER ====================

/// <summary>
/// Admin endpoints for viewing all payments and invoices (super_admin only)
/// </summary>
[ApiController]
[Route("api/admin/payments")]
[Authorize(Roles = "super_admin")]
public class AdminPaymentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AdminPaymentsController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get all payments across all sites
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResponse<AdminPaymentDto>>>> GetAllPayments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? siteId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var query = _context.Payments
            .Include(p => p.Site)
            .Include(p => p.PaymentMethod)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(p => p.Status == status);

        if (!string.IsNullOrEmpty(siteId))
            query = query.Where(p => p.SiteId == siteId);

        if (from.HasValue)
            query = query.Where(p => p.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(p => p.CreatedAt <= to.Value);

        var total = await query.CountAsync();

        var payments = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new AdminPaymentDto(
                p.Id,
                p.SiteId,
                p.Site.Name,
                p.InvoiceId,
                p.Amount,
                p.Currency,
                p.Status,
                p.FailureReason,
                p.PaymentMethod != null ? p.PaymentMethod.Last4 : null,
                p.PaymentMethod != null ? p.PaymentMethod.Brand : null,
                p.StripePaymentIntentId,
                p.StripeChargeId,
                p.CreatedAt
            ))
            .ToListAsync();

        var totalPages = (int)Math.Ceiling((double)total / pageSize);
        return Ok(ApiResponse<PagedResponse<AdminPaymentDto>>.Ok(
            new PagedResponse<AdminPaymentDto>(payments, page, pageSize, total, totalPages)));
    }

    /// <summary>
    /// Get all invoices across all sites
    /// </summary>
    [HttpGet("invoices")]
    public async Task<ActionResult<ApiResponse<PagedResponse<AdminInvoiceDto>>>> GetAllInvoices(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? siteId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var query = _context.Invoices
            .Include(i => i.Site)
            .Include(i => i.Items)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(i => i.Status == status);

        if (!string.IsNullOrEmpty(siteId))
            query = query.Where(i => i.SiteId == siteId);

        if (from.HasValue)
            query = query.Where(i => i.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(i => i.CreatedAt <= to.Value);

        var total = await query.CountAsync();

        var invoices = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new AdminInvoiceDto(
                i.Id,
                i.SiteId,
                i.Site.Name,
                i.InvoiceNumber,
                i.Status,
                i.Subtotal,
                i.Tax,
                i.Discount,
                i.Total,
                i.AmountPaid,
                i.AmountDue,
                i.Currency,
                i.DueDate,
                i.PaidAt,
                i.PeriodStart,
                i.PeriodEnd,
                i.StripeInvoiceUrl,
                i.StripeInvoicePdf,
                i.CreatedAt
            ))
            .ToListAsync();

        var totalPages = (int)Math.Ceiling((double)total / pageSize);
        return Ok(ApiResponse<PagedResponse<AdminInvoiceDto>>.Ok(
            new PagedResponse<AdminInvoiceDto>(invoices, page, pageSize, total, totalPages)));
    }

    /// <summary>
    /// Download any invoice (admin access)
    /// </summary>
    [HttpGet("invoices/{invoiceId}/download")]
    public async Task<IActionResult> AdminDownloadInvoice(string invoiceId)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Site)
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);

        if (invoice == null)
        {
            return NotFound("Invoice not found");
        }

        // If Stripe PDF exists, redirect to it
        if (!string.IsNullOrEmpty(invoice.StripeInvoicePdf))
        {
            return Redirect(invoice.StripeInvoicePdf);
        }

        // Generate HTML invoice
        var html = GenerateAdminInvoiceHtml(invoice);
        return Content(html, "text/html");
    }

    private string GenerateAdminInvoiceHtml(Invoice invoice)
    {
        var itemsHtml = "";
        if (invoice.Items != null && invoice.Items.Any())
        {
            foreach (var item in invoice.Items)
            {
                itemsHtml += $@"
                <tr>
                    <td style='padding: 12px; border-bottom: 1px solid #e5e7eb;'>{item.Description}</td>
                    <td style='padding: 12px; border-bottom: 1px solid #e5e7eb; text-align: center;'>{item.Quantity}</td>
                    <td style='padding: 12px; border-bottom: 1px solid #e5e7eb; text-align: right;'>{invoice.Currency} {item.UnitPrice:N2}</td>
                    <td style='padding: 12px; border-bottom: 1px solid #e5e7eb; text-align: right;'>{invoice.Currency} {item.Amount:N2}</td>
                </tr>";
            }
        }
        else
        {
            itemsHtml = $@"
            <tr>
                <td style='padding: 12px; border-bottom: 1px solid #e5e7eb;'>Subscription ({invoice.PeriodStart:MMM dd} - {invoice.PeriodEnd:MMM dd, yyyy})</td>
                <td style='padding: 12px; border-bottom: 1px solid #e5e7eb; text-align: center;'>1</td>
                <td style='padding: 12px; border-bottom: 1px solid #e5e7eb; text-align: right;'>{invoice.Currency} {invoice.Subtotal:N2}</td>
                <td style='padding: 12px; border-bottom: 1px solid #e5e7eb; text-align: right;'>{invoice.Currency} {invoice.Subtotal:N2}</td>
            </tr>";
        }

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <title>Invoice {invoice.InvoiceNumber}</title>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #f3f4f6; padding: 40px; }}
        .invoice {{ max-width: 800px; margin: 0 auto; background: white; border-radius: 12px; box-shadow: 0 4px 6px rgba(0,0,0,0.1); overflow: hidden; }}
        .header {{ background: linear-gradient(135deg, #2563eb, #1d4ed8); color: white; padding: 40px; }}
        .header h1 {{ font-size: 28px; margin-bottom: 8px; }}
        .header p {{ opacity: 0.9; }}
        .content {{ padding: 40px; }}
        .meta {{ display: flex; justify-content: space-between; margin-bottom: 40px; }}
        .meta-block h3 {{ color: #6b7280; font-size: 12px; text-transform: uppercase; margin-bottom: 8px; }}
        .meta-block p {{ color: #111827; font-size: 14px; line-height: 1.6; }}
        .status {{ display: inline-block; padding: 6px 12px; border-radius: 20px; font-size: 12px; font-weight: 600; }}
        .status-paid {{ background: #dcfce7; color: #166534; }}
        .status-open {{ background: #fef3c7; color: #92400e; }}
        .status-void {{ background: #fee2e2; color: #991b1b; }}
        table {{ width: 100%; border-collapse: collapse; margin-bottom: 30px; }}
        th {{ background: #f9fafb; padding: 12px; text-align: left; font-size: 12px; text-transform: uppercase; color: #6b7280; }}
        .totals {{ margin-left: auto; width: 300px; }}
        .totals tr td {{ padding: 8px 0; }}
        .totals tr:last-child {{ border-top: 2px solid #111827; font-weight: 700; font-size: 18px; }}
        .footer {{ text-align: center; padding: 30px; background: #f9fafb; color: #6b7280; font-size: 14px; }}
        @media print {{ body {{ background: white; padding: 0; }} .invoice {{ box-shadow: none; }} }}
    </style>
</head>
<body>
    <div class='invoice'>
        <div class='header'>
            <h1>INVOICE</h1>
            <p>ChatApp - AI-Powered Customer Support</p>
        </div>
        <div class='content'>
            <div class='meta'>
                <div class='meta-block'>
                    <h3>Invoice Number</h3>
                    <p style='font-size: 18px; font-weight: 600;'>{invoice.InvoiceNumber}</p>
                    <p style='margin-top: 8px;'>Date: {invoice.CreatedAt:MMMM dd, yyyy}</p>
                    {(invoice.DueDate.HasValue ? $"<p>Due: {invoice.DueDate:MMMM dd, yyyy}</p>" : "")}
                </div>
                <div class='meta-block' style='text-align: right;'>
                    <h3>Status</h3>
                    <span class='status status-{invoice.Status.ToLower()}'>{invoice.Status.ToUpper()}</span>
                    {(invoice.PaidAt.HasValue ? $"<p style='margin-top: 8px;'>Paid on {invoice.PaidAt:MMMM dd, yyyy}</p>" : "")}
                </div>
            </div>
            <div class='meta-block' style='margin-bottom: 30px;'>
                <h3>Bill To</h3>
                <p style='font-weight: 600;'>{invoice.Site?.Name ?? "N/A"}</p>
            </div>

            <table>
                <thead>
                    <tr>
                        <th>Description</th>
                        <th style='text-align: center;'>Qty</th>
                        <th style='text-align: right;'>Unit Price</th>
                        <th style='text-align: right;'>Amount</th>
                    </tr>
                </thead>
                <tbody>
                    {itemsHtml}
                </tbody>
            </table>

            <table class='totals'>
                <tr>
                    <td>Subtotal</td>
                    <td style='text-align: right;'>{invoice.Currency} {invoice.Subtotal:N2}</td>
                </tr>
                {(invoice.Discount > 0 ? $"<tr><td>Discount</td><td style='text-align: right; color: #059669;'>-{invoice.Currency} {invoice.Discount:N2}</td></tr>" : "")}
                {(invoice.Tax > 0 ? $"<tr><td>Tax</td><td style='text-align: right;'>{invoice.Currency} {invoice.Tax:N2}</td></tr>" : "")}
                <tr>
                    <td>Total</td>
                    <td style='text-align: right;'>{invoice.Currency} {invoice.Total:N2}</td>
                </tr>
            </table>
        </div>
        <div class='footer'>
            <p>Thank you for your business!</p>
            <p style='margin-top: 8px;'>Questions? Contact support@chatapp.com</p>
        </div>
    </div>
    <script>window.onload = function() {{ window.print(); }}</script>
</body>
</html>";
    }

    /// <summary>
    /// Get payment logs for debugging and monitoring
    /// </summary>
    [HttpGet("logs")]
    public async Task<ActionResult<ApiResponse<PagedResponse<PaymentLogDto>>>> GetPaymentLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? gateway = null,
        [FromQuery] string? status = null,
        [FromQuery] string? action = null,
        [FromQuery] string? siteId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var query = _context.PaymentLogs.AsQueryable();

        if (!string.IsNullOrEmpty(gateway))
            query = query.Where(l => l.Gateway == gateway);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(l => l.Status == status);

        if (!string.IsNullOrEmpty(action))
            query = query.Where(l => l.Action == action);

        if (!string.IsNullOrEmpty(siteId))
            query = query.Where(l => l.SiteId == siteId);

        if (from.HasValue)
            query = query.Where(l => l.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(l => l.CreatedAt <= to.Value);

        var total = await query.CountAsync();

        var logs = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new PaymentLogDto(
                l.Id,
                l.SiteId,
                l.Action,
                l.Gateway,
                l.Status,
                l.OrderId,
                l.TransactionId,
                l.Amount,
                l.Currency,
                l.ErrorMessage,
                l.ErrorCode,
                l.RequestData,
                l.ResponseData,
                l.Metadata,
                l.DurationMs,
                l.CreatedAt
            ))
            .ToListAsync();

        var totalPages = (int)Math.Ceiling((double)total / pageSize);
        return Ok(ApiResponse<PagedResponse<PaymentLogDto>>.Ok(
            new PagedResponse<PaymentLogDto>(logs, page, pageSize, total, totalPages)));
    }

    /// <summary>
    /// Get payment statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<ApiResponse<PaymentStatsDto>>> GetPaymentStats()
    {
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1);
        var startOfLastMonth = startOfMonth.AddMonths(-1);

        var totalRevenue = await _context.Payments
            .Where(p => p.Status == "succeeded")
            .SumAsync(p => p.Amount);

        var monthlyRevenue = await _context.Payments
            .Where(p => p.Status == "succeeded" && p.CreatedAt >= startOfMonth)
            .SumAsync(p => p.Amount);

        var lastMonthRevenue = await _context.Payments
            .Where(p => p.Status == "succeeded" && p.CreatedAt >= startOfLastMonth && p.CreatedAt < startOfMonth)
            .SumAsync(p => p.Amount);

        var totalPayments = await _context.Payments.CountAsync();
        var successfulPayments = await _context.Payments.CountAsync(p => p.Status == "succeeded");
        var failedPayments = await _context.Payments.CountAsync(p => p.Status == "failed");
        var pendingPayments = await _context.Payments.CountAsync(p => p.Status == "pending");

        var totalInvoices = await _context.Invoices.CountAsync();
        var paidInvoices = await _context.Invoices.CountAsync(i => i.Status == "paid");
        var unpaidInvoices = await _context.Invoices.CountAsync(i => i.Status == "open");

        // Orphaned payments (successful payments where account creation failed)
        var orphanedPayments = await _context.PaymentLogs.CountAsync(l => l.Action == "orphaned_payment");

        return Ok(ApiResponse<PaymentStatsDto>.Ok(new PaymentStatsDto(
            totalRevenue,
            monthlyRevenue,
            lastMonthRevenue,
            totalPayments,
            successfulPayments,
            failedPayments,
            pendingPayments,
            totalInvoices,
            paidInvoices,
            unpaidInvoices,
            orphanedPayments
        )));
    }

    /// <summary>
    /// Get orphaned payments - successful payments where account creation failed
    /// </summary>
    [HttpGet("orphaned")]
    public async Task<ActionResult<ApiResponse<PagedResponse<OrphanedPaymentDto>>>> GetOrphanedPayments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var query = _context.PaymentLogs
            .Where(l => l.Action == "orphaned_payment")
            .AsQueryable();

        if (from.HasValue)
            query = query.Where(l => l.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(l => l.CreatedAt <= to.Value);

        var total = await query.CountAsync();

        var logs = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var orphanedPayments = logs.Select(l => {
            // Parse metadata JSON
            string? email = null, username = null, domain = null, planId = null, planName = null, billingCycle = null, paymentReference = null;

            if (!string.IsNullOrEmpty(l.Metadata))
            {
                try
                {
                    var metadata = System.Text.Json.JsonDocument.Parse(l.Metadata);
                    email = metadata.RootElement.TryGetProperty("email", out var e) ? e.GetString() : null;
                    username = metadata.RootElement.TryGetProperty("username", out var u) ? u.GetString() : null;
                    domain = metadata.RootElement.TryGetProperty("domain", out var d) ? d.GetString() : null;
                    planId = metadata.RootElement.TryGetProperty("planId", out var pi) ? pi.GetString() : null;
                    planName = metadata.RootElement.TryGetProperty("planName", out var pn) ? pn.GetString() : null;
                    billingCycle = metadata.RootElement.TryGetProperty("billingCycle", out var bc) ? bc.GetString() : null;
                    paymentReference = metadata.RootElement.TryGetProperty("paymentReference", out var pr) ? pr.GetString() : null;
                }
                catch { }
            }

            return new OrphanedPaymentDto(
                l.Id,
                l.Gateway,
                l.OrderId,
                l.TransactionId,
                l.Amount,
                l.Currency,
                email,
                username,
                domain,
                planId,
                planName,
                billingCycle,
                paymentReference,
                l.ErrorMessage,
                l.CreatedAt
            );
        }).ToList();

        var totalPages = (int)Math.Ceiling((double)total / pageSize);
        return Ok(ApiResponse<PagedResponse<OrphanedPaymentDto>>.Ok(
            new PagedResponse<OrphanedPaymentDto>(orphanedPayments, page, pageSize, total, totalPages)));
    }
}

// Admin Payment DTOs
public record AdminPaymentDto(
    string Id,
    string SiteId,
    string SiteName,
    string? InvoiceId,
    decimal Amount,
    string Currency,
    string Status,
    string? FailureReason,
    string? PaymentMethodLast4,
    string? PaymentMethodBrand,
    string? StripePaymentIntentId,
    string? RazorpayPaymentId,
    DateTime CreatedAt
);

public record AdminInvoiceDto(
    string Id,
    string SiteId,
    string SiteName,
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
    string? InvoiceUrl,
    string? InvoicePdf,
    DateTime CreatedAt
);

public record PaymentStatsDto(
    decimal TotalRevenue,
    decimal MonthlyRevenue,
    decimal LastMonthRevenue,
    int TotalPayments,
    int SuccessfulPayments,
    int FailedPayments,
    int PendingPayments,
    int TotalInvoices,
    int PaidInvoices,
    int UnpaidInvoices,
    int OrphanedPayments
);

public record PaymentLogDto(
    int Id,
    string? SiteId,
    string Action,
    string Gateway,
    string Status,
    string? OrderId,
    string? TransactionId,
    decimal? Amount,
    string? Currency,
    string? ErrorMessage,
    string? ErrorCode,
    string? RequestData,
    string? ResponseData,
    string? Metadata,
    int? DurationMs,
    DateTime CreatedAt
);

public record OrphanedPaymentDto(
    int Id,
    string Gateway,
    string? OrderId,
    string? TransactionId,
    decimal? Amount,
    string? Currency,
    string? Email,
    string? Username,
    string? Domain,
    string? PlanId,
    string? PlanName,
    string? BillingCycle,
    string? PaymentReference,
    string? ErrorMessage,
    DateTime CreatedAt
);
