# Event Bus Architecture Explained

## Table of Contents
- [What is an Event Bus?](#what-is-an-event-bus)
- [Why Use an Event Bus?](#why-use-an-event-bus)
- [Event-Driven Architecture Principles](#event-driven-architecture-principles)
- [RabbitMQ as Our Event Bus](#rabbitmq-as-our-event-bus)
- [How Our Event Bus Works](#how-our-event-bus-works)
- [Event Flow Examples](#event-flow-examples)
- [Implementation Details](#implementation-details)
- [Best Practices](#best-practices)

---

## What is an Event Bus?

An **Event Bus** is a communication mechanism that allows different parts of a distributed system to communicate asynchronously through events. Think of it as a "message highway" where services can:

- **Publish** events when something important happens
- **Subscribe** to events they're interested in
- **React** to events without knowing who published them

### Key Characteristics

| Characteristic | Description |
|----------------|-------------|
| **Asynchronous** | Publishers don't wait for subscribers to process events |
| **Decoupled** | Services don't need to know about each other |
| **Scalable** | Multiple subscribers can process events independently |
| **Reliable** | Messages are persisted and can be retried on failure |

### Visual Metaphor

```
Traditional Direct Communication (Tight Coupling):
ServiceA ---REST API---> ServiceB ---REST API---> ServiceC
   ↓ Problem: If ServiceB is down, ServiceA request fails

Event Bus (Loose Coupling):
ServiceA --publish--> [EVENT BUS] <--subscribe-- ServiceB
                           ↑
                           |
                      subscribe
                           |
                        ServiceC
   ✓ Benefit: Services can fail independently
```

---

## Why Use an Event Bus?

### 1. **Decoupling Services**

**Without Event Bus:**
```csharp
// BookingService needs to know about PaymentService
public async Task CreateBooking(BookingRequest request)
{
    var booking = await _bookingRepo.CreateAsync(request);
    
    // Tight coupling - must call PaymentService directly
    var payment = await _paymentServiceClient.ProcessPayment(booking.Id);
    
    // What if PaymentService is down? Booking fails!
}
```

**With Event Bus:**
```csharp
// BookingService doesn't need to know who processes the event
public async Task CreateBooking(BookingRequest request)
{
    var booking = await _bookingRepo.CreateAsync(request);
    
    // Just publish an event and forget
    await _eventBus.PublishAsync(new BookingCreatedEvent { ... });
    
    // ✓ Booking succeeds even if payment processing happens later
}
```

### 2. **Asynchronous Processing**

- **Synchronous**: User waits for payment processing (5-10 seconds)
- **Asynchronous**: User gets immediate confirmation, payment processes in background

### 3. **Multiple Subscribers**

One event can trigger multiple actions:

```
BookingCreated Event
    ├── PaymentService → Process payment
    ├── NotificationService → Send confirmation email
    ├── AnalyticsService → Log metrics
    └── InventoryService → Reserve room
```

### 4. **Resilience**

- If a subscriber is down, messages wait in queue
- Automatic retry on failure
- No cascading failures

---

## Event-Driven Architecture Principles

### 1. Domain Events

Events represent **something that happened** in the past:

✅ **Good Event Names** (Past Tense):
- `BookingCreated`
- `PaymentSucceeded`
- `OrderShipped`
- `UserRegistered`

❌ **Bad Event Names** (Commands):
- `CreateBooking` (this is a command, not an event)
- `ProcessPayment` (instruction, not notification)

### 2. Event Structure

Our events follow a standard structure:

```json
{
  "eventId": "unique-identifier",
  "eventName": "EventType",
  "timestamp": "when-it-happened",
  "data": {
    // Relevant information about what happened
  }
}
```

### 3. Event Choreography vs Orchestration

**Choreography** (What we use):
- Each service reacts to events independently
- No central coordinator
- More decoupled and scalable

```
BookingService → [BookingCreated] → PaymentService
                                   ↓
                      PaymentService processes
                                   ↓
                      [PaymentSucceeded] → BookingService
```

**Orchestration** (Alternative approach):
- Central orchestrator controls the workflow
- More complex but offers better control

---

## RabbitMQ as Our Event Bus

### What is RabbitMQ?

**RabbitMQ** is a message broker that implements the Advanced Message Queuing Protocol (AMQP). It acts as a middleman between publishers and consumers.

### Why RabbitMQ?

| Criteria | RabbitMQ | Alternative (e.g., Kafka) |
|----------|----------|---------------------------|
| **Learning Curve** | Easy | Moderate |
| **Setup Complexity** | Simple | Complex |
| **Best For** | Request/Response, Task Queues | Event Streaming, Big Data |
| **Message Ordering** | Per queue | Per partition |
| **Docker Support** | Excellent | Good |
| **Management UI** | Built-in | Requires additional tools |

For a **learning project** focused on microservices fundamentals, RabbitMQ is the better choice.

### RabbitMQ Core Concepts

#### 1. **Queue**
A buffer that stores messages until they're consumed.

```
[Publisher] → Queue: booking_created → [Consumer]
```

#### 2. **Exchange** (Not used in our simple setup)
Routes messages to queues based on rules.

#### 3. **Message**
The actual data being sent (our events).

#### 4. **Producer/Publisher**
Service that sends messages (e.g., BookingService publishing `BookingCreated`).

#### 5. **Consumer/Subscriber**
Service that receives and processes messages (e.g., PaymentService consuming `BookingCreated`).

### RabbitMQ in Docker

We run RabbitMQ in Docker with management UI:

```yaml
rabbitmq:
  image: rabbitmq:3-management
  ports:
    - "5672:5672"   # AMQP protocol
    - "15672:15672" # Management UI
  environment:
    RABBITMQ_DEFAULT_USER: guest
    RABBITMQ_DEFAULT_PASS: guest
```

**Access Management UI**: http://localhost:15672
- Username: `guest`
- Password: `guest`

### Message Persistence

RabbitMQ ensures messages aren't lost:

```csharp
var properties = _channel.CreateBasicProperties();
properties.Persistent = true;  // Survive broker restarts
properties.DeliveryMode = 2;    // Persistent delivery
```

Queue is also declared as durable:

```csharp
_channel.QueueDeclare(
    queue: queueName,
    durable: true,      // Survive broker restarts
    exclusive: false,
    autoDelete: false,
    arguments: null
);
```

---

## How Our Event Bus Works

### Architecture Overview

```
┌─────────────────┐
│ BookingService  │
│                 │
│  1. Create      │
│     Booking     │
│                 │
│  2. Save to DB  │
│                 │
│  3. Publish     │
│     Event       │
└────────┬────────┘
         │
         │ BookingCreated Event
         ↓
┌────────────────────────────────┐
│        RabbitMQ Broker         │
│  ┌──────────────────────────┐  │
│  │ Queue: booking_created   │  │
│  │ [Message][Message]       │  │
│  └──────────────────────────┘  │
│  ┌──────────────────────────┐  │
│  │ Queue: payment_succeeded │  │
│  │ [Message]                │  │
│  └──────────────────────────┘  │
└────────┬───────────────────┬───┘
         │                   │
         ↓                   ↓
┌────────────────┐   ┌────────────────┐
│ PaymentService │   │ BookingService │
│                │   │                │
│ 1. Consume     │   │ 1. Consume     │
│    Event       │   │    Event       │
│                │   │                │
│ 2. Process     │   │ 2. Update      │
│    Payment     │   │    Booking     │
│                │   │    Status      │
│ 3. Publish     │   │                │
│    Success     │───┘                │
└────────────────┘                    └────────────────┘
```

### Implementation Components

#### 1. **IEventBus Interface** (Shared)

Defines the contract for publishing events:

```csharp
namespace Shared.EventBus;

public interface IEventBus
{
    Task PublishAsync<T>(T @event, string queueName, 
        CancellationToken cancellationToken = default) where T : class;
}
```

#### 2. **RabbitMQEventBus** (Each Service)

Concrete implementation for RabbitMQ:

```csharp
public class RabbitMQEventBus : IEventBus
{
    private IConnection _connection;
    private IModel _channel;

    public async Task PublishAsync<T>(T @event, string queueName, ...)
    {
        // 1. Ensure connection to RabbitMQ
        EnsureConnection();
        
        // 2. Declare queue (idempotent - safe to call multiple times)
        _channel.QueueDeclare(queue: queueName, durable: true, ...);
        
        // 3. Serialize event to JSON
        var message = JsonSerializer.Serialize(@event);
        var body = Encoding.UTF8.GetBytes(message);
        
        // 4. Set message properties (persistence, content type)
        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        
        // 5. Publish to queue
        _channel.BasicPublish(exchange: "", routingKey: queueName, 
            body: body, basicProperties: properties);
    }
}
```

#### 3. **Event Consumer** (Background Service)

Listens for events in the background:

```csharp
public class PaymentSucceededConsumer : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 1. Connect to RabbitMQ
        InitializeRabbitMQ();
        
        // 2. Declare queue we want to consume from
        var queueName = "payment_succeeded";
        _channel.QueueDeclare(queue: queueName, durable: true, ...);
        
        // 3. Create consumer
        var consumer = new AsyncEventingBasicConsumer(_channel);
        
        // 4. Handle incoming messages
        consumer.Received += async (model, ea) => {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            var paymentEvent = JsonSerializer.Deserialize<PaymentSucceededEvent>(message);
            
            // Process the event
            await ProcessPaymentSucceededAsync(paymentEvent);
            
            // Acknowledge message (remove from queue)
            _channel.BasicAck(ea.DeliveryTag, false);
        };
        
        // 5. Start consuming
        _channel.BasicConsume(queue: queueName, autoAck: false, 
            consumer: consumer);
    }
}
```

---

## Event Flow Examples

### Example 1: Booking Creation Flow

**Step-by-Step:**

```
1. User creates booking via API
   ↓
2. BookingService.CreateBooking() called
   ↓
3. Booking saved to database (Status: PENDING)
   ↓
4. BookingCreated event published to RabbitMQ
   {
     "eventId": "abc-123",
     "eventName": "BookingCreated",
     "timestamp": "2025-11-03T10:00:00Z",
     "data": {
       "bookingId": "xyz-789",
       "userId": "user-456",
       "roomId": "ROOM-101",
       "amount": 500000,
       "status": "PENDING"
     }
   }
   ↓
5. Message stored in "booking_created" queue
   ↓
6. PaymentService consumes message
   ↓
7. Payment processing happens asynchronously
```

### Example 2: Payment Success Flow

**Step-by-Step:**

```
1. PaymentService processes payment successfully
   ↓
2. Payment record saved to database
   ↓
3. PaymentSucceeded event published to RabbitMQ
   {
     "eventId": "def-456",
     "eventName": "PaymentSucceeded",
     "timestamp": "2025-11-03T10:00:30Z",
     "data": {
       "paymentId": "pay-999",
       "bookingId": "xyz-789",
       "amount": 500000,
       "status": "SUCCESS"
     }
   }
   ↓
4. Message stored in "payment_succeeded" queue
   ↓
5. BookingService consumes message
   ↓
6. Booking status updated: PENDING → CONFIRMED
   ↓
7. ConfirmedAt timestamp recorded
```

### Complete Round Trip

```
Time: T+0s
Client → BookingService: POST /api/bookings
BookingService → Database: INSERT booking (status=PENDING)
BookingService → RabbitMQ: Publish BookingCreated
BookingService → Client: 201 Created (bookingId)

Time: T+1s
RabbitMQ → PaymentService: Deliver BookingCreated
PaymentService: Process payment logic
PaymentService → Database: INSERT payment
PaymentService → RabbitMQ: Publish PaymentSucceeded

Time: T+2s
RabbitMQ → BookingService: Deliver PaymentSucceeded
BookingService → Database: UPDATE booking (status=CONFIRMED)
BookingService: Log success

✅ Booking is now confirmed!
```

---

## Implementation Details

### Configuration (appsettings.json)

```json
{
  "RabbitMQ": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest",
    "VirtualHost": "/",
    "Queues": {
      "BookingCreated": "booking_created",
      "PaymentSucceeded": "payment_succeeded"
    }
  }
}
```

### Dependency Injection Setup

```csharp
// In Program.cs

// Configure RabbitMQ settings
builder.Services.Configure<RabbitMQSettings>(
    builder.Configuration.GetSection("RabbitMQ"));

// Register EventBus (Singleton for connection pooling)
builder.Services.AddSingleton<IEventBus, RabbitMQEventBus>();

// Register Background Services (Consumers)
builder.Services.AddHostedService<PaymentSucceededConsumer>();
```

### Publishing Events

```csharp
public class BookingServiceImpl : IBookingService
{
    private readonly IEventBus _eventBus;
    private readonly RabbitMQSettings _rabbitMQSettings;

    public async Task<BookingResponse> CreateBookingAsync(CreateBookingRequest request)
    {
        // 1. Save booking to database
        var booking = new Booking { /* ... */ };
        await _dbContext.SaveChangesAsync();

        // 2. Create event
        var bookingCreatedEvent = new BookingCreatedEvent
        {
            EventId = Guid.NewGuid(),
            EventName = "BookingCreated",
            Timestamp = DateTime.UtcNow,
            Data = new BookingCreatedData
            {
                BookingId = booking.Id,
                UserId = booking.UserId,
                RoomId = booking.RoomId,
                Amount = booking.Amount,
                Status = booking.Status
            }
        };

        // 3. Publish event (fire and forget)
        var queueName = _rabbitMQSettings.Queues["BookingCreated"];
        await _eventBus.PublishAsync(bookingCreatedEvent, queueName);

        return MapToResponse(booking);
    }
}
```

### Consuming Events

```csharp
private async Task HandleMessageAsync(BasicDeliverEventArgs ea)
{
    var body = ea.Body.ToArray();
    var message = Encoding.UTF8.GetString(body);

    try
    {
        // Deserialize event
        var paymentEvent = JsonSerializer.Deserialize<PaymentSucceededEvent>(message);
        
        // Process event
        await ProcessPaymentSucceededAsync(paymentEvent);
        
        // Acknowledge (remove from queue)
        _channel.BasicAck(ea.DeliveryTag, false);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error processing event");
        
        // Reject and requeue for retry
        _channel.BasicNack(ea.DeliveryTag, false, requeue: true);
    }
}
```

---

## Best Practices

### 1. **Idempotency**

Always check if an event has already been processed:

```csharp
private async Task ProcessPaymentSucceededAsync(PaymentSucceededEvent paymentEvent)
{
    var booking = await _dbContext.Bookings
        .FirstOrDefaultAsync(b => b.Id == paymentEvent.Data.BookingId);

    // ✓ Idempotency check
    if (booking.Status == "CONFIRMED")
    {
        _logger.LogInformation("Already confirmed. Skipping.");
        return; // Don't process twice
    }

    booking.Status = "CONFIRMED";
    await _dbContext.SaveChangesAsync();
}
```

### 2. **Error Handling**

```csharp
try
{
    await ProcessEventAsync(event);
    _channel.BasicAck(deliveryTag, false); // Success
}
catch (TransientException ex)
{
    // Temporary error - retry later
    _channel.BasicNack(deliveryTag, false, requeue: true);
}
catch (PermanentException ex)
{
    // Permanent error - don't retry
    _channel.BasicNack(deliveryTag, false, requeue: false);
    // Consider sending to dead-letter queue
}
```

### 3. **Event Versioning**

Include version in event name or data:

```json
{
  "eventName": "BookingCreated",
  "version": "1.0",
  "data": { /* ... */ }
}
```

### 4. **Logging and Monitoring**

```csharp
_logger.LogInformation(
    "Event published to queue {QueueName}: {EventType} with ID {EventId}",
    queueName, 
    typeof(T).Name, 
    @event.EventId
);
```

### 5. **Dead Letter Queue (Future)**

For messages that fail repeatedly:

```csharp
var args = new Dictionary<string, object>
{
    { "x-dead-letter-exchange", "dlx" },
    { "x-message-ttl", 60000 } // 60 seconds
};
```

### 6. **Message Size**

Keep events small and focused:

✅ **Good**: Include only IDs and critical data
```json
{
  "bookingId": "123",
  "userId": "456",
  "amount": 500000
}
```

❌ **Bad**: Include entire entity
```json
{
  "booking": { /* 50 fields */ },
  "user": { /* 30 fields */ },
  "room": { /* 20 fields */ }
}
```

### 7. **Queue Naming Convention**

Use snake_case for consistency:
- `booking_created`
- `payment_succeeded`
- `user_registered`

---

## Advantages of Our Event Bus Implementation

| Advantage | Description |
|-----------|-------------|
| **Loose Coupling** | Services don't know about each other |
| **Scalability** | Add more consumers without changing publishers |
| **Resilience** | Service failures don't cascade |
| **Async Processing** | Fast response times for users |
| **Audit Trail** | Events can be logged for debugging |
| **Flexibility** | Easy to add new event handlers |

## Disadvantages to Be Aware Of

| Disadvantage | Mitigation |
|--------------|------------|
| **Eventual Consistency** | Design UI to show "Processing" states |
| **Message Ordering** | Use single consumer or ordering keys |
| **Debugging Complexity** | Comprehensive logging and correlation IDs |
| **Duplicate Messages** | Implement idempotency checks |
| **Network Dependency** | RabbitMQ must be highly available |

---

## Testing the Event Bus

### 1. Manual Testing via RabbitMQ UI

1. Access http://localhost:15672
2. Go to "Queues" tab
3. See messages waiting in queues
4. Manually publish/consume messages

### 2. Code Testing

```csharp
// Create a booking
var booking = await bookingService.CreateBookingAsync(new CreateBookingRequest
{
    UserId = Guid.NewGuid(),
    RoomId = "ROOM-101",
    Amount = 500000
});

// Check RabbitMQ queue
// Should have 1 message in "booking_created" queue

// Simulate payment success
await eventBus.PublishAsync(new PaymentSucceededEvent
{
    Data = new PaymentSucceededData
    {
        BookingId = booking.Id,
        Amount = 500000,
        Status = "SUCCESS"
    }
}, "payment_succeeded");

// Wait for consumer to process
await Task.Delay(2000);

// Check booking status
var updatedBooking = await bookingService.GetBookingByIdAsync(booking.Id);
Assert.Equal("CONFIRMED", updatedBooking.Status);
```

---

## Further Reading

### RabbitMQ Resources
- [RabbitMQ Tutorials](https://www.rabbitmq.com/getstarted.html)
- [AMQP Protocol Concepts](https://www.rabbitmq.com/tutorials/amqp-concepts.html)
- [RabbitMQ Best Practices](https://www.cloudamqp.com/blog/part1-rabbitmq-best-practice.html)

### Event-Driven Architecture
- [Martin Fowler - Event-Driven Architecture](https://martinfowler.com/articles/201701-event-driven.html)
- [Microservices.io - Event-Driven Architecture](https://microservices.io/patterns/data/event-driven-architecture.html)

### Related Patterns
- **Outbox Pattern**: Reliably publish events using database transactions
- **Saga Pattern**: Coordinate complex workflows across services
- **CQRS**: Separate read and write models with events

---

## Summary

The Event Bus is the **backbone of asynchronous communication** in our microservices architecture:

1. **Publishers** send events when something happens
2. **RabbitMQ** stores and routes messages reliably
3. **Consumers** process events independently
4. Services remain **decoupled** and **resilient**

This architecture enables us to build scalable, maintainable microservices that can evolve independently while maintaining reliable communication.

---

**Last Updated**: November 3, 2025  
**Related Documentation**: 
- [Project README](../README.md)
- [Authorization Guide](AUTHORIZATION_GUIDE.md)
- [Docker Setup](DOCKER_SETUP.md)
