using Microsoft.AspNetCore.Builder;
using Shared.Middleware;

namespace Shared.Extensions;

/// <summary>
/// Extension methods for registering global exception handling middleware
/// </summary>
public static class ExceptionHandlingExtensions
{
    /// <summary>
    /// Adds global exception handling middleware to the application pipeline
    /// </summary>
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    }
}
