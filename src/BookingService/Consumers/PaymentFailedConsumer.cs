using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using Shared.Contracts;
using BookingService.EventBus;
using BookingService.Data;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;
using System.Net.Sockets;
using System.Threading.Tasks;
using Serilog.Context;

namespace BookingService.Consumers;

/// <summary>
/// Background service that consumes PaymentFailed events from RabbitMQ
/// </summary>
public class PaymentFailedConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMQSettings _settings;
    private readonly ILogger<PaymentFailedConsumer> _logger;
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly ResiliencePipeline _connectionPipeline;
    private IConnection? _connection;
    private IChannel? _channel;

    public PaymentFailedConsumer(
        IServiceProvider serviceProvider,
        IOptions<RabbitMQSettings> settings,
        ILogger<PaymentFailedConsumer> logger,
        IConfiguration configuration)
    { 
        _serviceProvider = serviceProvider;
        _settings = settings.Value;
        _logger = logger;

        var maxRetryAttempts = configuration.GetValue<int>("MessageConsumer:MaxRetryAttempts", 3);
        
        _logger.LogInformation(
            "PaymentFailedConsumer configured with MaxRetryAttempts: {MaxAttempts}",
            maxRetryAttempts);

        // Create inline resilience pipeline for message processing
        // This handles all retries - if it fails after all retries, message goes to DLQ
        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = maxRetryAttempts,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "Retrying message processing. Attempt {Attempt}/{MaxAttempts}",
                        args.AttemptNumber, maxRetryAttempts);
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
                        "PaymentFailedConsumer RabbitMQ connection retry {Attempt}/{MaxAttempts} after {Delay}ms. " +
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
        _logger.LogInformation("PaymentFailedConsumer is starting");

        stoppingToken.Register(() =>
            _logger.LogInformation("PaymentFailedConsumer background task is stopping"));

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
            _logger.LogError(ex, "Error in PaymentFailedConsumer");
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
                AutomaticRecoveryEnabled = true, // Enable automatic recovery
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
            };

            _connection = await factory.CreateConnectionAsync();
            _logger.LogInformation("PaymentFailedConsumer established connection to RabbitMQ");
        });

        _channel = await _connection!.CreateChannelAsync();

        var queueName = _settings.Queues.GetValueOrDefault("PaymentFailed", "payment_failed");

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

        _logger.LogInformation("PaymentFailedConsumer is listening to queue: {QueueName}", queueName);
    }

    private async Task HandleMessageAsync(BasicDeliverEventArgs ea)
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        var deliveryTag = ea.DeliveryTag;
        var firstAttemptTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Received PaymentFailed event: {Message}", message);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var paymentEvent = JsonSerializer.Deserialize<PaymentFailedEvent>(message, options);

            if (paymentEvent == null)
            {
                _logger.LogWarning("Invalid PaymentFailed event format - sending to DLQ");
                // ❌ Permanent failure - send to DLQ
                await SendToDeadLetterQueueAsync(message, "Invalid event format", null, firstAttemptTime, 0);
                await _channel!.BasicAckAsync(deliveryTag, false);
                return;
            }

            // ✅ Process with retry policy (resilience pipeline handles all retries)
            await _resiliencePipeline.ExecuteAsync(async ct =>
            {
                await ProcessPaymentFailedAsync(paymentEvent);
            }, CancellationToken.None);

            // ✅ Success - acknowledge
            await _channel!.BasicAckAsync(deliveryTag, false);

            _logger.LogInformation("PaymentFailed event processed successfully for BookingId: {BookingId}",
                paymentEvent.Data.BookingId);
        }
        catch (Exception ex)
        {
            // ❌ Failed after all resilience pipeline retries - send to DLQ
            _logger.LogError(ex, 
                "PaymentFailed event processing failed after all retry attempts. Moving to DLQ. Message: {Message}", 
                message);

            await SendToDeadLetterQueueAsync(message, ex.Message, ex.StackTrace, firstAttemptTime, 3);
            
            // Acknowledge to remove from queue (already sent to DLQ)
            await _channel!.BasicAckAsync(deliveryTag, false);
        }
    }

    private async Task SendToDeadLetterQueueAsync(
        string message,
        string errorMessage,
        string? stackTrace,
        DateTime firstAttemptTime,
        int retryCount)
    {
        try
        {
            var dlqName = _settings.Queues.GetValueOrDefault("PaymentFailed", "payment_failed") + "_dlq";
            
            // Ensure DLQ exists
            await _channel!.QueueDeclareAsync(
                queue: dlqName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            // Create properties with metadata
            var properties = new BasicProperties
            {
                Persistent = true,
                Headers = new Dictionary<string, object?>
                {
                    ["x-retry-count"] = retryCount,
                    ["x-first-attempt"] = firstAttemptTime.ToString("O"),
                    ["x-error-message"] = errorMessage,
                    ["x-stack-trace"] = stackTrace ?? "",
                    ["x-original-queue"] = _settings.Queues.GetValueOrDefault("PaymentFailed", "payment_failed"),
                    ["x-failed-at"] = DateTime.UtcNow.ToString("O")
                }
            };

            // Publish to DLQ
            await _channel.BasicPublishAsync(
                exchange: "",
                routingKey: dlqName,
                mandatory: false,
                basicProperties: properties,
                body: Encoding.UTF8.GetBytes(message));

            _logger.LogInformation(
                "Message sent to Dead Letter Queue: {DLQName}, Error: {Error}, RetryCount: {RetryCount}",
                dlqName,
                errorMessage,
                retryCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to Dead Letter Queue");
        }
    }

    private async Task ProcessPaymentFailedAsync(PaymentFailedEvent paymentEvent)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        // Push correlation ID from event to log context
        using (LogContext.PushProperty("CorrelationId", paymentEvent.CorrelationId))
        {
            _logger.LogInformation(
                "Processing PaymentFailed event for BookingId: {BookingId}, Reason: {Reason}", 
                paymentEvent.Data.BookingId, 
                paymentEvent.Data.Reason);

            var booking = await dbContext.Bookings
                .FirstOrDefaultAsync(b => b.Id == paymentEvent.Data.BookingId);

            if (booking == null)
            {
                _logger.LogWarning("Booking not found with ID: {BookingId}", paymentEvent.Data.BookingId);
                return;
            }

            _logger.LogInformation("Found booking {BookingId} with current status: {Status}", booking.Id, booking.Status);

            if (booking.Status == "CANCELLED")
            {
                _logger.LogInformation("Booking {BookingId} is already cancelled. Skipping update.", booking.Id);
                return;
            }

            // Update booking status to CANCELLED due to payment failure
            booking.Status = "CANCELLED";
            booking.CancellationReason = $"Payment failed: {paymentEvent.Data.Reason}";
            booking.CancelledAt = DateTime.UtcNow;
            booking.UpdatedAt = DateTime.UtcNow;

            _logger.LogInformation("Updating booking {BookingId} status to CANCELLED due to payment failure", booking.Id);

            await dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "Booking {BookingId} status updated to CANCELLED. Reason: {Reason}", 
                booking.Id, 
                booking.CancellationReason);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("PaymentFailedConsumer is stopping");

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
