using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using Shared.EventBus;
using Microsoft.Extensions.Options;

namespace BookingService.EventBus;

/// <summary>
/// RabbitMQ implementation of IEventBus for publishing events
/// </summary>
public class RabbitMQEventBus : IEventBus, IDisposable
{
    private readonly RabbitMQSettings _settings;
    private readonly ILogger<RabbitMQEventBus> _logger;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly object _lock = new();
    private bool _disposed;

    public RabbitMQEventBus(IOptions<RabbitMQSettings> settings, ILogger<RabbitMQEventBus> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    private void EnsureConnection()
    {
        if (_channel != null && _channel.IsOpen)
            return;

        lock (_lock)
        {
            if (_connection == null || !_connection.IsOpen)
            {
                var factory = new ConnectionFactory
                {
                    HostName = _settings.HostName,
                    Port = _settings.Port,
                    UserName = _settings.UserName,
                    Password = _settings.Password,
                    VirtualHost = _settings.VirtualHost
                };

                _connection = factory.CreateConnection();
                _logger.LogInformation("RabbitMQ connection established to {HostName}:{Port}", 
                    _settings.HostName, _settings.Port);
            }

            if (_channel == null || !_channel.IsOpen)
            {
                _channel = _connection.CreateModel();
                _logger.LogInformation("RabbitMQ channel created");
            }
        }
    }

    public Task PublishAsync<T>(T @event, string queueName, CancellationToken cancellationToken = default) where T : class
    {
        EnsureConnection();

        if (_channel == null)
        {
            throw new InvalidOperationException("RabbitMQ channel is not available");
        }

        try
        {
            // Declare queue (idempotent operation)
            _channel.QueueDeclare(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var message = JsonSerializer.Serialize(@event);
            var body = Encoding.UTF8.GetBytes(message);

            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.DeliveryMode = 2; // Persistent

            _channel.BasicPublish(
                exchange: string.Empty,
                routingKey: queueName,
                basicProperties: properties,
                body: body);

            _logger.LogInformation("Event published to queue {QueueName}: {EventType}", 
                queueName, typeof(T).Name);
            
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing event to queue {QueueName}", queueName);
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _channel?.Close();
        _channel?.Dispose();
        _connection?.Close();
        _connection?.Dispose();
        _disposed = true;
        
        _logger.LogInformation("RabbitMQ connection disposed");
    }
}

/// <summary>
/// RabbitMQ configuration settings
/// </summary>
public class RabbitMQSettings
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public Dictionary<string, string> Queues { get; set; } = new();
}
