using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Shared.EventBus;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using System.Net.Sockets;

namespace PaymentService.EventBus;

/// <summary>
/// RabbitMQ implementation of IEventBus for publishing events
/// Includes connection retry logic with exponential backoff
/// </summary>
public class RabbitMQEventBus : IEventBus, IDisposable
{
    private readonly RabbitMQSettings _settings;
    private readonly ILogger<RabbitMQEventBus> _logger;
    private readonly ResiliencePipeline _connectionPipeline;
    private IConnection? _connection;
    private IChannel? _channel;
    private readonly object _lock = new();
    private bool _disposed;

    public RabbitMQEventBus(IOptions<RabbitMQSettings> settings, ILogger<RabbitMQEventBus> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        _connectionPipeline = CreateConnectionResiliencePipeline();
    }

    /// <summary>
    /// Creates a resilience pipeline specifically for RabbitMQ connection establishment
    /// Uses aggressive retry strategy to wait for RabbitMQ to become available
    /// </summary>
    private ResiliencePipeline CreateConnectionResiliencePipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 10,
                Delay = TimeSpan.FromSeconds(5),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                MaxDelay = TimeSpan.FromSeconds(60), // Cap maximum delay at 60 seconds
                ShouldHandle = new PredicateBuilder()
                    .Handle<BrokerUnreachableException>()
                    .Handle<SocketException>()
                    .Handle<TimeoutException>(),
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "RabbitMQ connection retry {Attempt}/{MaxAttempts} after {Delay}ms. " +
                        "Error: {ErrorType} - {ErrorMessage}. Waiting for RabbitMQ to become available...",
                        args.AttemptNumber,
                        10,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.GetType().Name ?? "Unknown",
                        args.Outcome.Exception?.Message ?? "No message");
                    
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    private void EnsureConnection()
    {
        if (_channel != null && _channel.IsOpen)
            return;

        lock (_lock)
        {
            if (_connection == null || !_connection.IsOpen)
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
                        AutomaticRecoveryEnabled = true, // Enable automatic recovery
                        NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
                    };

                    _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
                    _logger.LogInformation("RabbitMQ connection established to {HostName}:{Port}", 
                        _settings.HostName, _settings.Port);
                });
            }

            if (_channel == null || !_channel.IsOpen)
            {
                _channel = _connection!.CreateChannelAsync().GetAwaiter().GetResult();
                _logger.LogInformation("RabbitMQ channel created");
            }
        }
    }

    public async Task PublishAsync<T>(T @event, string queueName, CancellationToken cancellationToken = default) where T : class
    {
        EnsureConnection();

        if (_channel == null)
        {
            throw new InvalidOperationException("RabbitMQ channel is not available");
        }

        try
        {
            // Declare queue (idempotent operation)
            await _channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var message = JsonSerializer.Serialize(@event);
            var body = Encoding.UTF8.GetBytes(message);

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent
            };

            await _channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: queueName,
                mandatory: false,
                basicProperties: properties,
                body: body);

            _logger.LogInformation("Event published to queue {QueueName}: {EventType}", 
                queueName, typeof(T).Name);
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

        if (_channel != null)
        {
            _channel.CloseAsync().GetAwaiter().GetResult();
            _channel.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        
        if (_connection != null)
        {
            _connection.CloseAsync().GetAwaiter().GetResult();
            _connection.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        
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
