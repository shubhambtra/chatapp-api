using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;
using ChatApp.API.Data;
using ChatApp.API.Models.DTOs;

namespace ChatApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebhooksController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(
        IConfiguration configuration,
        ApplicationDbContext context,
        ILogger<WebhooksController> logger)
    {
        _configuration = configuration;
        _context = context;
        _logger = logger;
    }

    [HttpPost("stripe")]
    public async Task<IActionResult> HandleStripeWebhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var webhookSecret = _configuration["Stripe:WebhookSecret"];

        try
        {
            var stripeEvent = EventUtility.ConstructEvent(
                json,
                Request.Headers["Stripe-Signature"],
                webhookSecret
            );

            _logger.LogInformation("Stripe webhook received: {EventType}", stripeEvent.Type);

            switch (stripeEvent.Type)
            {
                case Events.CustomerSubscriptionCreated:
                    await HandleSubscriptionCreated(stripeEvent);
                    break;

                case Events.CustomerSubscriptionUpdated:
                    await HandleSubscriptionUpdated(stripeEvent);
                    break;

                case Events.CustomerSubscriptionDeleted:
                    await HandleSubscriptionDeleted(stripeEvent);
                    break;

                case Events.InvoicePaid:
                    await HandleInvoicePaid(stripeEvent);
                    break;

                case Events.InvoicePaymentFailed:
                    await HandleInvoicePaymentFailed(stripeEvent);
                    break;

                case Events.PaymentIntentSucceeded:
                    await HandlePaymentSucceeded(stripeEvent);
                    break;

                case Events.PaymentIntentPaymentFailed:
                    await HandlePaymentFailed(stripeEvent);
                    break;

                default:
                    _logger.LogInformation("Unhandled Stripe event type: {EventType}", stripeEvent.Type);
                    break;
            }

            return Ok();
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe webhook error");
            return BadRequest();
        }
    }

    private async Task HandleSubscriptionCreated(Event stripeEvent)
    {
        var subscription = stripeEvent.Data.Object as Subscription;
        if (subscription == null) return;

        _logger.LogInformation("Subscription created: {SubscriptionId}", subscription.Id);
        // Update local subscription record
        await Task.CompletedTask;
    }

    private async Task HandleSubscriptionUpdated(Event stripeEvent)
    {
        var subscription = stripeEvent.Data.Object as Subscription;
        if (subscription == null) return;

        _logger.LogInformation("Subscription updated: {SubscriptionId}", subscription.Id);

        var localSubscription = _context.Subscriptions
            .FirstOrDefault(s => s.StripeSubscriptionId == subscription.Id);

        if (localSubscription != null)
        {
            localSubscription.Status = subscription.Status;
            localSubscription.CurrentPeriodStart = subscription.CurrentPeriodStart;
            localSubscription.CurrentPeriodEnd = subscription.CurrentPeriodEnd;
            localSubscription.CancelAtPeriodEnd = subscription.CancelAtPeriodEnd;

            if (subscription.CanceledAt.HasValue)
            {
                localSubscription.CanceledAt = subscription.CanceledAt.Value;
            }

            await _context.SaveChangesAsync();
        }
    }

    private async Task HandleSubscriptionDeleted(Event stripeEvent)
    {
        var subscription = stripeEvent.Data.Object as Subscription;
        if (subscription == null) return;

        _logger.LogInformation("Subscription deleted: {SubscriptionId}", subscription.Id);

        var localSubscription = _context.Subscriptions
            .FirstOrDefault(s => s.StripeSubscriptionId == subscription.Id);

        if (localSubscription != null)
        {
            localSubscription.Status = "canceled";
            localSubscription.CanceledAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    private async Task HandleInvoicePaid(Event stripeEvent)
    {
        var invoice = stripeEvent.Data.Object as Invoice;
        if (invoice == null) return;

        _logger.LogInformation("Invoice paid: {InvoiceId}", invoice.Id);

        var localInvoice = _context.Invoices
            .FirstOrDefault(i => i.StripeInvoiceId == invoice.Id);

        if (localInvoice != null)
        {
            localInvoice.Status = "paid";
            localInvoice.PaidAt = DateTime.UtcNow;
            localInvoice.AmountPaid = invoice.AmountPaid / 100m;
            localInvoice.AmountDue = 0;
            await _context.SaveChangesAsync();
        }
    }

    private async Task HandleInvoicePaymentFailed(Event stripeEvent)
    {
        var invoice = stripeEvent.Data.Object as Invoice;
        if (invoice == null) return;

        _logger.LogWarning("Invoice payment failed: {InvoiceId}", invoice.Id);

        var localInvoice = _context.Invoices
            .FirstOrDefault(i => i.StripeInvoiceId == invoice.Id);

        if (localInvoice != null)
        {
            localInvoice.Status = "open";
            await _context.SaveChangesAsync();
        }

        // Update subscription status
        if (!string.IsNullOrEmpty(invoice.SubscriptionId))
        {
            var subscription = _context.Subscriptions
                .FirstOrDefault(s => s.StripeSubscriptionId == invoice.SubscriptionId);

            if (subscription != null)
            {
                subscription.Status = "past_due";
                await _context.SaveChangesAsync();
            }
        }
    }

    private async Task HandlePaymentSucceeded(Event stripeEvent)
    {
        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
        if (paymentIntent == null) return;

        _logger.LogInformation("Payment succeeded: {PaymentIntentId}", paymentIntent.Id);

        var payment = _context.Payments
            .FirstOrDefault(p => p.StripePaymentIntentId == paymentIntent.Id);

        if (payment != null)
        {
            payment.Status = "succeeded";
            await _context.SaveChangesAsync();
        }
    }

    private async Task HandlePaymentFailed(Event stripeEvent)
    {
        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
        if (paymentIntent == null) return;

        _logger.LogWarning("Payment failed: {PaymentIntentId}", paymentIntent.Id);

        var payment = _context.Payments
            .FirstOrDefault(p => p.StripePaymentIntentId == paymentIntent.Id);

        if (payment != null)
        {
            payment.Status = "failed";
            payment.FailureReason = paymentIntent.LastPaymentError?.Message;
            await _context.SaveChangesAsync();
        }
    }
}
