# 📨 RabbitMQ Messaging in Microservices

**Category**: Communication Patterns  
**Difficulty**: Intermediate  
**Focus**: Reliable message-based communication

---

## 📖 Overview

This document covers how RabbitMQ enables asynchronous, reliable communication between microservices in the Booking System project.

**Quick Links**:
- Detailed Implementation: [RabbitMQ Implementation](../02-event-driven/rabbitmq-implementation.md)
- Architecture: [Event-Driven Architecture](../01-architecture-patterns/event-driven-architecture.md)

---

## 🎯 Key Concepts

### Message Broker Role

```
Producer Service → RabbitMQ (Broker) → Consumer Service
     ↓               ↓                      ↓
Publishes       Stores & Routes         Consumes
   Event           Message                Event
```

**RabbitMQ acts as middleman**:
- Receives messages from producers
- Stores them reliably (persists to disk)
- Routes to appropriate queues
- Delivers to consumers
- Handles retries on failure

---

## 🏗️ Architecture in Your Project

### Components

```
BookingService (Producer)
    ↓
 Publishes BookingCreatedEvent
    ↓
RabbitMQ Exchange (booking_system_exchange)
    ↓
 Routes by event name
    ↓
Queue (booking_created_queue)
    ↓
 Delivers event
    ↓
PaymentService (Consumer)
```

### Event Catalog

| Event | Producer | Consumer | Queue | When Triggered |
|-------|----------|----------|-------|----------------|
| `BookingCreated` | BookingService | PaymentService | `booking_created_queue` | After booking saved to DB |
| `PaymentSucceeded` | PaymentService | BookingService | `payment_succeeded_queue` | After payment processed successfully |
| `PaymentFailed` | PaymentService | BookingService | `payment_failed_queue` | When payment processing fails |

---

## 📨 Publishing Messages

### Basic Flow

```csharp
// 1. Create domain event
var @event = new BookingCreatedEvent
{
    EventId = Guid.NewGuid(),
    BookingId = booking.Id,
    UserId = booking.UserId,
    Amount = booking.Amount,
    CreatedAt = DateTime.UtcNow
};

// 2. Publish to RabbitMQ
await _eventBus.PublishAsync(@event);

// 3. Event persisted in queue (fire and forget!)
```

### Reliability Features

**Message Persistence**:
```csharp
var properties = _channel.CreateBasicProperties();
properties.Persistent = true; // Survive broker restart
properties.DeliveryMode = 2;   // Persistent delivery
```

**Publisher Confirms**:
```csharp
// RabbitMQ confirms message received
_channel.ConfirmSelect();
_channel.BasicPublish(/* ... */);
_channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));
```

**Retry Logic** (with Polly):
```csharp
var retryPolicy = Policy
    .Handle<BrokerUnreachableException>()
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))
    );

await retryPolicy.ExecuteAsync(async () =>
{
    await _eventBus.PublishAsync(@event);
});
```

---

## 📥 Consuming Messages

### Basic Consumer

```csharp
public class BookingCreatedEventHandler : IEventHandler<BookingCreatedEvent>
{
    public async Task HandleAsync(BookingCreatedEvent @event)
    {
        _logger.LogInformation(
            "Processing BookingCreatedEvent for booking {BookingId}",
            @event.BookingId
        );
        
        // Process event
        await _paymentService.ProcessPaymentAsync(@event);
        
        // Auto-acknowledgment after successful processing
    }
}
```

### Manual Acknowledgment

```csharp
var consumer = new EventingBasicConsumer(_channel);
consumer.Received += async (model, ea) =>
{
    try
    {
        // Process message
        await ProcessEventAsync(ea.Body);
        
        // Acknowledge (remove from queue)
        _channel.BasicAck(ea.DeliveryTag, multiple: false);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to process message");
        
        // Negative acknowledge (requeue or dead-letter)
        _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
    }
};
```

---

## 🔄 Message Flow Examples

### Example 1: Successful Flow

```
Timeline:
t=0ms    BookingService publishes BookingCreatedEvent
t=1ms    RabbitMQ receives and stores event in queue
t=2ms    RabbitMQ delivers to PaymentService consumer
t=50ms   PaymentService processes payment
t=51ms   PaymentService acknowledges message (removed from queue)
t=52ms   PaymentService publishes PaymentSucceededEvent
t=53ms   BookingService receives and updates booking status

Result: ✅ Booking confirmed, payment processed
```

### Example 2: Consumer Failure with Retry

```
Timeline:
t=0ms    PaymentService receives BookingCreatedEvent
t=10ms   PaymentService processing fails (database timeout)
t=11ms   PaymentService sends NACK (requeue=true)
t=12ms   RabbitMQ requeues message
t=5s     RabbitMQ redelivers message (retry #1)
t=5.1s   PaymentService processes successfully
t=5.2s   PaymentService acknowledges message

Result: ✅ Automatic retry succeeded
```

### Example 3: Broker Downtime

```
Timeline:
t=0ms    BookingService tries to publish event
t=1ms    RabbitMQ is down (connection error)
t=2ms    Polly retry policy kicks in
t=4ms    Retry attempt #1 (still down)
t=8ms    Retry attempt #2 (still down)
t=16ms   Retry attempt #3 (RabbitMQ back up)
t=17ms   Event published successfully

Result: ✅ Event published with automatic retry
```

---

## 🛡️ Reliability Patterns

### 1. Idempotent Consumers

Handle duplicate messages safely:

```csharp
public async Task HandleAsync(PaymentSucceededEvent @event)
{
    // Check if already processed
    var booking = await _dbContext.Bookings.FindAsync(@event.BookingId);
    
    if (booking.Status == "CONFIRMED")
    {
        _logger.LogWarning("Already processed, skipping");
        return; // Safe to process multiple times
    }
    
    // Process event
    booking.Status = "CONFIRMED";
    await _dbContext.SaveChangesAsync();
}
```

### 2. Dead Letter Queue (DLQ)

Move failed messages after max retries:

```csharp
// Configure DLQ
var args = new Dictionary<string, object>
{
    { "x-dead-letter-exchange", "dlx_exchange" },
    { "x-dead-letter-routing-key", "failed_messages" },
    { "x-max-retries", 5 }
};

_channel.QueueDeclare(
    queue: "booking_created_queue",
    durable: true,
    exclusive: false,
    autoDelete: false,
    arguments: args
);
```

### 3. Message TTL (Time To Live)

Expire old messages:

```csharp
var properties = _channel.CreateBasicProperties();
properties.Expiration = "60000"; // 60 seconds
_channel.BasicPublish(/* ... */, properties);
```

---

## 📊 Monitoring & Debugging

### RabbitMQ Management UI

**Access**: http://localhost:15672 (guest/guest)

**Key Metrics**:
- Messages ready: Waiting in queue
- Messages unacknowledged: Being processed
- Message rate: Messages/second
- Consumer count: Active consumers

### Logging

```csharp
_logger.LogInformation(
    "Published event {EventName} to queue {Queue} with ID {EventId}",
    @event.GetType().Name,
    queueName,
    @event.EventId
);

_logger.LogInformation(
    "Consumed event {EventName} from queue {Queue}, processing...",
    @event.GetType().Name,
    queueName
);
```

### Seq Queries

```
// Find all published events
@Level = "Information" AND @Message LIKE "%Published event%"

// Find failed event processing
@Level = "Error" AND @Message LIKE "%Failed to process%"

// Track specific booking events
BookingId = "your-booking-id"
```

---

## 🎯 Best Practices

### 1. Always Use Manual Ack

```csharp
// ✅ Good: Manual acknowledgment
_channel.BasicConsume(queue, autoAck: false, consumer);

// ❌ Bad: Auto acknowledgment (message lost on failure)
_channel.BasicConsume(queue, autoAck: true, consumer);
```

### 2. Set Prefetch Count

```csharp
// Limit unacknowledged messages per consumer
_channel.BasicQos(
    prefetchSize: 0,
    prefetchCount: 10, // Process max 10 messages at a time
    global: false
);
```

### 3. Use Durable Queues

```csharp
// ✅ Good: Survives broker restart
_channel.QueueDeclare(queue, durable: true, /* ... */);

// ❌ Bad: Lost on broker restart
_channel.QueueDeclare(queue, durable: false, /* ... */);
```

### 4. Include Correlation ID

```csharp
var properties = _channel.CreateBasicProperties();
properties.CorrelationId = correlationId.ToString();
properties.MessageId = @event.EventId.ToString();
```

### 5. Handle Poison Messages

```csharp
if (message.RetryCount >= MaxRetries)
{
    _logger.LogCritical("Poison message detected, sending to DLQ");
    _channel.BasicNack(deliveryTag, multiple: false, requeue: false);
    await PublishToDLQAsync(message);
    return;
}
```

---

## 🎓 Key Takeaways

1. **RabbitMQ = Reliable Message Delivery**
   - Persists messages to disk
   - Guarantees delivery with acknowledgments
   - Automatic retry on failure

2. **Loose Coupling**
   - Services don't know about each other
   - Add/remove consumers without changing producers

3. **Scalability**
   - Multiple consumers process in parallel
   - Queue buffers load spikes

4. **Reliability Patterns**
   - Idempotent consumers
   - Dead letter queues
   - Manual acknowledgment

5. **Monitor & Alert**
   - Watch queue depths
   - Track message rates
   - Alert on DLQ messages

---

## 📚 Further Reading

- **Detailed Implementation**: [RabbitMQ Implementation](../02-event-driven/rabbitmq-implementation.md)
- **Event Patterns**: [Event-Driven Architecture](../01-architecture-patterns/event-driven-architecture.md)
- **Project Docs**: `/docs/phase3-event-integration/`

---

**Last Updated**: November 11, 2025  
**Status**: ✅ Implemented  
**See Also**: [Event Choreography](./event-choreography.md), [Correlation Tracking](./correlation-tracking.md)
