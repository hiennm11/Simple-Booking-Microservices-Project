# 🔄 Event-Driven Architecture (EDA)

**Category**: Architecture Patterns  
**Difficulty**: Intermediate  
**Implementation Status**: ✅ Complete in this project

---

## 📖 What is Event-Driven Architecture?

**Event-Driven Architecture (EDA)** is a software design pattern where services communicate by producing and consuming **events** rather than making direct synchronous calls.

### Key Concepts

**Event**: A notification that something significant has happened
```json
{
  "eventId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "eventName": "PaymentSucceeded",
  "timestamp": "2025-11-07T10:30:00Z",
  "data": {
    "paymentId": "payment-123",
    "bookingId": "booking-456",
    "amount": 500000
  }
}
```

**Producer**: Service that publishes events  
**Consumer**: Service that subscribes to and processes events  
**Event Bus**: Infrastructure that routes events (RabbitMQ, Kafka, etc.)

---

## 🎯 Why Event-Driven Architecture?

### Benefits

| Benefit | Description | Example in Our Project |
|---------|-------------|----------------------|
| **Loose Coupling** | Services don't need to know about each other | PaymentService doesn't call BookingService directly |
| **Asynchronous** | Non-blocking operations | BookingService doesn't wait for payment processing |
| **Scalability** | Process events at different rates | Scale PaymentService independently |
| **Resilience** | Events can be retried if processing fails | Retry failed payments automatically |
| **Auditability** | Event log provides complete history | Track full booking lifecycle |
| **Extensibility** | Add new consumers without changing producers | Add NotificationService without changing BookingService |

### Synchronous vs Asynchronous Communication

```
SYNCHRONOUS (Request/Response)
Client → API Gateway → BookingService → PaymentService
                              ↑________________↓
                            (Waits for response)
❌ Tight coupling
❌ Client waits for all processing
❌ If PaymentService is down, booking fails

ASYNCHRONOUS (Event-Driven)
Client → API Gateway → BookingService → RabbitMQ → PaymentService
                              ↓
                        Response 201 Created
✅ Loose coupling
✅ Client gets immediate response
✅ If PaymentService is down, event queued for later
```

---

## 🏗️ In This Project: Implementation

### Event Flow Diagram

```
1. Create Booking
┌────────┐  POST /bookings  ┌──────────────┐
│ Client │ ───────────────► │BookingService│
└────────┘                  └──────┬───────┘
                                   │
                                   │ 1. Save to DB
                                   │    (Status: PENDING)
                                   │
                                   │ 2. Publish Event
                                   ▼
                            ┌─────────────┐
                            │  RabbitMQ   │
                            │   Queue:    │
                            │booking_     │
                            │created      │
                            └──────┬──────┘
                                   │
                                   │ 3. Consume Event
                                   ▼
2. Process Payment          ┌──────────────┐
                            │PaymentService│
                            └──────┬───────┘
                                   │
                                   │ 4. Process Payment
                                   │ 5. Save to DB
                                   │ 6. Publish Event
                                   ▼
                            ┌─────────────┐
                            │  RabbitMQ   │
                            │   Queue:    │
                            │payment_     │
                            │succeeded    │
                            └──────┬──────┘
                                   │
3. Update Booking                  │ 7. Consume Event
                                   ▼
                            ┌──────────────┐
                            │BookingService│
                            │(Consumer)    │
                            └──────┬───────┘
                                   │
                                   │ 8. Update Status
                                   │    (Status: CONFIRMED)
                                   ▼
                                  DB
```

### Event Catalog

#### Event 1: BookingCreated

**Publisher**: BookingService  
**Consumers**: PaymentService  
**Queue**: `booking_created`  
**Routing Key**: `booking.created`

**Schema**:
```json
{
  "eventId": "uuid",
  "eventName": "BookingCreated",
  "timestamp": "ISO 8601",
  "correlationId": "uuid",
  "data": {
    "bookingId": "uuid",
    "userId": "uuid",
    "roomId": "string",
    "amount": "decimal",
    "status": "PENDING"
  }
}
```

**When Published**: After booking is saved to database

**Code Location**: `src/BookingService/Services/BookingServiceImpl.cs`

#### Event 2: PaymentSucceeded

**Publisher**: PaymentService  
**Consumers**: BookingService  
**Queue**: `payment_succeeded`  
**Routing Key**: `payment.succeeded`

**Schema**:
```json
{
  "eventId": "uuid",
  "eventName": "PaymentSucceeded",
  "timestamp": "ISO 8601",
  "correlationId": "uuid",
  "data": {
    "paymentId": "uuid",
    "bookingId": "uuid",
    "amount": "decimal",
    "status": "SUCCESS"
  }
}
```

**When Published**: After payment is processed successfully

**Code Location**: `src/PaymentService/Services/PaymentServiceImpl.cs`

#### Event 3: PaymentFailed (Future)

**Publisher**: PaymentService  
**Consumers**: BookingService, NotificationService  
**Queue**: `payment_failed`

**Schema**:
```json
{
  "eventId": "uuid",
  "eventName": "PaymentFailed",
  "timestamp": "ISO 8601",
  "data": {
    "paymentId": "uuid",
    "bookingId": "uuid",
    "reason": "Insufficient funds",
    "status": "FAILED"
  }
}
```

---

## 🔧 RabbitMQ Configuration

### Exchange and Queue Setup

```
Exchange: booking-exchange (Topic)
   │
   ├─→ Queue: booking_created
   │   Binding: booking.created
   │   Consumer: PaymentService
   │
   └─→ Queue: payment_succeeded
       Binding: payment.succeeded
       Consumer: BookingService
```

### Connection Settings

```json
{
  "RabbitMQ": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest",
    "Exchange": "booking-exchange",
    "ExchangeType": "topic",
    "Queues": {
      "BookingCreated": "booking_created",
      "PaymentSucceeded": "payment_succeeded",
      "PaymentFailed": "payment_failed"
    }
  }
}
```

---

## 📝 Implementation Code Examples

### Publishing an Event

**File**: `src/BookingService/Services/BookingServiceImpl.cs`

```csharp
public async Task<BookingResponse> CreateBookingAsync(CreateBookingRequest request)
{
    // 1. Create and save booking to database
    var booking = new Booking
    {
        Id = Guid.NewGuid(),
        UserId = request.UserId,
        RoomId = request.RoomId,
        Amount = request.Amount,
        Status = "PENDING",
        CreatedAt = DateTime.UtcNow
    };

    _dbContext.Bookings.Add(booking);
    await _dbContext.SaveChangesAsync();

    // 2. Create event
    var bookingEvent = new BookingCreatedEvent
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

    // 3. Publish event to RabbitMQ
    await _eventBus.PublishAsync(
        bookingEvent, 
        queueName: "booking_created",
        routingKey: "booking.created"
    );

    _logger.LogInformation(
        "Published BookingCreated event for booking {BookingId}", 
        booking.Id
    );

    return new BookingResponse { /* ... */ };
}
```

### Consuming an Event

**File**: `src/PaymentService/Consumers/BookingCreatedConsumer.cs`

```csharp
public class BookingCreatedConsumer : IEventConsumer<BookingCreatedEvent>
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<BookingCreatedConsumer> _logger;

    public async Task ConsumeAsync(BookingCreatedEvent @event)
    {
        _logger.LogInformation(
            "Received BookingCreated event for booking {BookingId}",
            @event.Data.BookingId
        );

        try
        {
            // Process the payment
            var paymentRequest = new ProcessPaymentRequest
            {
                BookingId = @event.Data.BookingId,
                Amount = @event.Data.Amount
            };

            await _paymentService.ProcessPaymentAsync(paymentRequest);

            _logger.LogInformation(
                "Successfully processed payment for booking {BookingId}",
                @event.Data.BookingId
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to process payment for booking {BookingId}",
                @event.Data.BookingId
            );
            
            // Message will be requeued or sent to DLQ
            throw;
        }
    }
}
```

### Event Bus Interface

**File**: `src/Shared/EventBus/IEventBus.cs`

```csharp
public interface IEventBus
{
    /// <summary>
    /// Publish an event to the message broker
    /// </summary>
    Task PublishAsync<T>(
        T @event, 
        string queueName, 
        string routingKey = null
    ) where T : class;

    /// <summary>
    /// Subscribe to events and process them with a consumer
    /// </summary>
    Task SubscribeAsync<TEvent, TConsumer>(string queueName)
        where TEvent : class
        where TConsumer : IEventConsumer<TEvent>;
}
```

---

## 🎨 Event Design Patterns

### 1. Event Choreography (Used in This Project)

**Definition**: Services react to events independently without central coordination.

```
BookingService ──► RabbitMQ ──► PaymentService
      ▲                              │
      │                              │
      └──────────── RabbitMQ ◄───────┘
```

**Pros**:
- ✅ Loose coupling
- ✅ Easy to add new services
- ✅ No single point of failure

**Cons**:
- ❌ Hard to understand full workflow
- ❌ No central control
- ❌ Harder to debug

### 2. Event Orchestration (Future: Saga Pattern)

**Definition**: Central orchestrator coordinates the workflow.

```
              Saga Orchestrator
                     │
        ┌────────────┼────────────┐
        │            │            │
        ▼            ▼            ▼
  BookingService  PaymentService  NotificationService
```

**Pros**:
- ✅ Clear workflow visibility
- ✅ Easy to debug
- ✅ Centralized error handling

**Cons**:
- ❌ Orchestrator is single point of failure
- ❌ Tight coupling to orchestrator
- ❌ More complex to implement

---

## 🛡️ Reliability Patterns (Implemented)

### 1. Retry with Exponential Backoff

**Problem**: Transient failures when publishing events

**Solution**: Retry with increasing delays

```csharp
// Polly resilience pipeline
var retryPipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(2),
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true
    })
    .Build();

await retryPipeline.ExecuteAsync(async ct =>
{
    await _eventBus.PublishAsync(@event, queueName);
}, cancellationToken);
```

**See**: `/docs/phase3-event-integration/RETRY_LOGIC_AND_POLLY.md`

### 2. Outbox Pattern

**Problem**: Event lost if RabbitMQ is down when publishing

**Solution**: Save events to database first, publish later

```
1. Begin Transaction
2. ├─ Save Booking to DB
3. └─ Save Event to Outbox Table
4. Commit Transaction
5. Background Job: Publish events from Outbox
```

**Benefits**:
- ✅ Guaranteed event delivery
- ✅ Atomic with business transaction
- ✅ Survives RabbitMQ downtime

**See**: `/docs/phase6-advanced/OUTBOX_PATTERN_IMPLEMENTATION.md`

### 3. Dead Letter Queue (DLQ)

**Problem**: Poison messages that continuously fail processing

**Solution**: Move to DLQ after max retries

```
Event → Consumer → Fails → Retry 1 → Fails → Retry 2 → Fails → DLQ
```

**Configuration**:
```csharp
public async Task ConsumeAsync(BookingCreatedEvent @event)
{
    const int MaxRetries = 3;
    
    if (redeliveryCount >= MaxRetries)
    {
        _logger.LogError("Max retries exhausted, sending to DLQ");
        channel.BasicReject(deliveryTag, requeue: false);
        return;
    }
    
    try
    {
        await ProcessEventAsync(@event);
    }
    catch
    {
        // Requeue for retry
        channel.BasicNack(deliveryTag, multiple: false, requeue: true);
    }
}
```

---

## 🎯 Best Practices

### Event Design

1. **Use Past Tense for Event Names**
   - ✅ `BookingCreated`, `PaymentSucceeded`
   - ❌ `CreateBooking`, `SucceedPayment`

2. **Include All Necessary Data**
   - Include `bookingId`, `amount`, etc.
   - Consumers shouldn't need to call back to producer

3. **Include Metadata**
   - `eventId`: Unique identifier
   - `timestamp`: When it happened
   - `correlationId`: Track related events

4. **Version Your Events**
   ```json
   {
     "eventName": "BookingCreated",
     "version": "v1",
     "data": { /* ... */ }
   }
   ```

### Consumer Design

1. **Idempotency**
   - Handle duplicate events safely
   - Check if already processed

   ```csharp
   public async Task ConsumeAsync(PaymentSucceededEvent @event)
   {
       // Check if already processed
       var existing = await _dbContext.Bookings
           .FirstOrDefaultAsync(b => b.Id == @event.Data.BookingId);
       
       if (existing.Status == "CONFIRMED")
       {
           _logger.LogInformation("Already processed, skipping");
           return; // Idempotent!
       }
       
       // Process...
   }
   ```

2. **Error Handling**
   - Distinguish between transient and permanent errors
   - Retry transient, reject permanent

3. **Correlation ID**
   - Track requests across services
   - Include in all logs

   ```csharp
   var correlationId = @event.CorrelationId ?? Guid.NewGuid();
   using (_logger.BeginScope("CorrelationId: {CorrelationId}", correlationId))
   {
       // All logs include correlationId
   }
   ```

---

## 📊 Real-World Applications

### E-Commerce Order Processing
```
Order Created → Inventory Reserved → Payment Processed → 
Shipment Created → Email Sent → Order Completed
```

### Banking Transaction
```
Transfer Initiated → Debit Account → Credit Account → 
Notification Sent → Audit Log Created
```

### Food Delivery
```
Order Placed → Restaurant Notified → Order Accepted → 
Driver Assigned → Food Picked Up → Delivered → Rated
```

---

## 🎓 Key Takeaways

1. **Event-Driven = Asynchronous Communication**
   - Services publish events, others subscribe
   - Loose coupling between services

2. **Events vs Commands**
   - **Event**: "Something happened" (past tense)
   - **Command**: "Do something" (imperative)

3. **Eventual Consistency**
   - Data becomes consistent over time
   - Trade-off for better scalability

4. **Reliability Patterns Required**
   - Retry with backoff
   - Outbox pattern for guaranteed delivery
   - Dead letter queue for poison messages

5. **Idempotency is Critical**
   - Same event processed multiple times = same result
   - Essential for "at-least-once" delivery

---

## 🧪 Hands-On Exercise

### Test Event Flow

1. **Create a booking**
   ```bash
   POST http://localhost:5000/booking/api/bookings
   {
     "userId": "user-guid",
     "roomId": "ROOM-101",
     "amount": 500000
   }
   ```

2. **Check RabbitMQ Management UI**
   - URL: http://localhost:15672
   - Queue: `booking_created`
   - Verify message published

3. **Check Seq Logs**
   - URL: http://localhost:5341
   - Search: `BookingCreated`
   - Follow correlation ID

4. **Verify Database Updates**
   ```sql
   -- BookingService DB
   SELECT * FROM bookings WHERE id = 'booking-id';
   -- Status should be PENDING, then CONFIRMED
   
   -- PaymentService DB
   db.payments.find({ bookingId: 'booking-id' })
   ```

---

## 📚 Further Reading

### Books
- **"Enterprise Integration Patterns"** by Gregor Hohpe
  - Chapter on Event-Driven Architecture
- **"Building Event-Driven Microservices"** by Adam Bellemare

### Online Resources
- [AWS EventBridge Patterns](https://aws.amazon.com/eventbridge/patterns/)
- [Martin Fowler - Event-Driven Architecture](https://martinfowler.com/articles/201701-event-driven.html)

---

## ❓ Interview Questions

1. What is event-driven architecture?
2. What's the difference between events and commands?
3. How do you ensure event delivery reliability?
4. What is eventual consistency?
5. Explain the Outbox pattern and why it's needed.
6. How do you handle duplicate events (idempotency)?
7. What's the difference between choreography and orchestration?
8. How do you debug issues in event-driven systems?

---

**Last Updated**: November 7, 2025  
**Code Reference**: `/src/Shared/EventBus/`, `/src/BookingService/Services/`, `/src/PaymentService/Consumers/`  
**Related Docs**:
- [Outbox Pattern](./outbox-pattern.md)
- [RabbitMQ Messaging](../02-communication/rabbitmq-messaging.md)
- [Retry Patterns](../03-resilience/retry-patterns-polly.md)
