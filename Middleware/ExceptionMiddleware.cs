using System.Net;
using System.Text.Json;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Services.Interfaces;

namespace ChatApp.API.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IErrorLogService _errorLogService;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IErrorLogService errorLogService)
    {
        _next = next;
        _logger = logger;
        _errorLogService = errorLogService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Enable request body buffering so we can read it in error logging
        context.Request.EnableBuffering();

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");

            var severity = ex switch
            {
                KeyNotFoundException => "Warning",
                UnauthorizedAccessException => "Warning",
                InvalidOperationException => "Warning",
                ArgumentException => "Warning",
                _ => "Error"
            };

            await _errorLogService.LogErrorAsync(ex, context, severity);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, message) = exception switch
        {
            KeyNotFoundException => (HttpStatusCode.NotFound, exception.Message),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, exception.Message),
            InvalidOperationException => (HttpStatusCode.BadRequest, exception.Message),
            ArgumentException => (HttpStatusCode.BadRequest, exception.Message),
            _ => (HttpStatusCode.InternalServerError, "An internal server error occurred")
        };

        context.Response.StatusCode = (int)statusCode;

        var response = new ErrorResponse(
            statusCode.ToString(),
            message,
            null,
            null
        );

        var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(jsonResponse);
    }
}
