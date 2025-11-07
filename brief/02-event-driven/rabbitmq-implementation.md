# 🐰 RabbitMQ Message Broker Implementation

**Category**: Event-Driven Architecture  
**Difficulty**: Intermediate  
**Focus**: Reliable asynchronous communication between microservices

---

## 📖 Overview

**RabbitMQ** is a message broker that enables asynchronous communication between microservices using the AMQP protocol. It acts as a middleman, receiving messages from publishers and delivering them to consumers.

---

## 🎯 Why RabbitMQ?

### Without Message Broker (Synchronous)

```text
❌ Direct Service-to-Service Calls:

BookingService → HTTP POST → PaymentService

Problems:
1. Tight coupling: BookingService must know PaymentService URL
2. Blocking: BookingService waits for PaymentService response
3. Failure cascade: If PaymentService down, booking fails
4. No retry: Failed requests lost
5. Peak load: PaymentService overwhelmed during traffic spikes
```

### With RabbitMQ (Asynchronous)

```text
✅ Event-Driven with Message Broker:

BookingService → Publish Event → RabbitMQ → Consume Event → PaymentService
                                    ↓
                              (Persisted queue)

Benefits:
✅ Loose coupling: Services don't know about each other
✅ Non-blocking: BookingService returns immediately
✅ Resilience: Messages persist if PaymentService down
✅ Automatic retry: Failed messages redelivered
✅ Load leveling: PaymentService processes at its own pace
✅ Multiple consumers: Scale PaymentService independently
```

---

## 🏗️ RabbitMQ Architecture

### Core Concepts

```text
┌────────────────────────────────────────────────────────────────┐
│                         RabbitMQ                               │
│                                                                │
│  Publisher          Exchange            Queue         Consumer│
│  ┌────────┐        ┌────────┐        ┌────────┐     ┌────────┐│
│  │Booking │─Msg───→│ Direct │─Route─→│Booking │────→│Payment ││
│  │Service │        │Exchange│        │Created │     │Service ││
│  └────────┘        └────────┘        │Queue   │     └────────┘│
│                        ↓              └────────┘               │
│                    Routing Key                                 │
│                 "booking.created"                              │
└────────────────────────────────────────────────────────────────┘
```

**Components**:

1. **Publisher**: Sends messages (BookingService)
2. **Exchange**: Routes messages based on routing key
3. **Queue**: Stores messages until consumed
4. **Consumer**: Receives and processes messages (PaymentService)
5. **Routing Key**: Determines which queue receives message

### Exchange Types

| Type | Description | Use Case |
|------|-------------|----------|
| **Direct** | Exact routing key match | Your project (simple routing) |
| **Topic** | Pattern matching (wildcards) | Complex routing (order.*, order.created) |
| **Fanout** | Broadcast to all queues | Notifications to all services |
| **Headers** | Route by header values | Advanced routing logic |

**Your Project Uses**: **Direct Exchange**

---

## 🏗️ Implementation in Your Project

### Docker Compose Configuration

**File**: `docker-compose.yml`

```yaml
services:
  rabbitmq:
    image: rabbitmq:3-management
    container_name: rabbitmq
    environment:
      RABBITMQ_DEFAULT_USER: guest
      RABBITMQ_DEFAULT_PASS: guest
    ports:
      - "5672:5672"      # AMQP protocol
      - "15672:15672"    # Management UI
    volumes:
      - rabbitmq-data:/var/lib/rabbitmq
    networks:
      - bookingsystem-network
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  rabbitmq-data:

networks:
  bookingsystem-network:
    driver: bridge
```

**Access**:
- **AMQP**: `amqp://guest:guest@rabbitmq:5672`
- **Management UI**: `http://localhost:15672` (guest/guest)

### Connection Configuration

**All Services** (`appsettings.json`):

```json
{
  "RabbitMQ": {
    "HostName": "rabbitmq",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest",
    "VirtualHost": "/",
    "RetryCount": 5,
    "RetryDelay": 2000
  }
}
```

### Event Bus Abstraction

**File**: `src/Shared/Events/IEventBus.cs`

```csharp
public interface IEventBus
{
    // Publish event
    Task PublishAsync<TEvent>(TEvent @event) where TEvent : IntegrationEvent;
    
    // Subscribe to event
    void Subscribe<TEvent, THandler>()
        where TEvent : IntegrationEvent
        where THandler : IEventHandler<TEvent>;
    
    // Unsubscribe
    void Unsubscribe<TEvent, THandler>()
        where TEvent : IntegrationEvent
        where THandler : IEventHandler<TEvent>;
}

public abstract class IntegrationEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public interface IEventHandler<in TEvent> where TEvent : IntegrationEvent
{
    Task HandleAsync(TEvent @event);
}
```

### RabbitMQ Implementation

**File**: `src/Shared/EventBus/RabbitMqEventBus.cs`

```csharp
public class RabbitMqEventBus : IEventBus, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RabbitMqEventBus> _logger;
    private readonly ConcurrentDictionary<string, List<Type>> _handlers;
    private readonly string _exchangeName = "booking_system_exchange";
    
    public RabbitMqEventBus(
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        ILogger<RabbitMqEventBus> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _handlers = new ConcurrentDictionary<string, List<Type>>();
        
        // Create connection
        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:HostName"],
            Port = int.Parse(configuration["RabbitMQ:Port"]),
            UserName = configuration["RabbitMQ:UserName"],
            Password = configuration["RabbitMQ:Password"],
            VirtualHost = configuration["RabbitMQ:VirtualHost"],
            
            // Connection resilience
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
            RequestedHeartbeat = TimeSpan.FromSeconds(60)
        };
        
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        
        // Declare exchange
        _channel.ExchangeDeclare(
            exchange: _exchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false
        );
        
        _logger.LogInformation("RabbitMQ connection established");
    }
    
    public async Task PublishAsync<TEvent>(TEvent @event) where TEvent : IntegrationEvent
    {
        var eventName = typeof(TEvent).Name;
        var message = JsonSerializer.Serialize(@event);
        var body = Encoding.UTF8.GetBytes(message);
        
        // Message properties
        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true; // Survive broker restart
        properties.ContentType = "application/json";
        properties.MessageId = @event.Id.ToString();
        properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        
        // Retry logic
        int retryCount = 0;
        int maxRetries = 5;
        
        while (retryCount < maxRetries)
        {
            try
            {
                _channel.BasicPublish(
                    exchange: _exchangeName,
                    routingKey: eventName,
                    basicProperties: properties,
                    body: body
                );
                
                _logger.LogInformation(
                    "Published event {EventName} with ID {EventId}",
                    eventName, @event.Id
                );
                
                return;
            }
            catch (Exception ex)
            {
                retryCount++;
                _logger.LogWarning(
                    ex,
                    "Failed to publish event {EventName} (Attempt {Attempt}/{MaxAttempts})",
                    eventName, retryCount, maxRetries
                );
                
                if (retryCount >= maxRetries)
                {
                    _logger.LogError(
                        ex,
                        "Failed to publish event {EventName} after {MaxAttempts} attempts",
                        eventName, maxRetries
                    );
                    throw;
                }
                
                // Exponential backoff
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)));
            }
        }
    }
    
    public void Subscribe<TEvent, THandler>()
        where TEvent : IntegrationEvent
        where THandler : IEventHandler<TEvent>
    {
        var eventName = typeof(TEvent).Name;
        
        // Add handler to dictionary
        _handlers.AddOrUpdate(
            eventName,
            new List<Type> { typeof(THandler) },
            (key, existing) =>
            {
                if (!existing.Contains(typeof(THandler)))
                {
                    existing.Add(typeof(THandler));
                }
                return existing;
            }
        );
        
        // Declare queue
        var queueName = $"{eventName}_queue";
        _channel.QueueDeclare(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );
        
        // Bind queue to exchange
        _channel.QueueBind(
            queue: queueName,
            exchange: _exchangeName,
            routingKey: eventName
        );
        
        // Create consumer
        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            var eventId = ea.BasicProperties.MessageId;
            
            _logger.LogInformation(
                "Received event {EventName} with ID {EventId}",
                eventName, eventId
            );
            
            try
            {
                // Deserialize event
                var @event = JsonSerializer.Deserialize<TEvent>(message);
                
                // Get handlers
                if (_handlers.TryGetValue(eventName, out var handlerTypes))
                {
                    using var scope = _serviceProvider.CreateScope();
                    
                    foreach (var handlerType in handlerTypes)
                    {
                        var handler = (IEventHandler<TEvent>)scope.ServiceProvider.GetService(handlerType);
                        if (handler != null)
                        {
                            await handler.HandleAsync(@event);
                        }
                    }
                }
                
                // Acknowledge message (remove from queue)
                _channel.BasicAck(ea.DeliveryTag, multiple: false);
                
                _logger.LogInformation(
                    "Successfully processed event {EventName} with ID {EventId}",
                    eventName, eventId
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error processing event {EventName} with ID {EventId}",
                    eventName, eventId
                );
                
                // Negative acknowledge (requeue or dead-letter)
                _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
            }
        };
        
        // Start consuming
        _channel.BasicConsume(
            queue: queueName,
            autoAck: false, // Manual acknowledgment
            consumer: consumer
        );
        
        _logger.LogInformation(
            "Subscribed to event {EventName} with handler {HandlerName}",
            eventName, typeof(THandler).Name
        );
    }
    
    public void Unsubscribe<TEvent, THandler>()
        where TEvent : IntegrationEvent
        where THandler : IEventHandler<TEvent>
    {
        var eventName = typeof(TEvent).Name;
        
        if (_handlers.TryGetValue(eventName, out var handlerTypes))
        {
            handlerTypes.Remove(typeof(THandler));
            
            if (handlerTypes.Count == 0)
            {
                _handlers.TryRemove(eventName, out _);
            }
        }
    }
    
    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
```

---

## 📨 Publishing Events

### Define Event

**File**: `src/Shared/Events/BookingCreatedEvent.cs`

```csharp
public class BookingCreatedEvent : IntegrationEvent
{
    public Guid BookingId { get; set; }
    public string UserId { get; set; }
    public string EventName { get; set; }
    public int TicketCount { get; set; }
    public decimal TotalAmount { get; set; }
}
```

### Publish from BookingService

**File**: `src/BookingService/Services/BookingServiceImpl.cs`

```csharp
public class BookingServiceImpl : IBookingService
{
    private readonly BookingDbContext _dbContext;
    private readonly IEventBus _eventBus;
    private readonly ILogger<BookingServiceImpl> _logger;
    
    public async Task<Booking> CreateBookingAsync(CreateBookingRequest request)
    {
        // 1. Create booking in database
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            EventName = request.EventName,
            TicketCount = request.TicketCount,
            TotalAmount = request.TicketCount * 50m, // $50 per ticket
            Status = BookingStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        
        await _dbContext.Bookings.AddAsync(booking);
        await _dbContext.SaveChangesAsync();
        
        _logger.LogInformation(
            "Created booking {BookingId} for user {UserId}",
            booking.Id, booking.UserId
        );
        
        // 2. Publish event to RabbitMQ
        var @event = new BookingCreatedEvent
        {
            BookingId = booking.Id,
            UserId = booking.UserId,
            EventName = booking.EventName,
            TicketCount = booking.TicketCount,
            TotalAmount = booking.TotalAmount
        };
        
        await _eventBus.PublishAsync(@event);
        
        _logger.LogInformation(
            "Published BookingCreatedEvent for booking {BookingId}",
            booking.Id
        );
        
        return booking;
    }
}
```

---

## 📥 Consuming Events

### Register Consumer in PaymentService

**File**: `src/PaymentService/Program.cs`

```csharp
var builder = WebApplication.CreateBuilder(args);

// ... other services ...

// Register event bus
builder.Services.AddSingleton<IEventBus, RabbitMqEventBus>();

// Register event handlers
builder.Services.AddScoped<BookingCreatedEventHandler>();

var app = builder.Build();

// ... middleware ...

// Subscribe to events
var eventBus = app.Services.GetRequiredService<IEventBus>();
eventBus.Subscribe<BookingCreatedEvent, BookingCreatedEventHandler>();

app.Run();
```

### Implement Event Handler

**File**: `src/PaymentService/EventHandlers/BookingCreatedEventHandler.cs`

```csharp
public class BookingCreatedEventHandler : IEventHandler<BookingCreatedEvent>
{
    private readonly PaymentDbContext _dbContext;
    private readonly IEventBus _eventBus;
    private readonly ILogger<BookingCreatedEventHandler> _logger;
    
    public BookingCreatedEventHandler(
        PaymentDbContext dbContext,
        IEventBus eventBus,
        ILogger<BookingCreatedEventHandler> logger)
    {
        _dbContext = dbContext;
        _eventBus = eventBus;
        _logger = logger;
    }
    
    public async Task HandleAsync(BookingCreatedEvent @event)
    {
        _logger.LogInformation(
            "Processing BookingCreatedEvent for booking {BookingId}",
            @event.BookingId
        );
        
        try
        {
            // 1. Check if payment already exists (idempotency)
            var existingPayment = await _dbContext.Payments
                .FirstOrDefaultAsync(p => p.BookingId == @event.BookingId);
            
            if (existingPayment != null)
            {
                _logger.LogWarning(
                    "Payment for booking {BookingId} already exists, skipping",
                    @event.BookingId
                );
                return;
            }
            
            // 2. Create payment
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                BookingId = @event.BookingId,
                UserId = @event.UserId,
                Amount = @event.TotalAmount,
                Status = PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            
            await _dbContext.Payments.AddAsync(payment);
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation(
                "Created payment {PaymentId} for booking {BookingId}",
                payment.Id, @event.BookingId
            );
            
            // 3. Process payment (simulate)
            await Task.Delay(1000); // Simulate payment gateway call
            
            payment.Status = PaymentStatus.Completed;
            payment.TransactionId = $"txn_{Guid.NewGuid().ToString("N")[..10]}";
            payment.CompletedAt = DateTime.UtcNow;
            
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation(
                "Payment {PaymentId} completed successfully",
                payment.Id
            );
            
            // 4. Publish PaymentSucceeded event
            var paymentSucceededEvent = new PaymentSucceededEvent
            {
                BookingId = @event.BookingId,
                PaymentId = payment.Id,
                TransactionId = payment.TransactionId,
                Amount = payment.Amount
            };
            
            await _eventBus.PublishAsync(paymentSucceededEvent);
            
            _logger.LogInformation(
                "Published PaymentSucceededEvent for booking {BookingId}",
                @event.BookingId
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to process BookingCreatedEvent for booking {BookingId}",
                @event.BookingId
            );
            
            // Publish PaymentFailed event
            var paymentFailedEvent = new PaymentFailedEvent
            {
                BookingId = @event.BookingId,
                Reason = ex.Message
            };
            
            await _eventBus.PublishAsync(paymentFailedEvent);
            
            throw; // Requeue message for retry
        }
    }
}
```

---

## 🔄 Event Flow Example

### Complete Flow: Create Booking → Process Payment → Confirm Booking

```text
Time  Service          Action                                    Event
──────────────────────────────────────────────────────────────────────────
t=0   BookingService   POST /api/bookings
                       Create booking (status=Pending)
                       Save to PostgreSQL ✓

t=1   BookingService   Publish BookingCreatedEvent
                       → RabbitMQ (booking_created_queue)

t=2   RabbitMQ         Store message in queue
                       (persisted to disk)

t=3   PaymentService   Consume BookingCreatedEvent
                       Create payment (status=Pending)
                       Save to MongoDB ✓

t=4   PaymentService   Process payment
                       (call payment gateway)
                       
t=5   PaymentService   Update payment (status=Completed)
                       Save to MongoDB ✓
                       
t=6   PaymentService   Publish PaymentSucceededEvent
                       → RabbitMQ (payment_succeeded_queue)

t=7   RabbitMQ         Store message in queue

t=8   BookingService   Consume PaymentSucceededEvent
                       Update booking (status=Confirmed)
                       Save to PostgreSQL ✓

Result: Booking confirmed, payment processed (eventual consistency)
```

### Failure Scenario: Payment Fails

```text
Time  Service          Action                                    Event
──────────────────────────────────────────────────────────────────────────
t=0   BookingService   POST /api/bookings
                       Create booking (status=Pending) ✓

t=1   BookingService   Publish BookingCreatedEvent ✓

t=2   PaymentService   Consume BookingCreatedEvent
                       Create payment (status=Pending) ✓

t=3   PaymentService   Process payment
                       (payment gateway error!) ❌

t=4   PaymentService   Publish PaymentFailedEvent
                       → RabbitMQ

t=5   BookingService   Consume PaymentFailedEvent
                       Update booking (status=Cancelled)
                       reason="Payment failed" ✓

Result: Booking cancelled (compensating action), user notified
```

---

## 🔄 Connection Resilience

### Automatic Reconnection

```csharp
var factory = new ConnectionFactory
{
    HostName = configuration["RabbitMQ:HostName"],
    
    // Automatic recovery settings
    AutomaticRecoveryEnabled = true,
    NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
    RequestedHeartbeat = TimeSpan.FromSeconds(60),
    
    // Retry settings
    ContinuationTimeout = TimeSpan.FromSeconds(10)
};
```

**How It Works**:

```text
Normal Operation:
Service ←heartbeat every 60s→ RabbitMQ

Network Partition:
Service ✗ ───────────────── ✗ RabbitMQ
           (connection lost)

Automatic Recovery:
Service waits 10 seconds...
Service attempts reconnection...
Service ✓ ────connected───→ ✓ RabbitMQ
           (recovered!)

Resume: Continue consuming/publishing messages
```

### Manual Retry with Polly

**File**: `src/Shared/EventBus/RabbitMqEventBusWithPolly.cs`

```csharp
public class RabbitMqEventBusWithPolly : IEventBus
{
    private readonly IAsyncPolicy _retryPolicy;
    
    public RabbitMqEventBusWithPolly()
    {
        _retryPolicy = Policy
            .Handle<BrokerUnreachableException>()
            .Or<SocketException>()
            .WaitAndRetryAsync(
                retryCount: 5,
                sleepDurationProvider: retryAttempt => 
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + 
                    TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)), // Jitter
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        exception,
                        "RabbitMQ connection attempt {RetryCount} failed. Retrying in {RetryDelay}s",
                        retryCount, timeSpan.TotalSeconds
                    );
                }
            );
    }
    
    public async Task PublishAsync<TEvent>(TEvent @event) where TEvent : IntegrationEvent
    {
        await _retryPolicy.ExecuteAsync(async () =>
        {
            _channel.BasicPublish(
                exchange: _exchangeName,
                routingKey: typeof(TEvent).Name,
                basicProperties: _channel.CreateBasicProperties(),
                body: Encoding.UTF8.GetBytes(JsonSerializer.Serialize(@event))
            );
        });
    }
}
```

---

## 📊 Monitoring & Management UI

### Access Management UI

**URL**: `http://localhost:15672`  
**Credentials**: guest/guest

### Key Metrics

**Queues Tab**:
- Messages ready: Waiting to be consumed
- Messages unacknowledged: Being processed
- Message rate: Messages/sec

**Exchanges Tab**:
- Messages published
- Messages routed

**Connections Tab**:
- Active connections from services
- Connection state (running/blocked)

### Example: View Messages

```text
1. Open http://localhost:15672
2. Go to "Queues" tab
3. Click on "booking_created_queue"
4. Click "Get messages"
5. See message payload:
   {
     "id": "123e4567-e89b-12d3-a456-426614174000",
     "bookingId": "abc-123",
     "userId": "user-123",
     "eventName": "Concert",
     "ticketCount": 2,
     "totalAmount": 100.00,
     "createdAt": "2025-11-07T10:30:00Z"
   }
```

---

## 🎓 Key Takeaways

### Benefits of RabbitMQ

1. **Asynchronous Communication**: Non-blocking, services don't wait
2. **Loose Coupling**: Services don't know about each other
3. **Reliability**: Messages persist, survive crashes
4. **Load Leveling**: Consumers process at own pace
5. **Scalability**: Multiple consumers can process in parallel
6. **Retry Mechanism**: Failed messages automatically retried

### Your Project Architecture

| Component | Technology | Purpose |
|-----------|-----------|---------|
| **Message Broker** | RabbitMQ 3 | Route and store messages |
| **Protocol** | AMQP | Reliable message delivery |
| **Exchange Type** | Direct | Simple routing by event name |
| **Message Format** | JSON | Easy serialization/deserialization |
| **Acknowledgment** | Manual | Control when message removed |
| **Persistence** | Durable queues | Survive broker restart |

### Event Flow Summary

```text
1. BookingService creates booking → Publishes BookingCreatedEvent
2. RabbitMQ stores event in booking_created_queue
3. PaymentService consumes event → Processes payment
4. PaymentService publishes PaymentSucceededEvent or PaymentFailedEvent
5. BookingService consumes event → Updates booking status
```

### Best Practices

1. **Idempotency**: Check if already processed (duplicate messages)
2. **Manual Ack**: Control when message removed from queue
3. **Error Handling**: Catch exceptions, publish failure events
4. **Retry Logic**: Exponential backoff with jitter
5. **Dead Letter Queue**: Move failed messages after max retries (future)
6. **Correlation IDs**: Track event flow across services
7. **Durable Queues**: Persist messages to disk

---

## 📚 Further Study

### Related Documents

- [Event-Driven Architecture](./event-driven-architecture.md)
- [Outbox Pattern](./outbox-pattern.md)
- [Distributed Systems Theory](../07-computer-science/distributed-systems-theory.md)
- [Retry Logic and Polly](/docs/phase3-event-integration/RETRY_LOGIC_AND_POLLY.md)

### Project Documentation

- `/docs/phase3-event-integration/PHASE4_SUMMARY.md`
- `/docs/phase3-event-integration/PHASE4_CONNECTION_RETRY.md`
- `docker-compose.yml`

### External Resources

- [RabbitMQ Tutorials](https://www.rabbitmq.com/getstarted.html)
- [RabbitMQ Best Practices](https://www.rabbitmq.com/best-practices.html)
- [AMQP 0-9-1 Model Explained](https://www.rabbitmq.com/tutorials/amqp-concepts.html)

---

**Last Updated**: November 7, 2025  
**Status**: ✅ Fully implemented in your project  
**Next**: [Outbox Pattern](./outbox-pattern.md)
