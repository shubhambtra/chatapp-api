using Microsoft.EntityFrameworkCore;
using ChatApp.API.Data;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Models.Entities;
using ChatApp.API.Services.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Diagnostics;

namespace ChatApp.API.Services.Implementations;

public class PaymentService : IPaymentService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IEmailService _emailService;
    private readonly ILogger<PaymentService> _logger;
    private readonly IErrorLogService _errorLogService;

    public PaymentService(
        ApplicationDbContext context,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ISubscriptionService subscriptionService,
        IEmailService emailService,
        ILogger<PaymentService> logger,
        IErrorLogService errorLogService)
    {
        _context = context;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _subscriptionService = subscriptionService;
        _emailService = emailService;
        _logger = logger;
        _errorLogService = errorLogService;
    }

    // ==================== DB CONFIG OVERRIDE HELPERS ====================

    private AppConfiguration? _cachedAppConfig;
    private bool _appConfigLoaded;

    private async Task<AppConfiguration?> GetAppConfigAsync()
    {
        if (!_appConfigLoaded)
        {
            _cachedAppConfig = await _context.AppConfigurations.FirstOrDefaultAsync();
            _appConfigLoaded = true;
        }
        return _cachedAppConfig;
    }

    /// <summary>
    /// Returns (KeyId, KeySecret) for Razorpay — DB first when IsActive, else appsettings.json
    /// </summary>
    private async Task<(string? KeyId, string? KeySecret)> GetRazorpayCredentialsAsync()
    {
        var appConfig = await GetAppConfigAsync();
        if (appConfig is { IsActive: true })
        {
            var dbKeyId = appConfig.RazorpayKeyId;
            var dbKeySecret = appConfig.RazorpayKeySecret;
            if (!string.IsNullOrWhiteSpace(dbKeyId) && !string.IsNullOrWhiteSpace(dbKeySecret))
            {
                return (dbKeyId, dbKeySecret);
            }
        }
        return (_configuration["Razorpay:KeyId"], _configuration["Razorpay:KeySecret"]);
    }

    /// <summary>
    /// Returns (ClientId, ClientSecret, Mode) for PayPal — DB first when IsActive, else appsettings.json
    /// </summary>
    private async Task<(string? ClientId, string? ClientSecret, string Mode)> GetPayPalCredentialsAsync()
    {
        var appConfig = await GetAppConfigAsync();
        if (appConfig is { IsActive: true })
        {
            var dbClientId = appConfig.PayPalClientId;
            var dbClientSecret = appConfig.PayPalClientSecret;
            if (!string.IsNullOrWhiteSpace(dbClientId) && !string.IsNullOrWhiteSpace(dbClientSecret))
            {
                return (dbClientId, dbClientSecret, appConfig.PayPalMode ?? "sandbox");
            }
        }
        return (_configuration["PayPal:ClientId"], _configuration["PayPal:ClientSecret"], _configuration["PayPal:Mode"] ?? "sandbox");
    }

    private async Task LogPaymentAction(PaymentLog log)
    {
        try
        {
            _context.PaymentLogs.Add(log);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save payment log: {Action} {Gateway}", log.Action, log.Gateway);
            await _errorLogService.LogErrorAsync(ex, null, "Warning");
        }
    }

    public async Task<List<PaymentMethodDto>> GetPaymentMethodsAsync(string siteId)
    {
        var methods = await _context.PaymentMethods
            .Where(pm => pm.SiteId == siteId)
            .OrderByDescending(pm => pm.IsDefault)
            .ThenByDescending(pm => pm.CreatedAt)
            .ToListAsync();

        return methods.Select(MapPaymentMethodToDto).ToList();
    }

    public async Task<PaymentMethodDto> AddPaymentMethodAsync(string siteId, AddPaymentMethodRequest request)
    {
        // In production, this would interact with Stripe to create a payment method
        // For now, we'll create a placeholder
        var paymentMethod = new PaymentMethod
        {
            SiteId = siteId,
            Type = "card",
            Last4 = "4242", // Placeholder
            Brand = "Visa", // Placeholder
            ExpMonth = 12,
            ExpYear = DateTime.UtcNow.Year + 2,
            IsDefault = request.SetAsDefault,
            StripePaymentMethodId = request.PaymentMethodToken
        };

        if (request.SetAsDefault)
        {
            // Unset other default methods
            var existingDefaults = await _context.PaymentMethods
                .Where(pm => pm.SiteId == siteId && pm.IsDefault)
                .ToListAsync();

            foreach (var method in existingDefaults)
            {
                method.IsDefault = false;
            }
        }

        _context.PaymentMethods.Add(paymentMethod);
        await _context.SaveChangesAsync();

        return MapPaymentMethodToDto(paymentMethod);
    }

    public async Task SetDefaultPaymentMethodAsync(string siteId, SetDefaultPaymentMethodRequest request)
    {
        var methods = await _context.PaymentMethods
            .Where(pm => pm.SiteId == siteId)
            .ToListAsync();

        foreach (var method in methods)
        {
            method.IsDefault = method.Id == request.PaymentMethodId;
        }

        await _context.SaveChangesAsync();
    }

    public async Task DeletePaymentMethodAsync(string siteId, string paymentMethodId)
    {
        var method = await _context.PaymentMethods
            .FirstOrDefaultAsync(pm => pm.Id == paymentMethodId && pm.SiteId == siteId);

        if (method == null) throw new KeyNotFoundException("Payment method not found");

        _context.PaymentMethods.Remove(method);
        await _context.SaveChangesAsync();
    }

    public async Task<PagedResponse<InvoiceDto>> GetInvoicesAsync(string siteId, InvoiceListRequest request)
    {
        var query = _context.Invoices
            .Include(i => i.Items)
            .Where(i => i.SiteId == siteId);

        if (!string.IsNullOrEmpty(request.Status))
        {
            query = query.Where(i => i.Status == request.Status);
        }

        if (request.From.HasValue)
        {
            query = query.Where(i => i.CreatedAt >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            query = query.Where(i => i.CreatedAt <= request.To.Value);
        }

        var totalItems = await query.CountAsync();
        var invoices = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResponse<InvoiceDto>(
            invoices.Select(MapInvoiceToDto).ToList(),
            request.Page,
            request.PageSize,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)request.PageSize)
        );
    }

    public async Task<InvoiceDto?> GetInvoiceAsync(string invoiceId)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);

        return invoice != null ? MapInvoiceToDto(invoice) : null;
    }

    public async Task<PagedResponse<PaymentDto>> GetPaymentsAsync(string siteId, PaymentListRequest request)
    {
        var query = _context.Payments
            .Include(p => p.PaymentMethod)
            .Where(p => p.SiteId == siteId);

        if (!string.IsNullOrEmpty(request.Status))
        {
            query = query.Where(p => p.Status == request.Status);
        }

        if (request.From.HasValue)
        {
            query = query.Where(p => p.CreatedAt >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            query = query.Where(p => p.CreatedAt <= request.To.Value);
        }

        var totalItems = await query.CountAsync();
        var payments = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResponse<PaymentDto>(
            payments.Select(MapPaymentToDto).ToList(),
            request.Page,
            request.PageSize,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)request.PageSize)
        );
    }

    public async Task<PaymentDto> CreatePaymentAsync(string siteId, CreatePaymentRequest request)
    {
        var invoice = await _context.Invoices.FindAsync(request.InvoiceId);
        if (invoice == null) throw new KeyNotFoundException("Invoice not found");

        var paymentMethodId = request.PaymentMethodId;
        if (string.IsNullOrEmpty(paymentMethodId))
        {
            var defaultMethod = await _context.PaymentMethods
                .FirstOrDefaultAsync(pm => pm.SiteId == siteId && pm.IsDefault);
            paymentMethodId = defaultMethod?.Id;
        }

        var payment = new Payment
        {
            SiteId = siteId,
            InvoiceId = request.InvoiceId,
            PaymentMethodId = paymentMethodId,
            Amount = invoice.AmountDue,
            Currency = invoice.Currency,
            Status = "succeeded" // In production, this would come from Stripe
        };

        _context.Payments.Add(payment);

        // Update invoice
        invoice.AmountPaid += payment.Amount;
        invoice.AmountDue -= payment.Amount;
        if (invoice.AmountDue <= 0)
        {
            invoice.Status = "paid";
            invoice.PaidAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return MapPaymentToDto(payment);
    }

    public async Task<PaymentDto> RefundPaymentAsync(string paymentId, RefundPaymentRequest request)
    {
        var payment = await _context.Payments
            .Include(p => p.PaymentMethod)
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment == null) throw new KeyNotFoundException("Payment not found");

        var refundAmount = request.Amount ?? payment.Amount;

        var refund = new PaymentRefund
        {
            PaymentId = paymentId,
            Amount = refundAmount,
            Reason = request.Reason,
            Status = "succeeded"
        };

        _context.PaymentRefunds.Add(refund);

        // Update payment status
        var totalRefunded = await _context.PaymentRefunds
            .Where(r => r.PaymentId == paymentId && r.Status == "succeeded")
            .SumAsync(r => r.Amount);

        payment.Status = totalRefunded >= payment.Amount ? "refunded" : "partially_refunded";

        await _context.SaveChangesAsync();

        return MapPaymentToDto(payment);
    }

    public async Task<ValidateCouponResponse> ValidateCouponAsync(string code)
    {
        var coupon = await _context.Coupons
            .FirstOrDefaultAsync(c => c.Code == code && c.IsActive);

        if (coupon == null)
        {
            return new ValidateCouponResponse(false, null, "Coupon not found");
        }

        if (coupon.ValidFrom.HasValue && coupon.ValidFrom > DateTime.UtcNow)
        {
            return new ValidateCouponResponse(false, null, "Coupon is not yet valid");
        }

        if (coupon.ValidUntil.HasValue && coupon.ValidUntil < DateTime.UtcNow)
        {
            return new ValidateCouponResponse(false, null, "Coupon has expired");
        }

        if (coupon.MaxRedemptions.HasValue && coupon.TimesRedeemed >= coupon.MaxRedemptions)
        {
            return new ValidateCouponResponse(false, null, "Coupon has reached maximum redemptions");
        }

        return new ValidateCouponResponse(true, MapCouponToDto(coupon), null);
    }

    public async Task<CouponDto> ApplyCouponAsync(string siteId, ApplyCouponRequest request)
    {
        var validation = await ValidateCouponAsync(request.Code);
        if (!validation.IsValid || validation.Coupon == null)
        {
            throw new InvalidOperationException(validation.Message ?? "Invalid coupon");
        }

        var coupon = await _context.Coupons.FirstOrDefaultAsync(c => c.Code == request.Code);
        if (coupon == null) throw new KeyNotFoundException("Coupon not found");

        var redemption = new CouponRedemption
        {
            CouponId = coupon.Id,
            SiteId = siteId,
            SubscriptionId = request.SubscriptionId,
            DiscountAmount = coupon.DiscountValue
        };

        _context.CouponRedemptions.Add(redemption);
        coupon.TimesRedeemed++;

        await _context.SaveChangesAsync();

        return validation.Coupon;
    }

    // ==================== RAZORPAY IMPLEMENTATION ====================

    public async Task<RazorpayOrderResponse> CreateRazorpayOrderAsync(string siteId, decimal amount, string currency, string planId, string billingCycle)
    {
        var stopwatch = Stopwatch.StartNew();
        // Check if siteId exists in database to determine if it's a registration
        var siteExists = await _context.Sites.AnyAsync(s => s.Id == siteId);
        var log = new PaymentLog
        {
            SiteId = siteExists ? siteId : null,
            Action = "create_order",
            Gateway = "razorpay",
            Status = "initiated",
            Amount = amount,
            Currency = currency,
            Metadata = JsonSerializer.Serialize(new { planId, billingCycle, isRegistration = !siteExists })
        };

        try
        {
            var (keyId, keySecret) = await GetRazorpayCredentialsAsync();

            _logger.LogInformation("Creating Razorpay order: SiteId={SiteId}, Amount={Amount}, Currency={Currency}, PlanId={PlanId}",
                siteId, amount, currency, planId);

            if (string.IsNullOrEmpty(keyId) || string.IsNullOrEmpty(keySecret))
            {
                log.Status = "error";
                log.ErrorMessage = "Razorpay credentials not configured";
                log.ErrorCode = "CONFIG_ERROR";
                await LogPaymentAction(log);
                throw new InvalidOperationException("Razorpay credentials not configured");
            }

            var client = _httpClientFactory.CreateClient();

            // Convert amount to paise (Razorpay uses smallest currency unit)
            var amountInPaise = (int)(amount * 100);

            // Receipt must be max 40 characters
            var receiptId = $"rcpt_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString()[..8]}";

            var orderRequest = new
            {
                amount = amountInPaise,
                currency = currency.ToUpper(),
                receipt = receiptId,
                notes = new
                {
                    site_id = siteId,
                    plan_id = planId,
                    billing_cycle = billingCycle
                }
            };

            log.RequestData = JsonSerializer.Serialize(new { amount = amountInPaise, currency = currency.ToUpper(), receipt = receiptId, planId, billingCycle });

            var content = new StringContent(
                JsonSerializer.Serialize(orderRequest),
                Encoding.UTF8,
                "application/json"
            );

            // Add Basic Auth header
            var authString = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{keyId}:{keySecret}"));
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authString);

            log.Status = "processing";
            var response = await client.PostAsync("https://api.razorpay.com/v1/orders", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            log.ResponseData = responseContent;
            stopwatch.Stop();
            log.DurationMs = (int)stopwatch.ElapsedMilliseconds;

            _logger.LogInformation("Razorpay API Response: {StatusCode} - {Response} - Duration: {Duration}ms",
                response.StatusCode, responseContent, log.DurationMs);

            if (!response.IsSuccessStatusCode)
            {
                log.Status = "failed";
                log.ErrorMessage = $"Razorpay order creation failed: HTTP {(int)response.StatusCode}";
                log.ErrorCode = $"HTTP_{(int)response.StatusCode}";
                await LogPaymentAction(log);

                _logger.LogError("Razorpay order creation failed: {StatusCode} - {Response}", response.StatusCode, responseContent);
                throw new InvalidOperationException($"Razorpay order creation failed: {responseContent}");
            }

            var orderResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
            var orderId = orderResponse.GetProperty("id").GetString()!;

            log.Status = "success";
            log.OrderId = orderId;
            log.TransactionId = orderId;
            await LogPaymentAction(log);

            _logger.LogInformation("Razorpay order created successfully: OrderId={OrderId}", orderId);

            return new RazorpayOrderResponse(
                OrderId: orderId,
                Amount: amount,
                Currency: currency,
                KeyId: keyId,
                PlanId: planId,
                SiteId: siteId
            );
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            stopwatch.Stop();
            log.Status = "error";
            log.ErrorMessage = ex.Message;
            log.StackTrace = ex.StackTrace;
            log.DurationMs = (int)stopwatch.ElapsedMilliseconds;
            await LogPaymentAction(log);

            _logger.LogError(ex, "Razorpay order creation exception: {Message}", ex.Message);
            await _errorLogService.LogErrorAsync(ex, null, "Error");
            throw;
        }
    }

    public async Task<RazorpayOrderResponse> CreateRazorpayRegistrationOrderAsync(string paymentReference, decimal amount, string currency, string planId, string billingCycle)
    {
        var stopwatch = Stopwatch.StartNew();
        var log = new PaymentLog
        {
            SiteId = null, // No site yet for registration
            PaymentReference = paymentReference,
            Action = "create_order",
            Gateway = "razorpay",
            Status = "initiated",
            Amount = amount,
            Currency = currency,
            Metadata = JsonSerializer.Serialize(new { planId, billingCycle, isRegistration = true, paymentReference })
        };

        try
        {
            var (keyId, keySecret) = await GetRazorpayCredentialsAsync();

            _logger.LogInformation("Creating Razorpay registration order: PaymentRef={PaymentRef}, Amount={Amount}, Currency={Currency}, PlanId={PlanId}",
                paymentReference, amount, currency, planId);

            if (string.IsNullOrEmpty(keyId) || string.IsNullOrEmpty(keySecret))
            {
                log.Status = "error";
                log.ErrorMessage = "Razorpay credentials not configured";
                log.ErrorCode = "CONFIG_ERROR";
                await LogPaymentAction(log);
                throw new InvalidOperationException("Razorpay credentials not configured");
            }

            var client = _httpClientFactory.CreateClient();

            // Convert amount to paise (Razorpay uses smallest currency unit)
            var amountInPaise = (int)(amount * 100);

            // Receipt must be max 40 characters
            var receiptId = $"reg_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString()[..8]}";

            var orderRequest = new
            {
                amount = amountInPaise,
                currency = currency.ToUpper(),
                receipt = receiptId,
                notes = new
                {
                    payment_ref = paymentReference,
                    plan_id = planId,
                    billing_cycle = billingCycle,
                    type = "registration"
                }
            };

            log.RequestData = JsonSerializer.Serialize(new { amount = amountInPaise, currency = currency.ToUpper(), receipt = receiptId, planId, billingCycle, paymentReference });

            var content = new StringContent(
                JsonSerializer.Serialize(orderRequest),
                Encoding.UTF8,
                "application/json"
            );

            // Add Basic Auth header
            var authString = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{keyId}:{keySecret}"));
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authString);

            log.Status = "processing";
            var response = await client.PostAsync("https://api.razorpay.com/v1/orders", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            log.ResponseData = responseContent;
            stopwatch.Stop();
            log.DurationMs = (int)stopwatch.ElapsedMilliseconds;

            _logger.LogInformation("Razorpay API Response: {StatusCode} - {Response} - Duration: {Duration}ms",
                response.StatusCode, responseContent, log.DurationMs);

            if (!response.IsSuccessStatusCode)
            {
                log.Status = "failed";
                log.ErrorMessage = $"Razorpay order creation failed: HTTP {(int)response.StatusCode}";
                log.ErrorCode = $"HTTP_{(int)response.StatusCode}";
                await LogPaymentAction(log);

                _logger.LogError("Razorpay order creation failed: {StatusCode} - {Response}", response.StatusCode, responseContent);
                throw new InvalidOperationException($"Razorpay order creation failed: {responseContent}");
            }

            var orderResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
            var orderId = orderResponse.GetProperty("id").GetString()!;

            log.Status = "success";
            log.OrderId = orderId;
            log.TransactionId = orderId;
            await LogPaymentAction(log);

            _logger.LogInformation("Razorpay registration order created successfully: OrderId={OrderId}, PaymentRef={PaymentRef}", orderId, paymentReference);

            return new RazorpayOrderResponse(
                OrderId: orderId,
                Amount: amount,
                Currency: currency,
                KeyId: keyId,
                PlanId: planId,
                SiteId: paymentReference // Using paymentReference as SiteId for DTO compatibility
            );
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            stopwatch.Stop();
            log.Status = "error";
            log.ErrorMessage = ex.Message;
            log.StackTrace = ex.StackTrace;
            log.DurationMs = (int)stopwatch.ElapsedMilliseconds;
            await LogPaymentAction(log);

            _logger.LogError(ex, "Razorpay registration order creation exception: {Message}", ex.Message);
            await _errorLogService.LogErrorAsync(ex, null, "Error");
            throw;
        }
    }

    public async Task<PaymentVerificationResult> VerifyRazorpayPaymentAsync(RazorpayPaymentVerification verification)
    {
        var stopwatch = Stopwatch.StartNew();
        var log = new PaymentLog
        {
            SiteId = verification.SiteId,
            Action = "verify_payment",
            Gateway = "razorpay",
            Status = "initiated",
            OrderId = verification.RazorpayOrderId,
            TransactionId = verification.RazorpayPaymentId,
            Metadata = JsonSerializer.Serialize(new { verification.PlanId, verification.BillingCycle })
        };

        _logger.LogInformation("Verifying Razorpay payment: OrderId={OrderId}, PaymentId={PaymentId}, SiteId={SiteId}",
            verification.RazorpayOrderId, verification.RazorpayPaymentId, verification.SiteId);

        try
        {
            var (_, keySecret) = await GetRazorpayCredentialsAsync();

            if (string.IsNullOrEmpty(keySecret))
            {
                log.Status = "error";
                log.ErrorMessage = "Razorpay credentials not configured";
                log.ErrorCode = "CONFIG_ERROR";
                await LogPaymentAction(log);
                throw new InvalidOperationException("Razorpay credentials not configured");
            }

            // Verify signature
            var payload = $"{verification.RazorpayOrderId}|{verification.RazorpayPaymentId}";
            var expectedSignature = ComputeHmacSha256(payload, keySecret);

            log.RequestData = JsonSerializer.Serialize(new {
                orderId = verification.RazorpayOrderId,
                paymentId = verification.RazorpayPaymentId,
                signatureProvided = verification.RazorpaySignature?.Length > 0
            });

            if (expectedSignature != verification.RazorpaySignature)
            {
                stopwatch.Stop();
                log.Status = "failed";
                log.ErrorMessage = "Payment verification failed - invalid signature";
                log.ErrorCode = "INVALID_SIGNATURE";
                log.DurationMs = (int)stopwatch.ElapsedMilliseconds;
                await LogPaymentAction(log);

                _logger.LogWarning("Razorpay signature verification failed: OrderId={OrderId}", verification.RazorpayOrderId);

                return new PaymentVerificationResult(
                    Success: false,
                    PaymentId: null,
                    Message: "Payment verification failed - invalid signature",
                    TransactionId: null
                );
            }

            _logger.LogInformation("Razorpay signature verified successfully: OrderId={OrderId}", verification.RazorpayOrderId);

            // Payment verified - record it and upgrade subscription
            // Get plan details
            var plan = await _context.SubscriptionPlans.FindAsync(verification.PlanId);
            if (plan == null)
            {
                log.Status = "error";
                log.ErrorMessage = "Plan not found";
                log.ErrorCode = "PLAN_NOT_FOUND";
                await LogPaymentAction(log);
                throw new KeyNotFoundException("Plan not found");
            }

            var amount = verification.BillingCycle == "yearly" || verification.BillingCycle == "annual"
                ? plan.AnnualPrice ?? plan.MonthlyPrice
                : plan.MonthlyPrice;

            log.Amount = amount;
            log.Currency = plan.Currency;

            // Get active or trialing subscription for this site
            var subscription = await _context.Subscriptions
                .FirstOrDefaultAsync(s => s.SiteId == verification.SiteId && (s.Status == "active" || s.Status == "trialing"));

            // Create invoice
            var invoice = new Invoice
            {
                SiteId = verification.SiteId,
                SubscriptionId = subscription?.Id,
                InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                Status = "paid",
                Subtotal = amount,
                Tax = 0,
                Discount = 0,
                Total = amount,
                AmountPaid = amount,
                AmountDue = 0,
                Currency = plan.Currency,
                DueDate = DateTime.UtcNow,
                PaidAt = DateTime.UtcNow,
                PeriodStart = DateTime.UtcNow,
                PeriodEnd = verification.BillingCycle == "yearly" || verification.BillingCycle == "annual"
                    ? DateTime.UtcNow.AddYears(1)
                    : DateTime.UtcNow.AddMonths(1)
            };

            invoice.Items.Add(new InvoiceItem
            {
                Description = $"{plan.Name} Plan - {verification.BillingCycle} subscription",
                Quantity = 1,
                UnitPrice = amount,
                Amount = amount
            });

            _context.Invoices.Add(invoice);

            // Record payment
            var payment = new Payment
            {
                SiteId = verification.SiteId,
                InvoiceId = invoice.Id,
                Amount = amount,
                Currency = plan.Currency,
                Status = "succeeded",
                StripePaymentIntentId = verification.RazorpayPaymentId,
                StripeChargeId = verification.RazorpayOrderId
            };

            _context.Payments.Add(payment);

            // Upgrade subscription
            await _subscriptionService.UpdateSubscriptionAsync(verification.SiteId, new UpdateSubscriptionRequest(
                verification.PlanId,
                verification.BillingCycle,
                null
            ));

            await _context.SaveChangesAsync();

            stopwatch.Stop();
            log.Status = "success";
            log.PaymentId = payment.Id;
            log.DurationMs = (int)stopwatch.ElapsedMilliseconds;
            log.ResponseData = JsonSerializer.Serialize(new { paymentId = payment.Id, invoiceId = invoice.Id, planName = plan.Name });
            await LogPaymentAction(log);

            _logger.LogInformation("Razorpay payment verified and recorded: PaymentId={PaymentId}, Amount={Amount} {Currency}",
                payment.Id, amount, plan.Currency);

            // Send payment receipt email
            try
            {
                var site = await _context.Sites
                    .Include(s => s.OwnerUser)
                    .FirstOrDefaultAsync(s => s.Id == verification.SiteId);

                if (site?.OwnerUser != null)
                {
                    await _emailService.SendPaymentReceiptEmailAsync(
                        site.OwnerUser.Email,
                        site.OwnerUser.Username,
                        site.Name,
                        plan.Name,
                        invoice.InvoiceNumber,
                        verification.RazorpayPaymentId,
                        amount,
                        plan.Currency,
                        verification.BillingCycle,
                        invoice.PeriodStart,
                        invoice.PeriodEnd,
                        "Razorpay"
                    );
                    _logger.LogInformation("Payment receipt email sent for Razorpay payment: {PaymentId}", payment.Id);
                }
            }
            catch (Exception emailEx)
            {
                // Don't fail the payment verification if email fails
                _logger.LogError(emailEx, "Failed to send payment receipt email for Razorpay payment: {PaymentId}", payment.Id);
                await _errorLogService.LogErrorAsync(emailEx, null, "Warning");
            }

            return new PaymentVerificationResult(
                Success: true,
                PaymentId: payment.Id,
                Message: "Payment successful",
                TransactionId: verification.RazorpayPaymentId
            );
        }
        catch (Exception ex) when (ex is not InvalidOperationException && ex is not KeyNotFoundException)
        {
            stopwatch.Stop();
            log.Status = "error";
            log.ErrorMessage = ex.Message;
            log.StackTrace = ex.StackTrace;
            log.DurationMs = (int)stopwatch.ElapsedMilliseconds;
            await LogPaymentAction(log);

            _logger.LogError(ex, "Razorpay payment verification exception: {Message}", ex.Message);
            await _errorLogService.LogErrorAsync(ex, null, "Error");

            return new PaymentVerificationResult(
                Success: false,
                PaymentId: null,
                Message: $"Payment recorded but subscription update failed: {ex.Message}",
                TransactionId: verification.RazorpayPaymentId
            );
        }
    }

    private static string ComputeHmacSha256(string data, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }

    // ==================== PAYPAL IMPLEMENTATION ====================

    public async Task<PayPalOrderResponse> CreatePayPalOrderAsync(string siteId, decimal amount, string currency, string planId, string billingCycle, string returnUrl)
    {
        var (clientId, clientSecret, mode) = await GetPayPalCredentialsAsync();

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            throw new InvalidOperationException("PayPal credentials not configured");

        var baseUrl = mode == "live"
            ? "https://api-m.paypal.com"
            : "https://api-m.sandbox.paypal.com";

        var client = _httpClientFactory.CreateClient();

        // Get access token
        var authString = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{clientId}:{clientSecret}"));
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authString);

        var tokenContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials")
        });

        var tokenResponse = await client.PostAsync($"{baseUrl}/v1/oauth2/token", tokenContent);
        var tokenJson = await tokenResponse.Content.ReadAsStringAsync();

        if (!tokenResponse.IsSuccessStatusCode)
            throw new InvalidOperationException($"PayPal authentication failed: {tokenJson}");

        var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenJson);
        var accessToken = tokenData.GetProperty("access_token").GetString();

        // Create order
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var orderRequest = new
        {
            intent = "CAPTURE",
            purchase_units = new[]
            {
                new
                {
                    reference_id = $"{siteId}_{planId}_{billingCycle}",
                    amount = new
                    {
                        currency_code = currency.ToUpper(),
                        value = amount.ToString("F2")
                    },
                    description = $"Subscription upgrade to plan"
                }
            },
            application_context = new
            {
                return_url = $"{returnUrl}?payment=success&provider=paypal",
                cancel_url = $"{returnUrl}?payment=cancelled&provider=paypal"
            }
        };

        var orderContent = new StringContent(
            JsonSerializer.Serialize(orderRequest),
            Encoding.UTF8,
            "application/json"
        );

        var orderResponse = await client.PostAsync($"{baseUrl}/v2/checkout/orders", orderContent);
        var orderJson = await orderResponse.Content.ReadAsStringAsync();

        if (!orderResponse.IsSuccessStatusCode)
            throw new InvalidOperationException($"PayPal order creation failed: {orderJson}");

        var orderData = JsonSerializer.Deserialize<JsonElement>(orderJson);
        var orderId = orderData.GetProperty("id").GetString()!;

        // Find approval URL
        var approvalUrl = "";
        foreach (var link in orderData.GetProperty("links").EnumerateArray())
        {
            if (link.GetProperty("rel").GetString() == "approve")
            {
                approvalUrl = link.GetProperty("href").GetString()!;
                break;
            }
        }

        return new PayPalOrderResponse(
            OrderId: orderId,
            ApprovalUrl: approvalUrl,
            Amount: amount,
            Currency: currency,
            PlanId: planId,
            SiteId: siteId
        );
    }

    public async Task<PayPalOrderResponse> CreatePayPalRegistrationOrderAsync(string paymentReference, decimal amount, string currency, string planId, string billingCycle, string returnUrl)
    {
        var (clientId, clientSecret, mode) = await GetPayPalCredentialsAsync();

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            throw new InvalidOperationException("PayPal credentials not configured");

        var baseUrl = mode == "live"
            ? "https://api-m.paypal.com"
            : "https://api-m.sandbox.paypal.com";

        var client = _httpClientFactory.CreateClient();

        // Get access token
        var authString = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{clientId}:{clientSecret}"));
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authString);

        var tokenContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials")
        });

        var tokenResponse = await client.PostAsync($"{baseUrl}/v1/oauth2/token", tokenContent);
        var tokenJson = await tokenResponse.Content.ReadAsStringAsync();

        if (!tokenResponse.IsSuccessStatusCode)
            throw new InvalidOperationException($"PayPal authentication failed: {tokenJson}");

        var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenJson);
        var accessToken = tokenData.GetProperty("access_token").GetString();

        // Create order with payment reference instead of site ID
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var orderRequest = new
        {
            intent = "CAPTURE",
            purchase_units = new[]
            {
                new
                {
                    reference_id = $"reg_{paymentReference}_{planId}",
                    custom_id = paymentReference, // Store payment reference for later linking
                    amount = new
                    {
                        currency_code = currency.ToUpper(),
                        value = amount.ToString("F2")
                    },
                    description = $"New account registration - subscription plan"
                }
            },
            application_context = new
            {
                return_url = $"{returnUrl}?payment=success&provider=paypal",
                cancel_url = $"{returnUrl}?payment=cancelled&provider=paypal"
            }
        };

        var orderContent = new StringContent(
            JsonSerializer.Serialize(orderRequest),
            Encoding.UTF8,
            "application/json"
        );

        var orderResponse = await client.PostAsync($"{baseUrl}/v2/checkout/orders", orderContent);
        var orderJson = await orderResponse.Content.ReadAsStringAsync();

        if (!orderResponse.IsSuccessStatusCode)
            throw new InvalidOperationException($"PayPal order creation failed: {orderJson}");

        var orderData = JsonSerializer.Deserialize<JsonElement>(orderJson);
        var orderId = orderData.GetProperty("id").GetString()!;

        // Find approval URL
        var approvalUrl = "";
        foreach (var link in orderData.GetProperty("links").EnumerateArray())
        {
            if (link.GetProperty("rel").GetString() == "approve")
            {
                approvalUrl = link.GetProperty("href").GetString()!;
                break;
            }
        }

        _logger.LogInformation("PayPal registration order created: OrderId={OrderId}, PaymentRef={PaymentRef}", orderId, paymentReference);

        return new PayPalOrderResponse(
            OrderId: orderId,
            ApprovalUrl: approvalUrl,
            Amount: amount,
            Currency: currency,
            PlanId: planId,
            SiteId: paymentReference // Using paymentReference for DTO compatibility
        );
    }

    public async Task<PaymentVerificationResult> CapturePayPalPaymentAsync(string orderId, string siteId, string planId, string billingCycle)
    {
        var (clientId, clientSecret, mode) = await GetPayPalCredentialsAsync();

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            throw new InvalidOperationException("PayPal credentials not configured");

        var baseUrl = mode == "live"
            ? "https://api-m.paypal.com"
            : "https://api-m.sandbox.paypal.com";

        var client = _httpClientFactory.CreateClient();

        // Get access token
        var authString = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{clientId}:{clientSecret}"));
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authString);

        var tokenContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials")
        });

        var tokenResponse = await client.PostAsync($"{baseUrl}/v1/oauth2/token", tokenContent);
        var tokenJson = await tokenResponse.Content.ReadAsStringAsync();

        if (!tokenResponse.IsSuccessStatusCode)
            throw new InvalidOperationException($"PayPal authentication failed: {tokenJson}");

        var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenJson);
        var accessToken = tokenData.GetProperty("access_token").GetString();

        // Capture payment
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var captureResponse = await client.PostAsync(
            $"{baseUrl}/v2/checkout/orders/{orderId}/capture",
            new StringContent("{}", Encoding.UTF8, "application/json")
        );
        var captureJson = await captureResponse.Content.ReadAsStringAsync();

        if (!captureResponse.IsSuccessStatusCode)
        {
            return new PaymentVerificationResult(
                Success: false,
                PaymentId: null,
                Message: $"PayPal payment capture failed: {captureJson}",
                TransactionId: null
            );
        }

        var captureData = JsonSerializer.Deserialize<JsonElement>(captureJson);
        var status = captureData.GetProperty("status").GetString();

        if (status != "COMPLETED")
        {
            return new PaymentVerificationResult(
                Success: false,
                PaymentId: null,
                Message: $"Payment not completed. Status: {status}",
                TransactionId: orderId
            );
        }

        // Get capture details
        var purchaseUnit = captureData.GetProperty("purchase_units")[0];
        var capture = purchaseUnit.GetProperty("payments").GetProperty("captures")[0];
        var captureId = capture.GetProperty("id").GetString()!;
        var amountValue = decimal.Parse(capture.GetProperty("amount").GetProperty("value").GetString()!);
        var currencyCode = capture.GetProperty("amount").GetProperty("currency_code").GetString()!;

        try
        {
            // Get plan details
            var plan = await _context.SubscriptionPlans.FindAsync(planId);
            if (plan == null)
                throw new KeyNotFoundException("Plan not found");

            // Get active subscription for this site
            var subscription = await _context.Subscriptions
                .FirstOrDefaultAsync(s => s.SiteId == siteId && s.Status == "active");

            // Create invoice
            var invoice = new Invoice
            {
                SiteId = siteId,
                SubscriptionId = subscription?.Id,
                InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                Status = "paid",
                Subtotal = amountValue,
                Tax = 0,
                Discount = 0,
                Total = amountValue,
                AmountPaid = amountValue,
                AmountDue = 0,
                Currency = currencyCode,
                DueDate = DateTime.UtcNow,
                PaidAt = DateTime.UtcNow,
                PeriodStart = DateTime.UtcNow,
                PeriodEnd = billingCycle == "yearly" || billingCycle == "annual"
                    ? DateTime.UtcNow.AddYears(1)
                    : DateTime.UtcNow.AddMonths(1)
            };

            invoice.Items.Add(new InvoiceItem
            {
                Description = $"{plan.Name} Plan - {billingCycle} subscription",
                Quantity = 1,
                UnitPrice = amountValue,
                Amount = amountValue
            });

            _context.Invoices.Add(invoice);

            // Record payment
            var payment = new Payment
            {
                SiteId = siteId,
                InvoiceId = invoice.Id,
                Amount = amountValue,
                Currency = currencyCode,
                Status = "succeeded",
                StripePaymentIntentId = captureId, // Using for PayPal capture ID
                StripeChargeId = orderId // Using for PayPal order ID
            };

            _context.Payments.Add(payment);

            // Upgrade subscription
            await _subscriptionService.UpdateSubscriptionAsync(siteId, new UpdateSubscriptionRequest(
                planId,
                billingCycle,
                null
            ));

            await _context.SaveChangesAsync();

            // Send payment receipt email
            try
            {
                var site = await _context.Sites
                    .Include(s => s.OwnerUser)
                    .FirstOrDefaultAsync(s => s.Id == siteId);

                if (site?.OwnerUser != null)
                {
                    await _emailService.SendPaymentReceiptEmailAsync(
                        site.OwnerUser.Email,
                        site.OwnerUser.Username,
                        site.Name,
                        plan.Name,
                        invoice.InvoiceNumber,
                        captureId,
                        amountValue,
                        currencyCode,
                        billingCycle,
                        invoice.PeriodStart,
                        invoice.PeriodEnd,
                        "PayPal"
                    );
                    _logger.LogInformation("Payment receipt email sent for PayPal payment: {PaymentId}", payment.Id);
                }
            }
            catch (Exception emailEx)
            {
                // Don't fail the payment verification if email fails
                _logger.LogError(emailEx, "Failed to send payment receipt email for PayPal payment: {PaymentId}", payment.Id);
                await _errorLogService.LogErrorAsync(emailEx, null, "Warning");
            }

            return new PaymentVerificationResult(
                Success: true,
                PaymentId: payment.Id,
                Message: "Payment successful",
                TransactionId: captureId
            );
        }
        catch (Exception ex)
        {
            await _errorLogService.LogErrorAsync(ex, null, "Error");
            return new PaymentVerificationResult(
                Success: false,
                PaymentId: null,
                Message: $"Payment captured but subscription update failed: {ex.Message}",
                TransactionId: captureId
            );
        }
    }

    // ==================== MAPPING METHODS ====================

    private static PaymentMethodDto MapPaymentMethodToDto(PaymentMethod pm) => new(
        pm.Id,
        pm.Type,
        pm.Last4,
        pm.Brand,
        pm.ExpMonth,
        pm.ExpYear,
        pm.BankName,
        pm.IsDefault,
        pm.CreatedAt
    );

    private static InvoiceDto MapInvoiceToDto(Invoice invoice) => new(
        invoice.Id,
        invoice.InvoiceNumber,
        invoice.Status,
        invoice.Subtotal,
        invoice.Tax,
        invoice.Discount,
        invoice.Total,
        invoice.AmountPaid,
        invoice.AmountDue,
        invoice.Currency,
        invoice.DueDate,
        invoice.PaidAt,
        invoice.PeriodStart,
        invoice.PeriodEnd,
        invoice.Items.Select(i => new InvoiceItemDto(
            i.Description,
            i.Quantity,
            i.UnitPrice,
            i.Amount
        )).ToList(),
        invoice.StripeInvoiceUrl,
        invoice.StripeInvoicePdf,
        invoice.CreatedAt
    );

    private static PaymentDto MapPaymentToDto(Payment payment) => new(
        payment.Id,
        payment.InvoiceId,
        payment.Amount,
        payment.Currency,
        payment.Status,
        payment.FailureReason,
        payment.PaymentMethod?.Last4,
        payment.PaymentMethod?.Brand,
        payment.CreatedAt
    );

    private static CouponDto MapCouponToDto(Coupon coupon) => new(
        coupon.Id,
        coupon.Code,
        coupon.Description,
        coupon.DiscountType,
        coupon.DiscountValue,
        coupon.Currency,
        coupon.ValidFrom,
        coupon.ValidUntil,
        coupon.IsActive
    );
}
