using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using Shared.Contracts;
using BookingService.EventBus;
using BookingService.Data;
using BookingService.Services;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;
using System.Net.Sockets;
using Serilog.Context;

namespace BookingService.Consumers;

/// <summary>
/// Background service that consumes InventoryReservationFailed events from RabbitMQ
/// and cancels the booking when inventory reservation fails
/// </summary>
public class InventoryReservationFailedConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMQSettings _settings;
    private readonly ILogger<InventoryReservationFailedConsumer> _logger;
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly ResiliencePipeline _connectionPipeline;
    private IConnection? _connection;
    private IChannel? _channel;
    private int _retryCount = 0;
    private readonly int _maxRequeueAttempts;

    public InventoryReservationFailedConsumer(
        IServiceProvider serviceProvider,
        IOptions<RabbitMQSettings> settings,
        ILogger<InventoryReservationFailedConsumer> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _settings = settings.Value;
        _logger = logger;
        _maxRequeueAttempts = configuration.GetValue<int>("MessageConsumer:MaxRequeueAttempts", 3);
        
        _logger.LogInformation(
            "InventoryReservationFailedConsumer configured with MaxRequeueAttempts: {MaxAttempts}",
            _maxRequeueAttempts);

        // Create inline resilience pipeline for message processing
        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "Retrying message processing. Attempt {Attempt}/{MaxAttempts}",
                        args.AttemptNumber, 3);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();

        // Create resilience pipeline for RabbitMQ connection establishment
        _connectionPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 10,
                Delay = TimeSpan.FromSeconds(5),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                MaxDelay = TimeSpan.FromSeconds(60),
                ShouldHandle = new PredicateBuilder()
                    .Handle<BrokerUnreachableException>()
                    .Handle<SocketException>()
                    .Handle<TimeoutException>(),
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "InventoryReservationFailedConsumer RabbitMQ connection retry {Attempt}/{MaxAttempts} after {Delay}ms. " +
                        "Waiting for RabbitMQ to become available...",
                        args.AttemptNumber,
                        10,
                        args.RetryDelay.TotalMilliseconds);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("InventoryReservationFailedConsumer is starting");

        stoppingToken.Register(() =>
            _logger.LogInformation("InventoryReservationFailedConsumer background task is stopping"));

        try
        {
            await InitializeRabbitMQ();

            // Keep the consumer running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in InventoryReservationFailedConsumer");
        }
    }

    private async Task InitializeRabbitMQ()
    {
        // Execute connection with retry policy
        await _connectionPipeline.Execute(async () =>
        {
            var factory = new ConnectionFactory
            {
                HostName = _settings.HostName,
                Port = _settings.Port,
                UserName = _settings.UserName,
                Password = _settings.Password,
                VirtualHost = _settings.VirtualHost,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
            };

            _connection = await factory.CreateConnectionAsync();
            _logger.LogInformation("InventoryReservationFailedConsumer established connection to RabbitMQ");
        });

        _channel = await _connection!.CreateChannelAsync();

        var queueName = _settings.Queues.GetValueOrDefault("InventoryReservationFailed", "inventory_reservation_failed");

        await _channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        await _channel.BasicQosAsync(0, 1, false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            await HandleMessageAsync(ea);
        };

        await _channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer);

        _logger.LogInformation("InventoryReservationFailedConsumer is listening to queue: {QueueName}", queueName);
    }

    private async Task HandleMessageAsync(BasicDeliverEventArgs ea)
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);

        try
        {
            _logger.LogInformation("Received InventoryReservationFailed event: {Message}", message);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var failedEvent = JsonSerializer.Deserialize<InventoryReservationFailedEvent>(message, options);

            if (failedEvent?.Data == null)
            {
                _logger.LogWarning("Invalid InventoryReservationFailed event format");
                // ❌ Permanent failure - don't requeue
                await _channel!.BasicNackAsync(ea.DeliveryTag, false, requeue: false);
                return;
            }

            // ✅ Process with retry policy
            await _resiliencePipeline.ExecuteAsync(async ct =>
            {
                await ProcessInventoryReservationFailedAsync(failedEvent);
            }, CancellationToken.None);

            // ✅ Success - acknowledge
            await _channel!.BasicAckAsync(ea.DeliveryTag, false);
            _retryCount = 0; // Reset counter

            _logger.LogInformation("InventoryReservationFailed event processed successfully for BookingId: {BookingId}",
                failedEvent.Data.BookingId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing InventoryReservationFailed event: {Message}", message);

            _retryCount++;

            if (_retryCount >= _maxRequeueAttempts)
            {
                // ❌ Max retries reached - send to dead letter queue or log
                _logger.LogError(
                    "Message failed after {Attempts} requeue attempts. Moving to DLQ.",
                    _maxRequeueAttempts);

                await _channel!.BasicNackAsync(ea.DeliveryTag, false, requeue: false);
                _retryCount = 0;
            }
            else
            {
                // ⚠️ Requeue for retry
                _logger.LogWarning("Requeuing message. Attempt {Attempt}/{Max}",
                    _retryCount, _maxRequeueAttempts);

                await _channel!.BasicNackAsync(ea.DeliveryTag, false, requeue: true);
            }
        }
    }

    private async Task ProcessInventoryReservationFailedAsync(InventoryReservationFailedEvent failedEvent)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService>();

        // Push correlation ID from event to log context
        using (LogContext.PushProperty("CorrelationId", failedEvent.CorrelationId))
        {
            _logger.LogInformation(
                "Processing InventoryReservationFailed event for BookingId: {BookingId}, Reason: {Reason}",
                failedEvent.Data.BookingId,
                failedEvent.Data.Reason);

            var booking = await dbContext.Bookings
                .FirstOrDefaultAsync(b => b.Id == failedEvent.Data.BookingId);

            if (booking == null)
            {
                _logger.LogWarning("Booking not found with ID: {BookingId}", failedEvent.Data.BookingId);
                return;
            }

            _logger.LogInformation(
                "Found booking {BookingId} with current status: {Status}",
                booking.Id,
                booking.Status);

            // Idempotency check - don't cancel if already in final state
            if (booking.Status == "CANCELLED" || booking.Status == "CONFIRMED")
            {
                _logger.LogInformation(
                    "Booking {BookingId} is already in final state: {Status}. Skipping cancellation.",
                    booking.Id,
                    booking.Status);
                return;
            }

            // Update booking status to CANCELLED
            booking.Status = "CANCELLED";
            booking.CancellationReason = $"Inventory reservation failed: {failedEvent.Data.Reason}";
            booking.CancelledAt = DateTime.UtcNow;
            booking.UpdatedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Cancelling booking {BookingId} due to inventory reservation failure: {Reason}",
                booking.Id,
                failedEvent.Data.Reason);

            // Publish BookingCancelledEvent via Outbox for downstream services (optional - for audit/notifications)
            var cancelledEvent = new BookingCancelledEvent
            {
                CorrelationId = failedEvent.CorrelationId,
                Data = new BookingCancelledData
                {
                    BookingId = booking.Id,
                    UserId = booking.UserId,
                    RoomId = booking.RoomId,
                    Reason = booking.CancellationReason,
                    CancelledAt = booking.CancelledAt ?? DateTime.UtcNow
                }
            };

            await outboxService.AddToOutboxAsync(cancelledEvent, "BookingCancelled");

            // Save both booking update and outbox message in same transaction
            await dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "Booking {BookingId} cancelled. Reason: {Reason}",
                booking.Id,
                booking.CancellationReason);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("InventoryReservationFailedConsumer is stopping");

        if (_channel != null)
        {
            await _channel.CloseAsync();
            await _channel.DisposeAsync();
        }

        if (_connection != null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}
