using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ChatApp.API.Data;
using ChatApp.API.Models.Entities;

namespace ChatApp.API.Services.Implementations;

public interface IPaymentLogService
{
    Task<PaymentLog> LogAsync(PaymentLogEntry entry);
    Task<PaymentLog> LogSuccessAsync(PaymentLog log, string? responseData = null, string? transactionId = null);
    Task<PaymentLog> LogFailureAsync(PaymentLog log, string errorMessage, string? errorCode = null, string? stackTrace = null);
    Task<List<PaymentLog>> GetLogsAsync(string? siteId = null, string? subscriptionId = null, string? action = null, DateTime? from = null, DateTime? to = null, int limit = 100);
}

public class PaymentLogEntry
{
    public string? SiteId { get; set; }
    public string? SubscriptionId { get; set; }
    public string? PaymentMethodId { get; set; }
    public string? PaymentId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Gateway { get; set; } = string.Empty;
    public string? RequestData { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? UserId { get; set; }
    public object? Metadata { get; set; }
}

public class PaymentLogService : IPaymentLogService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PaymentLogService> _logger;

    public PaymentLogService(ApplicationDbContext context, ILogger<PaymentLogService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PaymentLog> LogAsync(PaymentLogEntry entry)
    {
        var log = new PaymentLog
        {
            SiteId = entry.SiteId,
            SubscriptionId = entry.SubscriptionId,
            PaymentMethodId = entry.PaymentMethodId,
            PaymentId = entry.PaymentId,
            Action = entry.Action,
            Gateway = entry.Gateway,
            Status = "initiated",
            RequestData = MaskSensitiveData(entry.RequestData),
            Amount = entry.Amount,
            Currency = entry.Currency,
            IpAddress = entry.IpAddress,
            UserAgent = entry.UserAgent,
            UserId = entry.UserId,
            Metadata = entry.Metadata != null ? JsonSerializer.Serialize(entry.Metadata) : null,
            CreatedAt = DateTime.UtcNow
        };

        _context.PaymentLogs.Add(log);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Payment log created: {Action} for Site {SiteId}, Gateway {Gateway}",
            entry.Action, entry.SiteId, entry.Gateway);

        return log;
    }

    public async Task<PaymentLog> LogSuccessAsync(PaymentLog log, string? responseData = null, string? transactionId = null)
    {
        log.Status = "success";
        log.ResponseData = MaskSensitiveData(responseData);
        log.TransactionId = transactionId;
        log.DurationMs = (int)(DateTime.UtcNow - log.CreatedAt).TotalMilliseconds;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Payment log success: {Action} for Site {SiteId}, Transaction {TransactionId}",
            log.Action, log.SiteId, transactionId);

        return log;
    }

    public async Task<PaymentLog> LogFailureAsync(PaymentLog log, string errorMessage, string? errorCode = null, string? stackTrace = null)
    {
        log.Status = "failed";
        log.ErrorMessage = errorMessage;
        log.ErrorCode = errorCode;
        log.StackTrace = stackTrace;
        log.DurationMs = (int)(DateTime.UtcNow - log.CreatedAt).TotalMilliseconds;

        await _context.SaveChangesAsync();

        _logger.LogWarning("Payment log failure: {Action} for Site {SiteId}, Error: {ErrorMessage}",
            log.Action, log.SiteId, errorMessage);

        return log;
    }

    public async Task<List<PaymentLog>> GetLogsAsync(
        string? siteId = null,
        string? subscriptionId = null,
        string? action = null,
        DateTime? from = null,
        DateTime? to = null,
        int limit = 100)
    {
        var query = _context.PaymentLogs.AsQueryable();

        if (!string.IsNullOrEmpty(siteId))
            query = query.Where(l => l.SiteId == siteId);

        if (!string.IsNullOrEmpty(subscriptionId))
            query = query.Where(l => l.SubscriptionId == subscriptionId);

        if (!string.IsNullOrEmpty(action))
            query = query.Where(l => l.Action == action);

        if (from.HasValue)
            query = query.Where(l => l.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(l => l.CreatedAt <= to.Value);

        return await query
            .OrderByDescending(l => l.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    private static string? MaskSensitiveData(string? data)
    {
        if (string.IsNullOrEmpty(data)) return data;

        try
        {
            // Try to parse as JSON and mask sensitive fields
            var json = JsonSerializer.Deserialize<JsonElement>(data);
            var masked = MaskJsonElement(json);
            return JsonSerializer.Serialize(masked);
        }
        catch
        {
            // If not JSON, return as-is (might want to add more masking here)
            return data;
        }
    }

    private static JsonElement MaskJsonElement(JsonElement element)
    {
        var sensitiveFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "password", "secret", "key", "token", "cvv", "cvc", "card_number",
            "cardnumber", "card", "api_key", "apikey", "authorization", "auth"
        };

        if (element.ValueKind == JsonValueKind.Object)
        {
            var dict = new Dictionary<string, object?>();
            foreach (var property in element.EnumerateObject())
            {
                if (sensitiveFields.Contains(property.Name))
                {
                    dict[property.Name] = "***MASKED***";
                }
                else
                {
                    dict[property.Name] = GetValue(property.Value);
                }
            }
            return JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(dict));
        }

        return element;
    }

    private static object? GetValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Object => JsonSerializer.Deserialize<Dictionary<string, object?>>(element.GetRawText()),
            JsonValueKind.Array => JsonSerializer.Deserialize<List<object?>>(element.GetRawText()),
            _ => element.GetRawText()
        };
    }
}
