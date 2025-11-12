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
using InventoryService.DTOs;
using InventoryService.EventBus;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;
using System.Net.Sockets;
using Serilog.Context;

namespace InventoryService.Consumers;

/// <summary>
/// Background service that consumes BookingCreated events from RabbitMQ
/// </summary>
public class BookingCreatedConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMQSettings _settings;
    private readonly ILogger<BookingCreatedConsumer> _logger;
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly ResiliencePipeline _connectionPipeline;
    private IConnection? _connection;
    private IChannel? _channel;
    private int _retryCount = 0;
    private readonly int _maxRequeueAttempts;

    public BookingCreatedConsumer(
        IServiceProvider serviceProvider,
        IOptions<RabbitMQSettings> settings,
        ILogger<BookingCreatedConsumer> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _settings = settings.Value;
        _logger = logger;
        _maxRequeueAttempts = configuration.GetValue<int>("MessageConsumer:MaxRequeueAttempts", 3);

        // Create resilience pipeline for message processing
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

        // Create resilience pipeline for RabbitMQ connection
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
        _logger.LogInformation("BookingCreatedConsumer is starting");

        stoppingToken.Register(() =>
            _logger.LogInformation("BookingCreatedConsumer is stopping"));

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
            _logger.LogError(ex, "Error in BookingCreatedConsumer");
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
            _logger.LogInformation("BookingCreatedConsumer established connection to RabbitMQ");
        }, CancellationToken.None);

        _channel = await _connection!.CreateChannelAsync();

        var queueName = _settings.Queues.GetValueOrDefault("BookingCreated", "booking_created");

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

        _logger.LogInformation("BookingCreatedConsumer is listening to queue: {QueueName}", queueName);
    }

    private async Task HandleMessageAsync(BasicDeliverEventArgs ea)
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);

        try
        {
            _logger.LogInformation("Received BookingCreated event: {Message}", message);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var bookingEvent = JsonSerializer.Deserialize<BookingCreatedEvent>(message, options);

            if (bookingEvent?.Data == null)
            {
                _logger.LogWarning("Invalid BookingCreated event format");
                await _channel!.BasicNackAsync(ea.DeliveryTag, false, requeue: false);
                return;
            }

            // Process with retry policy
            await _resiliencePipeline.ExecuteAsync(async ct =>
            {
                using (LogContext.PushProperty("CorrelationId", bookingEvent.CorrelationId))
                {
                    await ProcessBookingCreatedAsync(bookingEvent);
                }
            }, CancellationToken.None);

            await _channel!.BasicAckAsync(ea.DeliveryTag, false);
            _retryCount = 0;

            _logger.LogInformation("BookingCreated event processed successfully for BookingId: {BookingId}",
                bookingEvent.Data.BookingId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing BookingCreated event: {Message}", message);

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
        _logger.LogInformation("BookingCreatedConsumer is stopping");

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

    private async Task ProcessBookingCreatedAsync(BookingCreatedEvent @event)
    {
        using var scope = _serviceProvider.CreateScope();
        var inventoryService = scope.ServiceProvider.GetRequiredService<IInventoryService>();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        _logger.LogInformation("Processing BookingCreated event: BookingId={BookingId}, RoomId={RoomId}",
            @event.Data.BookingId, @event.Data.RoomId);

        try
        {
            // Reserve inventory for the booking
            var reserveRequest = new ReserveInventoryRequest
            {
                BookingId = @event.Data.BookingId,
                ItemId = @event.Data.RoomId,
                Quantity = 1
            };

            var reservation = await inventoryService.ReserveAsync(reserveRequest);

            _logger.LogInformation("Successfully reserved inventory: ReservationId={ReservationId}, BookingId={BookingId}",
                reservation.ReservationId, @event.Data.BookingId);

            // Publish InventoryReservedEvent
            var inventoryReservedEvent = new InventoryReservedEvent
            {
                CorrelationId = @event.CorrelationId,
                Data = new InventoryReservedData
                {
                    ReservationId = reservation.ReservationId,
                    BookingId = reservation.BookingId,
                    ItemId = reservation.ItemId,
                    Quantity = reservation.Quantity,
                    Status = reservation.Status,
                    ExpiresAt = reservation.ExpiresAt
                }
            };

            await eventBus.PublishAsync(inventoryReservedEvent, "inventory_reserved");

            _logger.LogInformation("Published InventoryReservedEvent: BookingId={BookingId}",
                @event.Data.BookingId);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Insufficient inventory") || 
                                                     ex.Message.Contains("not found"))
        {
            // Gracefully handle inventory issues - publish failure event instead of throwing
            _logger.LogWarning("Inventory reservation failed for BookingId={BookingId}, Reason={Reason}",
                @event.Data.BookingId, ex.Message);

            // Publish InventoryReservationFailedEvent
            var failedEvent = new InventoryReservationFailedEvent
            {
                CorrelationId = @event.CorrelationId,
                Data = new InventoryReservationFailedData
                {
                    BookingId = @event.Data.BookingId,
                    ItemId = @event.Data.RoomId,
                    Reason = ex.Message
                }
            };

            await eventBus.PublishAsync(failedEvent, "inventory_reservation_failed");

            _logger.LogInformation("Published InventoryReservationFailedEvent: BookingId={BookingId}",
                @event.Data.BookingId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while reserving inventory for BookingId={BookingId}",
                @event.Data.BookingId);
            throw;
        }
    }
}
