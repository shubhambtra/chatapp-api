namespace ChatApp.API.Services.Interfaces;

public interface IErrorLogService
{
    Task LogErrorAsync(Exception ex, HttpContext? context, string severity = "Error");
}
