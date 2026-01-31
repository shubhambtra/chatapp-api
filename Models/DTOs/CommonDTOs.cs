namespace ChatApp.API.Models.DTOs;

public record ApiResponse<T>(
    bool Success,
    T? Data,
    string? Message,
    List<string>? Errors
)
{
    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new(true, data, message, null);

    public static ApiResponse<T> Fail(string message, List<string>? errors = null) =>
        new(false, default, message, errors);
}

public record PagedResponse<T>(
    List<T> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages
);

public record ErrorResponse(
    string Error,
    string? Message,
    string? Code,
    Dictionary<string, List<string>>? ValidationErrors
);

public record HealthCheckResponse(
    string Status,
    DateTime Timestamp,
    Dictionary<string, string>? Services
);
