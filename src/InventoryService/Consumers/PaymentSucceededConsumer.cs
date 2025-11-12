using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using Shared.Contracts;
using Shared.EventBus;
using InventoryService.Data;
using InventoryService.Services;
using InventoryService.EventBus;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;
using System.Net.Sockets;
using Serilog.Context;

namespace InventoryService.Consumers;

public class PaymentSucceededConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMQSettings _settings;
    private readonly ILogger<PaymentSucceededConsumer> _logger;
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly ResiliencePipeline _connectionPipeline;
    private IConnection? _connection;
    private IChannel? _channel;
    private int _retryCount = 0;
    private readonly int _maxRequeueAttempts;

    public PaymentSucceededConsumer(
        IServiceProvider serviceProvider,
        IOptions<RabbitMQSettings> settings,
        ILogger<PaymentSucceededConsumer> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _settings = settings.Value;
        _logger = logger;
        _maxRequeueAttempts = configuration.GetValue<int>("MessageConsumer:MaxRequeueAttempts", 3);

        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    _logger.LogWarning("Retrying message processing. Attempt {Attempt}/{MaxAttempts}",
                        args.AttemptNumber, 3);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();

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
                    _logger.LogWarning("RabbitMQ connection retry {Attempt}/{MaxAttempts} after {Delay}ms",
                        args.AttemptNumber, 10, args.RetryDelay.TotalMilliseconds);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PaymentSucceededConsumer is starting");

        stoppingToken.Register(() =>
            _logger.LogInformation("PaymentSucceededConsumer is stopping"));

        try
        {
            await InitializeRabbitMQ();

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

    private async Task InitializeRabbitMQ()
    {
        await _connectionPipeline.ExecuteAsync(async ct =>
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
            _logger.LogInformation("PaymentSucceededConsumer established connection to RabbitMQ");
        }, CancellationToken.None);

        _channel = await _connection!.CreateChannelAsync();

        var queueName = _settings.Queues.GetValueOrDefault("PaymentSucceeded", "payment_succeeded");

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

        _logger.LogInformation("PaymentSucceededConsumer is listening to queue: {QueueName}", queueName);
    }

    private async Task HandleMessageAsync(BasicDeliverEventArgs ea)
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);

        try
        {
            _logger.LogInformation("Received PaymentSucceeded event: {Message}", message);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var paymentEvent = JsonSerializer.Deserialize<PaymentSucceededEvent>(message, options);

            if (paymentEvent?.Data == null)
            {
                _logger.LogWarning("Invalid PaymentSucceeded event format");
                await _channel!.BasicNackAsync(ea.DeliveryTag, false, requeue: false);
                return;
            }

            await _resiliencePipeline.ExecuteAsync(async ct =>
            {
                using (LogContext.PushProperty("CorrelationId", paymentEvent.CorrelationId))
                {
                    await ProcessPaymentSucceededAsync(paymentEvent);
                }
            }, CancellationToken.None);

            await _channel!.BasicAckAsync(ea.DeliveryTag, false);
            _retryCount = 0;

            _logger.LogInformation("PaymentSucceeded event processed successfully for BookingId: {BookingId}",
                paymentEvent.Data.BookingId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PaymentSucceeded event: {Message}", message);

            _retryCount++;

            if (_retryCount >= _maxRequeueAttempts)
            {
                _logger.LogError("Message failed after {Attempts} requeue attempts. Moving to DLQ.",
                    _maxRequeueAttempts);
                await _channel!.BasicNackAsync(ea.DeliveryTag, false, requeue: false);
                _retryCount = 0;
            }
            else
            {
                _logger.LogWarning("Requeuing message. Attempt {Attempt}/{Max}",
                    _retryCount, _maxRequeueAttempts);
                await _channel!.BasicNackAsync(ea.DeliveryTag, false, requeue: true);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("PaymentSucceededConsumer is stopping");

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

    private async Task ProcessPaymentSucceededAsync(PaymentSucceededEvent @event)
    {
        using var scope = _serviceProvider.CreateScope();
        var inventoryService = scope.ServiceProvider.GetRequiredService<IInventoryService>();

        _logger.LogInformation("Processing PaymentSucceeded event: BookingId={BookingId}, PaymentId={PaymentId}",
            @event.Data.BookingId, @event.Data.PaymentId);

        try
        {
            // Confirm the reservation (move from RESERVED to CONFIRMED)
            await inventoryService.ConfirmReservationAsync(@event.Data.BookingId);

            _logger.LogInformation("Successfully confirmed inventory reservation: BookingId={BookingId}",
                @event.Data.BookingId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to confirm inventory reservation for BookingId={BookingId}",
                @event.Data.BookingId);
            // This is not critical - the reservation is still valid
            // We can retry later or handle in a background job
        }
    }
}
