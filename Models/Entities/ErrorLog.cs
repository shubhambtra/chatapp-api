namespace ChatApp.API.Models.Entities;

public class ErrorLog : BaseEntityWithIntId
{
    public string ErrorMessage { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string? Source { get; set; }
    public string? ErrorCode { get; set; }
    public string? RequestPath { get; set; }
    public string? RequestMethod { get; set; }
    public string? RequestBody { get; set; }
    public string? QueryString { get; set; }
    public string? UserId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? ExceptionType { get; set; }
    public string? InnerException { get; set; }
    public string Severity { get; set; } = "Error";
}
