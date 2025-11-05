using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using Shared.Contracts;
using PaymentService.EventBus;
using PaymentService.Services;
using PaymentService.DTOs;
using Polly;
using Polly.Retry;
using System.Net.Sockets;

namespace PaymentService.Consumers;

/// <summary>
/// Background service that consumes BookingCreated events from RabbitMQ
/// This enables automatic payment processing when a booking is created
/// </summary>
public class BookingCreatedConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMQSettings _settings;
    private readonly ILogger<BookingCreatedConsumer> _logger;
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly ResiliencePipeline _connectionPipeline;
    private IConnection? _connection;
    private IModel? _channel;
    private const int MAX_REQUEUE_ATTEMPTS = 3;
    private readonly Dictionary<ulong, int> _retryCountByDeliveryTag = new();

    public BookingCreatedConsumer(
        IServiceProvider serviceProvider,
        IOptions<RabbitMQSettings> settings,
        ILogger<BookingCreatedConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _settings = settings.Value;
        _logger = logger;
        
        // Create resilience pipeline for message processing
        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "Retrying BookingCreated message processing. Attempt {Attempt}/{MaxAttempts}",
                        args.AttemptNumber,
                        2);
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
                        "BookingCreatedConsumer RabbitMQ connection retry {Attempt}/{MaxAttempts} after {Delay}ms. " +
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
        _logger.LogInformation("BookingCreatedConsumer is starting");

        stoppingToken.Register(() =>
            _logger.LogInformation("BookingCreatedConsumer background task is stopping"));

        try
        {
            InitializeRabbitMQ();
            
            // Keep the consumer running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in BookingCreatedConsumer");
        }
    }

    private void InitializeRabbitMQ()
    {
        // Execute connection with retry policy
        _connectionPipeline.Execute(() =>
        {
            var factory = new ConnectionFactory
            {
                HostName = _settings.HostName,
                Port = _settings.Port,
                UserName = _settings.UserName,
                Password = _settings.Password,
                VirtualHost = _settings.VirtualHost,
                DispatchConsumersAsync = true,
                AutomaticRecoveryEnabled = true, // Enable automatic recovery
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
            };

            _connection = factory.CreateConnection();
            _logger.LogInformation("BookingCreatedConsumer established connection to RabbitMQ");
        });

        _channel = _connection!.CreateModel();

        var queueName = _settings.Queues.GetValueOrDefault("BookingCreated", "booking_created");
        
        _channel.QueueDeclare(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        _channel.BasicQos(0, 1, false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            await HandleMessageAsync(ea);
        };

        _channel.BasicConsume(
            queue: queueName,
            autoAck: false,
            consumer: consumer);

        _logger.LogInformation("BookingCreatedConsumer is listening to queue: {QueueName}", queueName);
    }

    private async Task HandleMessageAsync(BasicDeliverEventArgs ea)
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        var deliveryTag = ea.DeliveryTag;

        try
        {
            _logger.LogInformation("Received BookingCreated event: {Message}", message);

            var bookingEvent = JsonSerializer.Deserialize<BookingCreatedEvent>(message);
            
            if (bookingEvent?.Data == null)
            {
                _logger.LogWarning("Invalid BookingCreated event format. Rejecting message without requeue.");
                // Permanent failure - invalid message format
                _channel!.BasicNack(deliveryTag, false, requeue: false);
                return;
            }

            // Process with resilience pipeline
            await _resiliencePipeline.ExecuteAsync(async ct =>
            {
                await ProcessBookingCreatedAsync(bookingEvent);
            }, CancellationToken.None);

            // Success - acknowledge and clean up retry count
            _channel!.BasicAck(deliveryTag, false);
            _retryCountByDeliveryTag.Remove(deliveryTag);
            
            _logger.LogInformation("BookingCreated event processed successfully for BookingId: {BookingId}", 
                bookingEvent.Data.BookingId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing BookingCreated event: {Message}", message);
            
            // Increment retry count
            if (!_retryCountByDeliveryTag.ContainsKey(deliveryTag))
            {
                _retryCountByDeliveryTag[deliveryTag] = 0;
            }
            _retryCountByDeliveryTag[deliveryTag]++;
            
            var currentRetryCount = _retryCountByDeliveryTag[deliveryTag];
            
            if (currentRetryCount >= MAX_REQUEUE_ATTEMPTS)
            {
                // Max retries reached - reject without requeue (send to DLQ if configured)
                var bookingId = JsonSerializer.Deserialize<BookingCreatedEvent>(message)?.Data?.BookingId.ToString() ?? "Unknown";
                _logger.LogError(
                    "BookingCreated message failed after {Attempts} requeue attempts. Rejecting message for BookingId: {BookingId}",
                    MAX_REQUEUE_ATTEMPTS,
                    bookingId);
                
                _channel!.BasicNack(deliveryTag, false, requeue: false);
                _retryCountByDeliveryTag.Remove(deliveryTag);
            }
            else
            {
                // Requeue for retry with exponential backoff
                var bookingId = JsonSerializer.Deserialize<BookingCreatedEvent>(message)?.Data?.BookingId.ToString() ?? "Unknown";
                _logger.LogWarning(
                    "Requeuing BookingCreated message. Attempt {Attempt}/{Max} for BookingId: {BookingId}",
                    currentRetryCount,
                    MAX_REQUEUE_ATTEMPTS,
                    bookingId);
                
                // Add delay before requeue to implement exponential backoff
                var delayMs = (int)(Math.Pow(2, currentRetryCount) * 1000);
                await Task.Delay(delayMs);
                
                _channel!.BasicNack(deliveryTag, false, requeue: true);
            }
        }
    }

    private async Task ProcessBookingCreatedAsync(BookingCreatedEvent bookingEvent)
    {
        using var scope = _serviceProvider.CreateScope();
        var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();

        _logger.LogInformation("Automatically processing payment for BookingId: {BookingId}", 
            bookingEvent.Data.BookingId);

        // Create payment request from booking event
        var paymentRequest = new ProcessPaymentRequest
        {
            BookingId = bookingEvent.Data.BookingId,
            Amount = bookingEvent.Data.Amount,
            PaymentMethod = "CREDIT_CARD"
        };

        try
        {
            var paymentResult = await paymentService.ProcessPaymentAsync(paymentRequest);
            
            _logger.LogInformation("Payment processed for BookingId: {BookingId}, Status: {Status}, PaymentId: {PaymentId}",
                bookingEvent.Data.BookingId, paymentResult.Status, paymentResult.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process payment for BookingId: {BookingId}", 
                bookingEvent.Data.BookingId);
            throw; // Re-throw to trigger message requeue
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("BookingCreatedConsumer is stopping");
        
        _channel?.Close();
        _channel?.Dispose();
        _connection?.Close();
        _connection?.Dispose();

        return base.StopAsync(cancellationToken);
    }
}
