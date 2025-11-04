using System.Security.Claims;

namespace ApiGateway.Middleware;

/// <summary>
/// Middleware to forward authenticated user claims as headers to downstream services
/// This runs before YARP forwards the request
/// </summary>
public class UserClaimsForwardingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<UserClaimsForwardingMiddleware> _logger;

    public UserClaimsForwardingMiddleware(RequestDelegate next, ILogger<UserClaimsForwardingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add a header to identify requests coming from API Gateway
        context.Request.Headers["X-Forwarded-By"] = "ApiGateway";
        
        // Check if user is authenticated
        if (context.User.Identity?.IsAuthenticated == true)
        {
            // Extract user ID from claims (Sub claim contains user ID)
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                        ?? context.User.FindFirst("sub")?.Value;
            
            var username = context.User.FindFirst(ClaimTypes.Name)?.Value 
                          ?? context.User.FindFirst("unique_name")?.Value;
            
            var email = context.User.FindFirst(ClaimTypes.Email)?.Value 
                       ?? context.User.FindFirst("email")?.Value;

            // Forward user information as headers to downstream services
            if (!string.IsNullOrEmpty(userId))
            {
                context.Request.Headers["X-User-Id"] = userId;
                _logger.LogDebug("Forwarding User ID: {UserId}", userId);
            }
            
            if (!string.IsNullOrEmpty(username))
            {
                context.Request.Headers["X-User-Name"] = username;
            }
            
            if (!string.IsNullOrEmpty(email))
            {
                context.Request.Headers["X-User-Email"] = email;
            }

            _logger.LogInformation("Forwarding authenticated user claims - UserId: {UserId}, Username: {Username}", 
                userId, username);
        }

        await _next(context);
    }
}

/// <summary>
/// Extension method to register the middleware
/// </summary>
public static class UserClaimsForwardingMiddlewareExtensions
{
    public static IApplicationBuilder UseUserClaimsForwarding(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<UserClaimsForwardingMiddleware>();
    }
}
