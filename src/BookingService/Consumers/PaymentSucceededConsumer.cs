using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Contracts;
using BookingService.EventBus;
using BookingService.Data;
using BookingService.Services;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;

namespace BookingService.Consumers;

/// <summary>
/// Background service that consumes PaymentSucceeded events from RabbitMQ
/// </summary>
public class PaymentSucceededConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMQSettings _settings;
    private readonly ILogger<PaymentSucceededConsumer> _logger;
    private readonly ResiliencePipeline _resiliencePipeline;
    private IConnection? _connection;
    private IModel? _channel;
    private int _retryCount = 0;
    private const int MAX_REQUEUE_ATTEMPTS = 3;

    public PaymentSucceededConsumer(
        IServiceProvider serviceProvider,
        IOptions<RabbitMQSettings> settings,
        ILogger<PaymentSucceededConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _settings = settings.Value;
        _logger = logger;
        
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
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PaymentSucceededConsumer is starting");

        stoppingToken.Register(() =>
            _logger.LogInformation("PaymentSucceededConsumer background task is stopping"));

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
            _logger.LogError(ex, "Error in PaymentSucceededConsumer");
        }
    }

    private void InitializeRabbitMQ()
    {
        var factory = new ConnectionFactory
        {
            HostName = _settings.HostName,
            Port = _settings.Port,
            UserName = _settings.UserName,
            Password = _settings.Password,
            VirtualHost = _settings.VirtualHost,
            DispatchConsumersAsync = true
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        var queueName = _settings.Queues.GetValueOrDefault("PaymentSucceeded", "payment_succeeded");
        
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

        _logger.LogInformation("PaymentSucceededConsumer is listening to queue: {QueueName}", queueName);
    }

    private async Task HandleMessageAsync(BasicDeliverEventArgs ea)
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);

        try
        {
            _logger.LogInformation("Received PaymentSucceeded event: {Message}", message);

            var paymentEvent = JsonSerializer.Deserialize<PaymentSucceededEvent>(message);
            
            if (paymentEvent?.Data == null)
            {
                _logger.LogWarning("Invalid PaymentSucceeded event format");
                // ❌ Permanent failure - don't requeue
                _channel!.BasicNack(ea.DeliveryTag, false, requeue: false);
                return;
            }

            // ✅ Process with retry policy
            await _resiliencePipeline.ExecuteAsync(async ct =>
            {
                await ProcessPaymentSucceededAsync(paymentEvent);
            }, CancellationToken.None);

            // ✅ Success - acknowledge
            _channel!.BasicAck(ea.DeliveryTag, false);
            _retryCount = 0; // Reset counter
            
            _logger.LogInformation("PaymentSucceeded event processed successfully for BookingId: {BookingId}", 
                paymentEvent.Data.BookingId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PaymentSucceeded event: {Message}", message);
            
            _retryCount++;
            
            if (_retryCount >= MAX_REQUEUE_ATTEMPTS)
            {
                // ❌ Max retries reached - send to dead letter queue or log
                _logger.LogError(
                    "Message failed after {Attempts} requeue attempts. Moving to DLQ.",
                    MAX_REQUEUE_ATTEMPTS);
                
                _channel!.BasicNack(ea.DeliveryTag, false, requeue: false);
                _retryCount = 0;
                
                // TODO: Store in dead-letter table for manual investigation
            }
            else
            {
                // ⚠️ Requeue for retry
                _logger.LogWarning("Requeuing message. Attempt {Attempt}/{Max}",
                    _retryCount, MAX_REQUEUE_ATTEMPTS);
                
                _channel!.BasicNack(ea.DeliveryTag, false, requeue: true);
            }
        }
    }

    private async Task ProcessPaymentSucceededAsync(PaymentSucceededEvent paymentEvent)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        var booking = await dbContext.Bookings
            .FirstOrDefaultAsync(b => b.Id == paymentEvent.Data.BookingId);

        if (booking == null)
        {
            _logger.LogWarning("Booking not found with ID: {BookingId}", paymentEvent.Data.BookingId);
            return;
        }

        if (booking.Status == "CONFIRMED")
        {
            _logger.LogInformation("Booking {BookingId} is already confirmed. Skipping update.", booking.Id);
            return;
        }

        // Update booking status to CONFIRMED
        booking.Status = "CONFIRMED";
        booking.ConfirmedAt = DateTime.UtcNow;
        booking.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        _logger.LogInformation("Booking {BookingId} status updated to CONFIRMED", booking.Id);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("PaymentSucceededConsumer is stopping");
        
        _channel?.Close();
        _channel?.Dispose();
        _connection?.Close();
        _connection?.Dispose();

        return base.StopAsync(cancellationToken);
    }
}
