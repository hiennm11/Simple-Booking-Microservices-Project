using Polly;
using Polly.Retry;
using Npgsql;

namespace InventoryService.Services;

/// <summary>
/// Service that provides pre-configured resilience pipelines for different operations
/// </summary>
public interface IResiliencePipelineService
{
    ResiliencePipeline GetEventPublishingPipeline();
    ResiliencePipeline GetDatabasePipeline();
}

public class ResiliencePipelineService : IResiliencePipelineService
{
    private readonly ResiliencePipeline _eventPublishingPipeline;
    private readonly ResiliencePipeline _databasePipeline;
    private readonly ILogger<ResiliencePipelineService> _logger;

    public ResiliencePipelineService(ILogger<ResiliencePipelineService> logger)
    {
        _logger = logger;
        _eventPublishingPipeline = CreateEventPublishingPipeline();
        _databasePipeline = CreateDatabasePipeline();
    }

    /// <summary>
    /// Creates a resilience pipeline for event publishing operations
    /// Uses exponential backoff with jitter to handle temporary RabbitMQ unavailability
    /// </summary>
    private ResiliencePipeline CreateEventPublishingPipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "Event publishing retry {Attempt}/{MaxAttempts} after {Delay}ms. " +
                        "Error: {ErrorType} - {ErrorMessage}",
                        args.AttemptNumber,
                        3,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.GetType().Name ?? "Unknown",
                        args.Outcome.Exception?.Message ?? "No message");
                    
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(10)) // Prevent hanging operations
            .Build();
    }

    /// <summary>
    /// Creates a resilience pipeline for database operations
    /// Handles PostgreSQL transient connection errors
    /// </summary>
    private ResiliencePipeline CreateDatabasePipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 5,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<TimeoutException>()
                    .Handle<NpgsqlException>(ex => ex.IsTransient)
                    .Handle<InvalidOperationException>(ex => 
                        ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase)),
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "Database operation retry {Attempt}/{MaxAttempts} after {Delay}ms. " +
                        "Error: {ErrorType}",
                        args.AttemptNumber,
                        5,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.GetType().Name ?? "Unknown");
                    
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(30))
            .Build();
    }

    public ResiliencePipeline GetEventPublishingPipeline() => _eventPublishingPipeline;
    public ResiliencePipeline GetDatabasePipeline() => _databasePipeline;
}
