using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Services;

/// <summary>
/// Service interface for accessing correlation ID
/// </summary>
public interface ICorrelationIdAccessor
{
    /// <summary>
    /// Gets the current correlation ID from the HTTP context
    /// </summary>
    /// <returns>The correlation ID, or null if not available</returns>
    string? GetCorrelationId();
    
    /// <summary>
    /// Gets the current correlation ID as a Guid
    /// </summary>
    /// <returns>The correlation ID as Guid, or Guid.Empty if not available or invalid</returns>
    Guid GetCorrelationIdAsGuid();
}

/// <summary>
/// Implementation of correlation ID accessor using HttpContext
/// </summary>
public class CorrelationIdAccessor : ICorrelationIdAccessor
{
    private const string CorrelationIdKey = "CorrelationId";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetCorrelationId()
    {
        return _httpContextAccessor.HttpContext?.Items[CorrelationIdKey]?.ToString();
    }

    public Guid GetCorrelationIdAsGuid()
    {
        var correlationId = GetCorrelationId();
        
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return Guid.Empty;
        }

        return Guid.TryParse(correlationId, out var guid) ? guid : Guid.Empty;
    }
}

/// <summary>
/// Extension methods for registering correlation ID services
/// </summary>
public static class CorrelationIdServiceExtensions
{
    /// <summary>
    /// Adds correlation ID accessor service to the DI container
    /// Also registers IHttpContextAccessor which is required for the accessor to work
    /// </summary>
    public static IServiceCollection AddCorrelationId(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICorrelationIdAccessor, CorrelationIdAccessor>();
        return services;
    }
}
