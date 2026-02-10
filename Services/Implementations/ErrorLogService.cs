using System.Security.Claims;
using System.Text.RegularExpressions;
using ChatApp.API.Data;
using ChatApp.API.Models.Entities;
using ChatApp.API.Services.Interfaces;

namespace ChatApp.API.Services.Implementations;

public class ErrorLogService : IErrorLogService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ErrorLogService> _logger;

    private static readonly Regex SensitiveFieldRegex = new(
        @"(""(?:password|token|secret|authorization|cookie|creditCard|cardNumber|cvv|ssn)""\s*:\s*)""[^""]*""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public ErrorLogService(IServiceScopeFactory scopeFactory, ILogger<ErrorLogService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task LogErrorAsync(Exception ex, HttpContext? context, string severity = "Error")
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var errorLog = new ErrorLog
            {
                ErrorMessage = ex.Message,
                StackTrace = ex.StackTrace,
                Source = ex.Source,
                ExceptionType = ex.GetType().FullName,
                InnerException = ex.InnerException?.Message,
                Severity = severity,
                CreatedAt = DateTime.UtcNow
            };

            if (context != null)
            {
                errorLog.RequestPath = context.Request.Path;
                errorLog.RequestMethod = context.Request.Method;
                errorLog.QueryString = context.Request.QueryString.HasValue
                    ? context.Request.QueryString.Value
                    : null;
                errorLog.UserId = context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
                errorLog.IpAddress = context.Connection.RemoteIpAddress?.ToString();
                errorLog.UserAgent = context.Request.Headers.UserAgent.ToString();

                errorLog.ErrorCode = context.Response.HasStarted
                    ? context.Response.StatusCode.ToString()
                    : null;

                errorLog.RequestBody = await ReadAndSanitizeBodyAsync(context);
            }

            dbContext.ErrorLogs.Add(errorLog);
            await dbContext.SaveChangesAsync();
        }
        catch (Exception logEx)
        {
            // Error logging should never crash the app
            _logger.LogError(logEx, "Failed to log error to database");
        }
    }

    private static async Task<string?> ReadAndSanitizeBodyAsync(HttpContext context)
    {
        try
        {
            if (context.Request.ContentLength is null or 0)
                return null;

            context.Request.Body.Position = 0;
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            if (string.IsNullOrWhiteSpace(body))
                return null;

            // Truncate very large bodies
            if (body.Length > 4000)
                body = body[..4000] + "...[truncated]";

            // Sanitize sensitive fields
            body = SensitiveFieldRegex.Replace(body, @"$1""***""");

            return body;
        }
        catch
        {
            return null;
        }
    }
}
