# 🎭 Event Choreography Pattern

**Category**: Communication Patterns  
**Difficulty**: Intermediate to Advanced  
**Focus**: Decentralized workflow coordination

---

## 📖 What is Event Choreography?

**Event Choreography** is a pattern where services react to events independently without a central coordinator. Each service knows **what to do** when an event occurs, but services don't directly call each other.

Think of it like a **dance choreography** - each dancer knows their moves and when to perform them based on music (events), without a director telling them what to do.

---

## 🎯 Choreography vs Orchestration

### Choreography (Decentralized)

```
❌ No Central Coordinator

BookingService → BookingCreatedEvent → PaymentService
                                            ↓
                                      Processes Payment
                                            ↓
                                  PaymentSucceededEvent
                                            ↓
               BookingService ← Updates Status

Each service reacts independently to events
```

### Orchestration (Centralized)

```
✅ Central Orchestrator

                    Orchestrator
                         │
        ┌────────────────┼────────────────┐
        │                │                │
        ▼                ▼                ▼
BookingService    PaymentService   NotificationService
        ↓                ↓                ↓
   Creates          Processes        Sends Email
   Booking          Payment          
        ↓                ↓                ↓
        └────────────────┴────────────────┘
                         │
                    Orchestrator
                 (Controls flow)
```

---

## 🏗️ Implementation in Your Project

### Current Flow (Choreography)

```
1. User creates booking
   ↓
2. BookingService
   - Saves booking (status=PENDING)
   - Publishes BookingCreatedEvent
   - Returns 201 to client
   
3. PaymentService (reacts independently)
   - Consumes BookingCreatedEvent
   - Processes payment
   - Publishes PaymentSucceededEvent OR PaymentFailedEvent
   
4. BookingService (reacts independently)
   - Consumes PaymentSucceededEvent
   - Updates booking (status=CONFIRMED)
   
   OR
   
   - Consumes PaymentFailedEvent
   - Updates booking (status=CANCELLED)
```

### Code Example

**BookingService (Step 2)**:

```csharp
public async Task<BookingResponse> CreateBookingAsync(CreateBookingRequest request)
{
    // Create booking
    var booking = new Booking
    {
        Id = Guid.NewGuid(),
        UserId = request.UserId,
        Status = "PENDING"
    };
    
    await _dbContext.Bookings.AddAsync(booking);
    await _dbContext.SaveChangesAsync();
    
    // Publish event (fire and forget!)
    await _eventBus.PublishAsync(new BookingCreatedEvent
    {
        BookingId = booking.Id,
        UserId = booking.UserId,
        Amount = booking.Amount
    });
    
    // BookingService doesn't know or care what happens next
    return new BookingResponse { BookingId = booking.Id };
}
```

**PaymentService (Step 3)** - Reacts independently:

```csharp
public class BookingCreatedEventHandler : IEventHandler<BookingCreatedEvent>
{
    public async Task HandleAsync(BookingCreatedEvent @event)
    {
        // PaymentService decides what to do
        // BookingService didn't tell it to do anything
        
        var payment = await _paymentGateway.ProcessAsync(@event.Amount);
        
        if (payment.Success)
        {
            // Publish success event
            await _eventBus.PublishAsync(new PaymentSucceededEvent
            {
                BookingId = @event.BookingId,
                PaymentId = payment.Id
            });
        }
        else
        {
            // Publish failure event
            await _eventBus.PublishAsync(new PaymentFailedEvent
            {
                BookingId = @event.BookingId,
                Reason = payment.ErrorMessage
            });
        }
    }
}
```

**BookingService (Step 4)** - Reacts independently:

```csharp
public class PaymentSucceededEventHandler : IEventHandler<PaymentSucceededEvent>
{
    public async Task HandleAsync(PaymentSucceededEvent @event)
    {
        // BookingService decides what to do when payment succeeds
        // PaymentService didn't tell it to update status
        
        var booking = await _dbContext.Bookings.FindAsync(@event.BookingId);
        booking.Status = "CONFIRMED";
        booking.UpdatedAt = DateTime.UtcNow;
        
        await _dbContext.SaveChangesAsync();
        
        _logger.LogInformation("Booking {BookingId} confirmed", @event.BookingId);
    }
}
```

---

## ✅ Benefits of Choreography

| Benefit | Description | Example in Your Project |
|---------|-------------|------------------------|
| **Loose Coupling** | Services don't know about each other | BookingService doesn't call PaymentService directly |
| **Easy to Extend** | Add new services without changes | Add NotificationService without modifying BookingService |
| **No Single Point of Failure** | No central coordinator to fail | If one service down, others continue |
| **Independent Deployment** | Deploy services separately | Deploy PaymentService without redeploying BookingService |
| **Parallel Execution** | Multiple services react simultaneously | NotificationService and AnalyticsService both consume BookingCreatedEvent |

---

## ❌ Challenges of Choreography

| Challenge | Description | How to Handle |
|-----------|-------------|---------------|
| **Hard to Understand** | Workflow scattered across services | Document event flows, use Seq for tracing |
| **Difficult to Debug** | Follow events across multiple services | Correlation IDs, centralized logging |
| **No Central View** | Can't see complete workflow | Event flow diagrams, monitoring dashboards |
| **Circular Dependencies** | Service A → B → C → A (deadlock) | Careful event design, avoid cycles |
| **Eventual Consistency** | Data inconsistent for brief period | Accept trade-off, design for it |

---

## 🔄 Real-World Workflow Example

### Scenario: Complete Booking Workflow

**With Choreography (Your Project)**:

```
1. BookingService publishes BookingCreatedEvent
   
2. Multiple services react independently:
   
   PaymentService:
   - Consumes BookingCreatedEvent
   - Processes payment
   - Publishes PaymentSucceededEvent
   
   NotificationService (future):
   - Consumes BookingCreatedEvent
   - Sends confirmation email to user
   
   AnalyticsService (future):
   - Consumes BookingCreatedEvent
   - Records booking analytics
   
3. BookingService reacts to PaymentSucceededEvent:
   - Updates booking status to CONFIRMED
   
4. NotificationService reacts to PaymentSucceededEvent:
   - Sends payment confirmation email
```

**Timeline**:

```
t=0ms     BookingCreatedEvent published
t=1ms     PaymentService, NotificationService, AnalyticsService all receive event
          (Parallel processing)
t=50ms    NotificationService sends email (fast)
t=100ms   AnalyticsService records data (fast)
t=5000ms  PaymentService finishes payment (slow)
t=5001ms  PaymentSucceededEvent published
t=5002ms  BookingService updates booking
t=5003ms  NotificationService sends payment confirmation

All services work independently, no waiting!
```

---

## 🎯 Design Principles

### 1. Events Should Be Facts

```csharp
// ✅ Good: Event states a fact (past tense)
public class BookingCreatedEvent
{
    public Guid BookingId { get; set; }
    public string UserId { get; set; }
    // ...
}

// ❌ Bad: Command tells service what to do
public class CreateBookingCommand // This is orchestration, not choreography!
{
    public string UserId { get; set; }
    // ...
}
```

### 2. Services React, Not Instructed

```csharp
// ✅ Good: Service decides what to do
public class BookingCreatedEventHandler
{
    public async Task HandleAsync(BookingCreatedEvent @event)
    {
        // PaymentService DECIDES to process payment
        // No one told it to do this
        await _paymentService.ProcessAsync(@event);
    }
}

// ❌ Bad: Service told what to do (orchestration)
public async Task ProcessBooking(Guid bookingId)
{
    // Orchestrator tells PaymentService: "process this payment"
    await _paymentService.ProcessPaymentAsync(bookingId);
}
```

### 3. No Response Expected

```csharp
// ✅ Good: Fire and forget
await _eventBus.PublishAsync(new BookingCreatedEvent { /* ... */ });
// BookingService doesn't wait for response

// ❌ Bad: Waiting for response (synchronous, not choreography)
var result = await _paymentService.ProcessPaymentAsync(booking);
if (result.Success) { /* ... */ }
```

---

## 🛡️ Handling Failures in Choreography

### Compensating Events

When something fails, publish a compensating event:

```csharp
public class PaymentFailedEventHandler : IEventHandler<PaymentFailedEvent>
{
    public async Task HandleAsync(PaymentFailedEvent @event)
    {
        // Compensate: Cancel the booking
        var booking = await _dbContext.Bookings.FindAsync(@event.BookingId);
        booking.Status = "CANCELLED";
        booking.CancellationReason = @event.Reason;
        
        await _dbContext.SaveChangesAsync();
        
        // Publish compensating event
        await _eventBus.PublishAsync(new BookingCancelledEvent
        {
            BookingId = @event.BookingId,
            Reason = @event.Reason
        });
    }
}
```

### Saga Pattern (Advanced)

For complex workflows with many steps:

```
Saga Execution Coordinator (SEC)
   ↓
1. BookingCreated → Reserve Inventory
   ✅ Success → InventoryReserved event
   
2. InventoryReserved → Process Payment
   ❌ Failed → PaymentFailed event
   
3. PaymentFailed → Compensate
   ↓
   Release Inventory → InventoryReleased event
   Cancel Booking → BookingCancelled event
```

---

## 📊 Monitoring Choreography

### Correlation ID Tracking

Track events across services:

```csharp
public class BookingCreatedEvent
{
    public Guid EventId { get; set; }
    public Guid CorrelationId { get; set; } // ← Track related events
    public Guid BookingId { get; set; }
    // ...
}

// In Seq query:
// CorrelationId = "abc-123"
// Shows all events for this booking workflow
```

### Event Flow Visualization

**Seq Query** to trace workflow:

```
CorrelationId = "abc-123"
| order by @Timestamp
| select @Timestamp, EventName, Service, BookingId
```

**Result**:

```
10:00:00  BookingCreated      BookingService   booking-456
10:00:01  PaymentInitiated    PaymentService   booking-456
10:00:30  PaymentSucceeded    PaymentService   booking-456
10:00:31  BookingConfirmed    BookingService   booking-456
```

---

## 🎓 Key Takeaways

### When to Use Choreography

✅ **Good For**:
- Simple workflows (2-3 steps)
- Independent services
- Parallel processing
- Event notifications
- Extensible systems (easy to add services)

❌ **Not Good For**:
- Complex workflows (5+ steps)
- Strict ordering requirements
- Transactional consistency needed
- Need centralized control
- Difficult to debug workflows

### Your Project Uses Choreography Because

1. **Simple Workflow**: Create Booking → Process Payment → Update Status (3 steps)
2. **Loose Coupling**: Services independent, easy to add NotificationService later
3. **Scalability**: Services can scale independently
4. **Resilience**: No single point of failure

### Comparison Summary

| Aspect | Choreography | Orchestration |
|--------|-------------|---------------|
| **Coordinator** | None (decentralized) | Central orchestrator |
| **Coupling** | Loose | Tight (to orchestrator) |
| **Complexity** | Simple workflows | Complex workflows |
| **Visibility** | Hard to see full flow | Easy to see full flow |
| **Failure Handling** | Compensating events | Orchestrator handles |
| **Debugging** | Harder (distributed) | Easier (centralized) |

---

## 📚 Further Reading

- **Event-Driven Architecture**: [Event-Driven Architecture](../01-architecture-patterns/event-driven-architecture.md)
- **Saga Pattern** (orchestration alternative): Future document
- **Project Docs**: `/docs/phase3-event-integration/`

### External Resources

- [Martin Fowler - Event Collaboration](https://martinfowler.com/eaaDev/EventCollaboration.html)
- [Chris Richardson - Choreography vs Orchestration](https://microservices.io/patterns/data/saga.html)

---

## ❓ Interview Questions

1. What is event choreography?
2. How does choreography differ from orchestration?
3. What are the benefits and challenges of choreography?
4. How do you handle failures in choreography?
5. When would you choose orchestration over choreography?
6. How do you debug issues in choreographed workflows?
7. Explain compensating events.

---

**Last Updated**: November 11, 2025  
**Status**: ✅ Implemented in project  
**Pattern**: Decentralized event-driven communication
