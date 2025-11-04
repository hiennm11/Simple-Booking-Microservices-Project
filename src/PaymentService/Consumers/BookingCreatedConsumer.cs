using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Contracts;
using PaymentService.EventBus;
using PaymentService.Services;
using PaymentService.DTOs;

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
    private IConnection? _connection;
    private IModel? _channel;

    public BookingCreatedConsumer(
        IServiceProvider serviceProvider,
        IOptions<RabbitMQSettings> settings,
        ILogger<BookingCreatedConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _settings = settings.Value;
        _logger = logger;
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

        try
        {
            _logger.LogInformation("Received BookingCreated event: {Message}", message);

            var bookingEvent = JsonSerializer.Deserialize<BookingCreatedEvent>(message);
            
            if (bookingEvent?.Data == null)
            {
                _logger.LogWarning("Invalid BookingCreated event format");
                _channel!.BasicNack(ea.DeliveryTag, false, false);
                return;
            }

            // Process the booking event - automatically trigger payment
            await ProcessBookingCreatedAsync(bookingEvent);

            // Acknowledge the message
            _channel!.BasicAck(ea.DeliveryTag, false);
            _logger.LogInformation("BookingCreated event processed successfully for BookingId: {BookingId}", 
                bookingEvent.Data.BookingId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing BookingCreated event: {Message}", message);
            
            // Reject and requeue the message
            _channel!.BasicNack(ea.DeliveryTag, false, true);
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
