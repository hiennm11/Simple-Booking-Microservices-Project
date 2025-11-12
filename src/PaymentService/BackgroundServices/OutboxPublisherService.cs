using System.Text.Json;
using Microsoft.Extensions.Options;
using PaymentService.Data;
using PaymentService.EventBus;
using PaymentService.Services;
using Shared.EventBus;

namespace PaymentService.BackgroundServices;

/// <summary>
/// Background service that continuously polls the outbox collection and publishes pending events.
/// This ensures events are eventually published even if RabbitMQ was temporarily unavailable.
/// </summary>
public class OutboxPublisherService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxPublisherService> _logger;
    private readonly TimeSpan _pollingInterval;
    private readonly int _batchSize;
    private readonly int _maxRetries;

    public OutboxPublisherService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<OutboxPublisherService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        // Read configuration settings with defaults
        _pollingInterval = TimeSpan.FromSeconds(
            configuration.GetValue<int>("OutboxPublisher:PollingIntervalSeconds", 10));
        _batchSize = configuration.GetValue<int>("OutboxPublisher:BatchSize", 100);
        _maxRetries = configuration.GetValue<int>("OutboxPublisher:MaxRetries", 3);
        
        _logger.LogInformation(
            "OutboxPublisher configured: PollingInterval={PollingInterval}s, BatchSize={BatchSize}, MaxRetries={MaxRetries}",
            _pollingInterval.TotalSeconds,
            _batchSize,
            _maxRetries);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxPublisher background service started");

        // Wait a bit for the application to fully start
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OutboxPublisher background service");
            }

            // Wait before next polling cycle
            await Task.Delay(_pollingInterval, stoppingToken);
        }

        _logger.LogInformation("OutboxPublisher background service stopped");
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        
        var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService>();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        var rabbitMQSettings = scope.ServiceProvider.GetRequiredService<IOptions<RabbitMQSettings>>().Value;

        // Get unpublished messages
        var messages = await outboxService.GetUnpublishedMessagesAsync(_batchSize, cancellationToken);

        if (messages.Count == 0)
        {
            _logger.LogDebug("No unpublished messages in outbox");
            return;
        }

        _logger.LogInformation("Processing {Count} unpublished messages from outbox", messages.Count);

        foreach (var message in messages)
        {
            // Send messages that have exceeded max retry attempts to DLQ
            if (message.RetryCount >= _maxRetries)
            {
                _logger.LogError(
                    "Outbox message {MessageId} has exceeded max retries ({MaxRetries}). Moving to Dead Letter Queue.",
                    message.Id,
                    _maxRetries);
                
                try
                {
                    await SendToDeadLetterQueueAsync(message, scope, cancellationToken);
                    
                    // Mark as published to remove from outbox (it's now in DLQ)
                    await outboxService.MarkAsPublishedAsync(message.Id, cancellationToken);
                    
                    _logger.LogInformation(
                        "Outbox message {MessageId} moved to DLQ and removed from outbox",
                        message.Id);
                }
                catch (Exception dlqEx)
                {
                    _logger.LogError(
                        dlqEx,
                        "Failed to move outbox message {MessageId} to DLQ. Will retry next cycle.",
                        message.Id);
                }
                
                continue;
            }

            try
            {
                // Determine queue name based on event type
                var queueName = rabbitMQSettings.Queues.GetValueOrDefault(
                    message.EventType, 
                    message.EventType.ToLower().Replace("event", ""));

                // Deserialize and publish the event
                var eventObject = JsonSerializer.Deserialize<object>(message.Payload);
                
                await eventBus.PublishAsync(
                    eventObject!, 
                    queueName, 
                    cancellationToken);

                // Mark as published
                await outboxService.MarkAsPublishedAsync(message.Id, cancellationToken);

                _logger.LogInformation(
                    "Successfully published outbox message {MessageId}, EventType: {EventType}, Queue: {Queue}",
                    message.Id,
                    message.EventType,
                    queueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to publish outbox message {MessageId}, EventType: {EventType}, Attempt: {AttemptCount}",
                    message.Id,
                    message.EventType,
                    message.RetryCount + 1);

                // Mark as failed and increment retry count
                await outboxService.MarkAsFailedAsync(
                    message.Id, 
                    ex.Message, 
                    cancellationToken);
            }
        }
    }

    private async Task SendToDeadLetterQueueAsync(
        Models.OutboxMessage message,
        IServiceScope scope,
        CancellationToken cancellationToken)
    {
        var mongoDbContext = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
        
        // Create a dead letter message record
        var deadLetterMessage = new Models.DeadLetterMessage
        {
            SourceQueue = "outbox_" + message.EventType.ToLower(),
            EventType = message.EventType,
            Payload = message.Payload,
            ErrorMessage = message.LastError ?? "Failed after max retry attempts in Outbox Publisher",
            StackTrace = null, // Outbox doesn't store stack traces
            AttemptCount = message.RetryCount,
            FirstAttemptAt = message.CreatedAt,
            FailedAt = DateTime.UtcNow,
            Resolved = false
        };
        
        await mongoDbContext.DeadLetterMessages.InsertOneAsync(deadLetterMessage, cancellationToken: cancellationToken);
        
        _logger.LogInformation(
            "Outbox message {MessageId} stored in Dead Letter collection with ID {DLQId}",
            message.Id,
            deadLetterMessage.Id);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("OutboxPublisher is stopping - processing remaining messages...");
        
        // Try to process any remaining messages before shutdown
        try
        {
            await ProcessOutboxMessagesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing messages during shutdown");
        }

        await base.StopAsync(cancellationToken);
    }
}
