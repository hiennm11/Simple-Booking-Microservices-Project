using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;

namespace ApiGateway.Middleware;

/// <summary>
/// Extension methods for configuring rate limiting in the API Gateway
/// </summary>
public static class RateLimitingExtensions
{
    private const string UnknownIdentifier = "unknown";
    private const string AnonymousIdentifier = "anonymous";
    private const string UserIdClaimType = "userId";
    
    /// <summary>
    /// Adds comprehensive rate limiting services with multiple policies
    /// </summary>
    public static IServiceCollection AddCustomRateLimiting(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        var rateLimitConfig = configuration.GetSection("RateLimiting");
        
        services.AddRateLimiter(options =>
        {
            // Global reject response when rate limit is exceeded
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            
            options.OnRejected = async (context, cancellationToken) =>
            {
                var endpoint = context.HttpContext.Request.Path;
                var ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? UnknownIdentifier;
                var userId = context.HttpContext.User.Identity?.Name ?? AnonymousIdentifier;
                
                Log.Warning(
                    "Rate limit exceeded for {Endpoint} by {UserId} from {IpAddress}",
                    endpoint, userId, ipAddress);
                
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                
                // Add retry-after header if available
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                }
                
                // Add rate limit headers
                context.HttpContext.Response.Headers["X-RateLimit-Remaining"] = "0";
                
                var retryAfterSeconds = retryAfter != default ? (int)retryAfter.TotalSeconds : 60;
                
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    error = "Rate limit exceeded",
                    message = "Too many requests. Please try again later.",
                    retryAfter = retryAfterSeconds,
                    endpoint = endpoint.ToString(),
                    timestamp = DateTime.UtcNow
                }, cancellationToken);
            };
            
            // ===== POLICY 1: Global Default Policy (Fixed Window) =====
            // Applies to all routes unless overridden
            // 100 requests per minute per IP
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? UnknownIdentifier;
                
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: $"global_{ipAddress}",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitConfig.GetValue("GlobalPolicy:PermitLimit", 100),
                        Window = TimeSpan.FromMinutes(rateLimitConfig.GetValue("GlobalPolicy:WindowMinutes", 1)),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = rateLimitConfig.GetValue("GlobalPolicy:QueueLimit", 5)
                    });
            });
            
            // ===== POLICY 2: User Authentication Policy (Sliding Window) =====
            // Stricter limits for auth endpoints to prevent brute force
            // 5 login attempts per 5 minutes per IP
            options.AddPolicy("auth", context =>
            {
                var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? UnknownIdentifier;
                
                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: $"auth_{ipAddress}",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitConfig.GetValue("AuthPolicy:PermitLimit", 5),
                        Window = TimeSpan.FromMinutes(rateLimitConfig.GetValue("AuthPolicy:WindowMinutes", 5)),
                        SegmentsPerWindow = rateLimitConfig.GetValue("AuthPolicy:SegmentsPerWindow", 5),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0 // No queueing for auth requests
                    });
            });
            
            // ===== POLICY 3: Booking Operations (Token Bucket) =====
            // Allows burst traffic with token replenishment
            // 50 tokens, replenish 10 per minute
            options.AddPolicy("booking", context =>
            {
                var userId = context.User.Identity?.Name ?? 
                             context.User.FindFirst(UserIdClaimType)?.Value ?? 
                             context.Connection.RemoteIpAddress?.ToString() ?? 
                             AnonymousIdentifier;
                
                return RateLimitPartition.GetTokenBucketLimiter(
                    partitionKey: $"booking_{userId}",
                    factory: _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = rateLimitConfig.GetValue("BookingPolicy:TokenLimit", 50),
                        TokensPerPeriod = rateLimitConfig.GetValue("BookingPolicy:TokensPerPeriod", 10),
                        ReplenishmentPeriod = TimeSpan.FromMinutes(
                            rateLimitConfig.GetValue("BookingPolicy:ReplenishmentMinutes", 1)),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = rateLimitConfig.GetValue("BookingPolicy:QueueLimit", 3)
                    });
            });
            
            // ===== POLICY 4: Payment Operations (Concurrency Limiter) =====
            // Limits concurrent payment operations
            // Only 10 concurrent payment requests per user
            options.AddPolicy("payment", context =>
            {
                var userId = context.User.Identity?.Name ?? 
                             context.User.FindFirst(UserIdClaimType)?.Value ?? 
                             context.Connection.RemoteIpAddress?.ToString() ?? 
                             AnonymousIdentifier;
                
                return RateLimitPartition.GetConcurrencyLimiter(
                    partitionKey: $"payment_{userId}",
                    factory: _ => new ConcurrencyLimiterOptions
                    {
                        PermitLimit = rateLimitConfig.GetValue("PaymentPolicy:PermitLimit", 10),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = rateLimitConfig.GetValue("PaymentPolicy:QueueLimit", 2)
                    });
            });
            
            // ===== POLICY 5: Read Operations (Higher Limits) =====
            // More permissive for GET requests
            // 200 requests per minute per user
            options.AddPolicy("read", context =>
            {
                var userId = context.User.Identity?.Name ?? 
                             context.User.FindFirst(UserIdClaimType)?.Value ?? 
                             context.Connection.RemoteIpAddress?.ToString() ?? 
                             AnonymousIdentifier;
                
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: $"read_{userId}",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitConfig.GetValue("ReadPolicy:PermitLimit", 200),
                        Window = TimeSpan.FromMinutes(rateLimitConfig.GetValue("ReadPolicy:WindowMinutes", 1)),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 10
                    });
            });
            
            // ===== POLICY 6: Premium User Policy (Higher Limits) =====
            // For future implementation of tiered access
            // 500 requests per minute for premium users
            options.AddPolicy("premium", context =>
            {
                var userId = context.User.Identity?.Name ?? 
                             context.User.FindFirst(UserIdClaimType)?.Value ?? 
                             AnonymousIdentifier;
                
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: $"premium_{userId}",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitConfig.GetValue("PremiumPolicy:PermitLimit", 500),
                        Window = TimeSpan.FromMinutes(rateLimitConfig.GetValue("PremiumPolicy:WindowMinutes", 1)),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 20
                    });
            });
        });
        
        Log.Information("Rate limiting configured with 6 policies: global, auth, booking, payment, read, premium");
        
        return services;
    }
}

/// <summary>
/// Middleware to add rate limit information headers to successful responses
/// </summary>
public class RateLimitHeadersMiddleware
{
    private readonly RequestDelegate _next;
    
    public RateLimitHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);
        
        // Add informational rate limit headers to successful responses
        if (context.Response.StatusCode < 400)
        {
            var endpoint = context.GetEndpoint();
            var rateLimitMetadata = endpoint?.Metadata.GetMetadata<EnableRateLimitingAttribute>();
            
            if ((rateLimitMetadata != null || context.Response.Headers.ContainsKey("X-RateLimit-Limit")) 
                && !context.Response.Headers.ContainsKey("X-RateLimit-Policy"))
            {
                // Headers were already set by rate limiter if limit was close to being exceeded
                // This is informational only
                context.Response.Headers["X-RateLimit-Policy"] = 
                    rateLimitMetadata?.PolicyName ?? "global";
            }
        }
    }
}

/// <summary>
/// Extension method to use rate limit headers middleware
/// </summary>
public static class RateLimitHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseRateLimitHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RateLimitHeadersMiddleware>();
    }
}
