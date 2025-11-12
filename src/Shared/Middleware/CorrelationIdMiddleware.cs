using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace Shared.Middleware;

/// <summary>
/// Middleware that handles correlation ID tracking across requests
/// Generates or extracts correlation ID from request headers and adds it to response headers and log context
/// </summary>
public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private const string CorrelationIdKey = "CorrelationId";
    
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Extract correlation ID from request header or generate a new one
        var correlationId = GetOrGenerateCorrelationId(context);

        // Store in HttpContext.Items for use in controllers and services
        context.Items[CorrelationIdKey] = correlationId;

        // Add to response headers so clients can track their requests
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(CorrelationIdHeader))
            {
                context.Response.Headers.Append(CorrelationIdHeader, correlationId);
            }
            return Task.CompletedTask;
        });

        // Push correlation ID to Serilog context for automatic inclusion in all logs
        using (LogContext.PushProperty(CorrelationIdKey, correlationId))
        {
            _logger.LogDebug("Request started with correlation ID: {CorrelationId}", correlationId);
            
            try
            {
                await _next(context);
            }
            finally
            {
                _logger.LogDebug("Request completed with correlation ID: {CorrelationId}", correlationId);
            }
        }
    }

    private string GetOrGenerateCorrelationId(HttpContext context)
    {
        // Try to get correlation ID from request headers
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var correlationId) 
            && !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId.ToString();
        }

        // Generate a new correlation ID if not provided
        var newCorrelationId = Guid.NewGuid().ToString();
        _logger.LogDebug("Generated new correlation ID: {CorrelationId}", newCorrelationId);
        
        return newCorrelationId;
    }
}

/// <summary>
/// Extension methods for registering correlation ID middleware
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    /// <summary>
    /// Adds correlation ID tracking middleware to the pipeline
    /// Should be added early in the pipeline to ensure correlation ID is available throughout the request
    /// </summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CorrelationIdMiddleware>();
    }
}
