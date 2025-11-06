using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Shared.Middleware;

/// <summary>
/// Global exception handling middleware for consistent error responses across all services
/// </summary>
public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.TraceIdentifier;
        var requestPath = context.Request.Path;
        var requestMethod = context.Request.Method;

        // Log the exception with context
        _logger.LogError(exception,
            "Unhandled exception occurred. CorrelationId: {CorrelationId}, Method: {Method}, Path: {Path}, Message: {Message}",
            correlationId, requestMethod, requestPath, exception.Message);

        // Determine status code and error message based on exception type
        var (statusCode, message, errorCode) = exception switch
        {
            ArgumentNullException => (HttpStatusCode.BadRequest, "Required parameter is missing", "MISSING_PARAMETER"),
            ArgumentException => (HttpStatusCode.BadRequest, exception.Message, "INVALID_ARGUMENT"),
            InvalidOperationException => (HttpStatusCode.BadRequest, exception.Message, "INVALID_OPERATION"),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized access", "UNAUTHORIZED"),
            KeyNotFoundException => (HttpStatusCode.NotFound, "Resource not found", "NOT_FOUND"),
            TimeoutException => (HttpStatusCode.RequestTimeout, "Request timeout", "TIMEOUT"),
            NotImplementedException => (HttpStatusCode.NotImplemented, "Feature not implemented", "NOT_IMPLEMENTED"),
            _ => (HttpStatusCode.InternalServerError, "An internal server error occurred", "INTERNAL_ERROR")
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        // Get environment to determine if we should include stack trace
        var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

        var errorResponse = new ErrorResponse
        {
            Success = false,
            ErrorCode = errorCode,
            Message = message,
            CorrelationId = correlationId,
            Timestamp = DateTime.UtcNow,
            Path = requestPath,
            // Only include stack trace in development
            StackTrace = isDevelopment ? exception.StackTrace : null
        };

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(errorResponse, jsonOptions);
        await context.Response.WriteAsync(json);
    }
}

/// <summary>
/// Standardized error response model
/// </summary>
public class ErrorResponse
{
    public bool Success { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Path { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
}
