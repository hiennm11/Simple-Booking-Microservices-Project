using System.Text;
using MongoDB.Driver;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using PaymentService.EventBus;
using PaymentService.Models;
using Polly;
using Polly.Retry;
using System.Net.Sockets;

namespace PaymentService.Consumers;

/// <summary>
/// Background service that consumes messages from Dead Letter Queues (DLQ)
/// Stores failed payment-related messages in MongoDB for manual investigation and recovery
/// </summary>
public class DeadLetterQueueHandler : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMQSettings _settings;
    private readonly ILogger<DeadLetterQueueHandler> _logger;
    private readonly ResiliencePipeline _connectionPipeline;
    private IConnection? _connection;
    private IChannel? _channel;
    private readonly Dictionary<string, string> _dlqMappings;

    public DeadLetterQueueHandler(
        IServiceProvider serviceProvider,
        IOptions<RabbitMQSettings> settings,
        ILogger<DeadLetterQueueHandler> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _settings = settings.Value;
        _logger = logger;

        // Build DLQ mappings from configuration
        _dlqMappings = new Dictionary<string, string>();
        var dlqConfig = configuration.GetSection("RabbitMQ:DeadLetterQueues");
        foreach (var child in dlqConfig.GetChildren())
        {
            var queueName = child.Value;
            var eventType = child.Key;
            if (!string.IsNullOrEmpty(queueName))
            {
                _dlqMappings[queueName] = eventType;
            }
        }

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
                        "DeadLetterQueueHandler RabbitMQ connection retry {Attempt}/{MaxAttempts} after {Delay}ms. " +
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
        _logger.LogInformation("DeadLetterQueueHandler is starting");

        stoppingToken.Register(() =>
            _logger.LogInformation("DeadLetterQueueHandler background task is stopping"));

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
            _logger.LogError(ex, "Error in DeadLetterQueueHandler");
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
            _logger.LogInformation("DeadLetterQueueHandler established connection to RabbitMQ");
        });

        _channel = await _connection!.CreateChannelAsync();

        // Set up consumers for each DLQ
        foreach (var dlqMapping in _dlqMappings)
        {
            var queueName = dlqMapping.Key;
            var eventType = dlqMapping.Value;

            // Declare the DLQ (if not already declared)
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
                await HandleDeadLetterMessageAsync(ea, queueName, eventType);
            };

            await _channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: false,
                consumer: consumer);

            _logger.LogInformation(
                "DeadLetterQueueHandler is listening to DLQ: {QueueName} for event type: {EventType}",
                queueName,
                eventType);
        }
    }

    private async Task HandleDeadLetterMessageAsync(
        BasicDeliverEventArgs ea,
        string queueName,
        string eventType)
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);

        try
        {
            _logger.LogWarning(
                "Received message from DLQ: {QueueName}, EventType: {EventType}, Message: {Message}",
                queueName,
                eventType,
                message);

            // Extract metadata from message headers if available
            var attemptCount = GetHeaderValue<int>(ea.BasicProperties, "x-retry-count", 0);
            var firstAttemptAt = GetHeaderValue<DateTime?>(ea.BasicProperties, "x-first-attempt", null) ?? DateTime.UtcNow;
            var errorMessage = GetHeaderValue<string>(ea.BasicProperties, "x-error-message", "Unknown error");
            var stackTrace = GetHeaderValue<string?>(ea.BasicProperties, "x-stack-trace", null);

            // Store the failed message in MongoDB
            await StoreDeadLetterMessageAsync(new DeadLetterMessage
            {
                SourceQueue = queueName.Replace("_dlq", ""),
                EventType = eventType,
                Payload = message,
                ErrorMessage = errorMessage,
                StackTrace = stackTrace,
                AttemptCount = attemptCount > 0 ? attemptCount : 3, // Default to max retry count
                FirstAttemptAt = firstAttemptAt,
                FailedAt = DateTime.UtcNow,
                Resolved = false
            });

            // Acknowledge the message (remove from DLQ)
            await _channel!.BasicAckAsync(ea.DeliveryTag, false);

            _logger.LogInformation(
                "Successfully stored dead letter message from {QueueName} in MongoDB",
                queueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing dead letter message from {QueueName}: {Message}",
                queueName,
                message);

            // Don't requeue - we'll try again next time the service restarts
            // or wait for manual intervention
            await _channel!.BasicNackAsync(ea.DeliveryTag, false, requeue: false);
        }
    }

    private async Task StoreDeadLetterMessageAsync(DeadLetterMessage deadLetterMessage)
    {
        using var scope = _serviceProvider.CreateScope();
        var mongoClient = scope.ServiceProvider.GetRequiredService<IMongoClient>();
        var database = mongoClient.GetDatabase("paymentdb");
        var collection = database.GetCollection<DeadLetterMessage>("deadlettermessages");

        await collection.InsertOneAsync(deadLetterMessage);

        _logger.LogInformation(
            "Dead letter message stored in MongoDB. ID: {Id}, EventType: {EventType}, SourceQueue: {SourceQueue}",
            deadLetterMessage.Id,
            deadLetterMessage.EventType,
            deadLetterMessage.SourceQueue);
    }

    private static T GetHeaderValue<T>(IReadOnlyBasicProperties? properties, string headerName, T defaultValue)
    {
        if (properties?.Headers == null || !properties.Headers.ContainsKey(headerName))
        {
            return defaultValue;
        }

        try
        {
            var value = properties.Headers[headerName];
            
            if (value == null)
                return defaultValue;

            if (typeof(T) == typeof(string))
            {
                return (T)(object)Encoding.UTF8.GetString((byte[])value);
            }
            else if (typeof(T) == typeof(int))
            {
                return (T)(object)Convert.ToInt32(value);
            }
            else if (typeof(T) == typeof(DateTime) || typeof(T) == typeof(DateTime?))
            {
                var stringValue = Encoding.UTF8.GetString((byte[])value);
                if (DateTime.TryParse(stringValue, out var dateValue))
                {
                    return (T)(object)dateValue;
                }
            }

            return defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DeadLetterQueueHandler is stopping");

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
