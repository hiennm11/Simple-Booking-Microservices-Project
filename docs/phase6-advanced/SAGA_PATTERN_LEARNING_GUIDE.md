# Saga Pattern Implementation Guide

## 📚 Learning Objectives

By the end of this guide, you will understand:
1. What the Saga pattern is and why it's needed in microservices
2. The difference between choreography-based and orchestration-based sagas
3. How compensating actions work to maintain consistency
4. Real implementation in this booking system
5. Best practices and common pitfalls

---

## 🎯 What is the Saga Pattern?

### The Problem: Distributed Transactions

In a monolithic application, you can use **database transactions** to ensure data consistency:

```csharp
// Monolithic approach - Single database transaction
using (var transaction = dbContext.Database.BeginTransaction())
{
    // Create booking
    var booking = new Booking { Status = "PENDING" };
    dbContext.Bookings.Add(booking);
    
    // Reserve inventory
    var inventory = dbContext.Inventory.Find(roomId);
    inventory.AvailableQuantity--;
    
    // Process payment
    var payment = new Payment { Status = "SUCCESS" };
    dbContext.Payments.Add(payment);
    
    dbContext.SaveChanges();
    transaction.Commit(); // All or nothing!
}
```

**But in microservices**, each service has its own database:
- ❌ No shared database transactions (2PC/XA is too slow and complex)
- ❌ One service failure shouldn't block others
- ❌ Services need to be independently deployable

### The Solution: Saga Pattern

A **Saga** is a sequence of local transactions where each step:
1. Executes a local transaction in one service
2. Publishes an event to trigger the next step
3. If a step fails, executes **compensating transactions** to undo previous steps

> **Key Concept**: Instead of ACID transactions, we achieve **eventual consistency** through a series of coordinated local transactions.

---

## 🎭 Two Types of Sagas

### 1. Choreography-Based Saga (Used in This Project) ✅

Each service listens for events and decides what to do next. **No central coordinator**.

```
Create Booking → Publish BookingCreated
                  ↓
            Inventory Service listens
                  ↓
            Reserve Inventory → Publish InventoryReserved
                  ↓
            Payment Service listens
                  ↓
            Process Payment → Publish PaymentSucceeded OR PaymentFailed
                  ↓
            ← Compensating Actions if Failed ←
```

**Advantages**:
- ✅ Simple to implement for basic workflows
- ✅ Good for loosely coupled services
- ✅ No single point of failure

**Disadvantages**:
- ❌ Hard to understand full workflow (scattered across services)
- ❌ Cyclic dependencies possible
- ❌ Difficult to add new steps

### 2. Orchestration-Based Saga

A central **Saga Orchestrator** tells each service what to do.

```
                 Saga Orchestrator
                        ↓
        ┌───────────────┼───────────────┐
        ↓               ↓               ↓
  BookingService  InventoryService  PaymentService
        ↓               ↓               ↓
   Response       Response        Response
        └───────────────┼───────────────┘
                        ↓
              Decision: Continue or Rollback
```

**Advantages**:
- ✅ Easy to understand workflow (centralized logic)
- ✅ Easy to add new steps
- ✅ Better for complex workflows

**Disadvantages**:
- ❌ Additional complexity (orchestrator service)
- ❌ Single point of failure
- ❌ Orchestrator knows about all services (tight coupling)

---

## 🏗️ Saga Implementation in This Booking System

### Architecture Overview

This project uses **Choreography-Based Saga** with the following flow:

```
┌─────────────────────── HAPPY PATH ───────────────────────┐
│                                                           │
│  1. BookingCreated Event                                 │
│     → InventoryService                                    │
│                                                           │
│  2. InventoryReserved Event                              │
│     → PaymentService                                      │
│                                                           │
│  3. PaymentSucceeded Event                               │
│     → BookingService (confirm)                           │
│     → InventoryService (confirm)                         │
│                                                           │
└───────────────────────────────────────────────────────────┘

┌─────────────────── COMPENSATING PATH ────────────────────┐
│                                                           │
│  3a. PaymentFailed Event                                 │
│      → InventoryService: Release reservation ⏪          │
│      → BookingService: Cancel booking ⏪                 │
│                                                           │
│  OR                                                       │
│                                                           │
│  1a. InventoryReservationFailed Event                    │
│      → BookingService: Cancel booking ⏪                 │
│                                                           │
└───────────────────────────────────────────────────────────┘
```

### Scenario 1: Successful Booking (Happy Path) ✅

```csharp
// Step 1: BookingService creates booking
POST /api/booking
→ Status: PENDING
→ Saves to outbox: BookingCreatedEvent
→ Outbox publisher sends to RabbitMQ

// Step 2: InventoryService receives BookingCreatedEvent
InventoryService.BookingCreatedConsumer
→ Check inventory availability
→ Create reservation (Status: RESERVED)
→ Update: AvailableQuantity--, ReservedQuantity++
→ Publish: InventoryReservedEvent

// Step 3: PaymentService receives InventoryReservedEvent
PaymentService.InventoryReservedConsumer
→ Process payment (70% success simulation)
→ Create payment record
→ Publish: PaymentSucceededEvent

// Step 4a: BookingService receives PaymentSucceededEvent
BookingService.PaymentSucceededConsumer
→ Update booking: Status = CONFIRMED
→ Done! ✅

// Step 4b: InventoryService receives PaymentSucceededEvent
InventoryService.PaymentSucceededConsumer
→ Update reservation: Status = CONFIRMED
→ Inventory finalized ✅
```

### Scenario 2: Payment Failure with Compensation ❌➡️🔄

```csharp
// Steps 1-2: Same as happy path (booking created, inventory reserved)

// Step 3: PaymentService FAILS
PaymentService.InventoryReservedConsumer
→ Payment processing fails (30% failure simulation)
→ Create payment record (Status: FAILED)
→ Publish: PaymentFailedEvent

// Step 4: COMPENSATING ACTIONS BEGIN 🔄

// 4a: InventoryService receives PaymentFailedEvent
InventoryService.PaymentFailedConsumer
→ Find reservation by BookingId
→ Update: Status = RELEASED, ReleasedAt = NOW
→ Update: AvailableQuantity++, ReservedQuantity--
→ Publish: InventoryReleasedEvent
→ ✅ Inventory returned to pool

// 4b: BookingService receives PaymentFailedEvent
BookingService.PaymentFailedConsumer
→ Update booking: Status = CANCELLED
→ Set: CancellationReason = "Payment failed: ..."
→ ✅ Booking cancelled

// RESULT: System back to consistent state, ready for new bookings
```

### Scenario 3: Insufficient Inventory ⛔

```csharp
// Step 1: BookingService creates booking (as usual)

// Step 2: InventoryService receives BookingCreatedEvent
InventoryService.BookingCreatedConsumer
→ Check inventory: AvailableQuantity = 0 ⛔
→ Catch InvalidOperationException("Insufficient inventory")
→ Log WARNING (not ERROR)
→ Publish: InventoryReservationFailedEvent

// Step 3: IMMEDIATE COMPENSATION 🔄
BookingService.InventoryReservationFailedConsumer
→ Update booking: Status = CANCELLED
→ Set: CancellationReason = "Inventory reservation failed: ..."
→ Save to outbox: BookingCancelledEvent
→ ✅ Booking cancelled gracefully

// RESULT: No payment attempted, resources not wasted
```

---

## 🔧 Implementation Details

### Key Components

#### 1. Event Contracts (Shared/Contracts/)

```csharp
// Success events
public class BookingCreatedEvent { /* BookingId, UserId, RoomId, Amount */ }
public class InventoryReservedEvent { /* ReservationId, BookingId, ItemId */ }
public class PaymentSucceededEvent { /* PaymentId, BookingId, Amount */ }

// Failure events (trigger compensations)
public class PaymentFailedEvent { /* PaymentId, BookingId, Reason */ }
public class InventoryReservationFailedEvent { /* BookingId, ItemId, Reason */ }

// Compensation events (for audit)
public class InventoryReleasedEvent { /* ReservationId, BookingId, Reason */ }
public class BookingCancelledEvent { /* BookingId, Reason, CancelledAt */ }
```

#### 2. Event Consumers (Background Services)

Each consumer is a **BackgroundService** that:
- Connects to RabbitMQ queue
- Deserializes event JSON
- Processes business logic
- Publishes next event
- Handles retries and failures

**Example: PaymentFailedConsumer in InventoryService**

```csharp
public class PaymentFailedConsumer : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Connect to RabbitMQ
        await InitializeRabbitMQ();
        
        // Listen to payment_failed queue
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            await HandleMessageAsync(ea); // Process event
        };
    }
    
    private async Task ProcessPaymentFailedAsync(PaymentFailedEvent paymentEvent)
    {
        // COMPENSATING ACTION: Release inventory
        
        using var scope = _serviceProvider.CreateScope();
        var inventoryService = scope.ServiceProvider
            .GetRequiredService<IInventoryManagementService>();
        
        // Find reservation by BookingId
        await inventoryService.ReleaseAsync(paymentEvent.Data.BookingId);
        
        // Publish InventoryReleasedEvent for audit
        var releasedEvent = new InventoryReleasedEvent { /* ... */ };
        await _eventBus.PublishAsync(releasedEvent, "inventory_released");
        
        _logger.LogInformation(
            "Inventory released for BookingId: {BookingId}", 
            paymentEvent.Data.BookingId
        );
    }
}
```

#### 3. Idempotency Checks

**Critical**: Event handlers must be **idempotent** (safe to run multiple times).

```csharp
// Example from BookingService.PaymentSucceededConsumer
var booking = await dbContext.Bookings.FindAsync(bookingId);

if (booking.Status == "CONFIRMED")
{
    _logger.LogInformation("Booking already confirmed. Skipping.");
    return; // ✅ Idempotent - safe to process duplicate events
}

booking.Status = "CONFIRMED";
await dbContext.SaveChangesAsync();
```

**Why needed?**
- Network failures may cause duplicate message delivery
- Consumer crash may re-process message after restart
- RabbitMQ requeue on NACK

#### 4. Correlation ID Tracking

All events share the same **CorrelationId** throughout the saga:

```csharp
public class BookingCreatedEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public Guid CorrelationId { get; set; } // ← Same across all events
    public string EventName { get; set; } = "BookingCreated";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public BookingCreatedData Data { get; set; }
}
```

**Benefits**:
- Track entire saga flow in Seq logs
- Debug distributed transactions
- Monitor saga completion time
- Identify bottlenecks

**Query in Seq**:
```
CorrelationId = 'd6fd46e5-9831-4d11-97c3-af8fcb942f9c'
```

---

## 🎓 Key Concepts Explained

### 1. Eventual Consistency

**Traditional (ACID)**:
```
BEGIN TRANSACTION
  Update Booking SET Status = 'CONFIRMED'
  Update Inventory SET AvailableQuantity = AvailableQuantity - 1
  Insert INTO Payments (Status) VALUES ('SUCCESS')
COMMIT -- All updated instantly ✅
```

**Saga (Eventual Consistency)**:
```
Time T0: Booking created (PENDING)
Time T1: Inventory reserved (AvailableQuantity decremented)
Time T2: Payment processed (SUCCESS)
Time T3: Booking confirmed (CONFIRMED)
Time T4: Inventory confirmed (CONFIRMED)

→ System consistent at T4, but inconsistent from T0-T3 ⏳
```

**Implications**:
- Users might see "PENDING" bookings briefly
- Inventory shows as reserved before payment completes
- Need to handle "in-flight" transactions gracefully

### 2. Compensating Transactions

A compensating transaction **undoes** the effect of a previous step:

| Forward Action | Compensating Action |
|----------------|---------------------|
| Create booking (PENDING) | Cancel booking (CANCELLED) |
| Reserve inventory (AvailableQuantity--) | Release inventory (AvailableQuantity++) |
| Process payment (SUCCESS) | Refund payment (REFUNDED) |

**Important**: Compensations are **business logic**, not database rollbacks.

```csharp
// ✅ CORRECT: Business compensation
public async Task ReleaseAsync(Guid bookingId)
{
    var reservation = await GetByBookingIdAsync(bookingId);
    
    // Update reservation status
    reservation.Status = "RELEASED";
    reservation.ReleasedAt = DateTime.UtcNow;
    
    // Restore inventory quantities
    var item = await _dbContext.InventoryItems
        .FindAsync(reservation.ItemId);
    item.AvailableQuantity++;
    item.ReservedQuantity--;
    
    await _dbContext.SaveChangesAsync();
}

// ❌ WRONG: Database rollback (not possible across services)
dbContext.Database.RollbackTransaction();
```

### 3. Graceful Degradation

**Business failures** (insufficient inventory, invalid card) vs **Technical failures** (database down, network timeout):

```csharp
try
{
    await inventoryService.ReserveAsync(bookingId, roomId, quantity);
}
catch (InvalidOperationException ex) 
    when (ex.Message.Contains("Insufficient inventory"))
{
    // ✅ Business failure - graceful handling
    _logger.LogWarning("Insufficient inventory: {Message}", ex.Message);
    
    var failedEvent = new InventoryReservationFailedEvent { /* ... */ };
    await _eventBus.PublishAsync(failedEvent, "inventory_reservation_failed");
    
    // Don't throw - system continues normally
}
catch (Exception ex)
{
    // ❌ Technical failure - retry and escalate
    _logger.LogError(ex, "Database error reserving inventory");
    throw; // Trigger retry policy
}
```

**Key Difference**:
- **Business failures**: Expected, publish failure events, no retries
- **Technical failures**: Unexpected, retry with Polly, move to DLQ if all retries fail

### 4. Dead Letter Queue (DLQ)

When a message fails after all retry attempts:

```csharp
private async Task HandleMessageAsync(BasicDeliverEventArgs ea)
{
    try
    {
        await _resiliencePipeline.ExecuteAsync(async ct =>
        {
            await ProcessPaymentFailedAsync(paymentEvent);
        }, CancellationToken.None);
        
        await _channel.BasicAckAsync(ea.DeliveryTag, false); // ✅ Success
    }
    catch (Exception ex)
    {
        _retryCount++;
        
        if (_retryCount >= _maxRequeueAttempts)
        {
            // ❌ Max retries reached - send to DLQ
            await SendToDeadLetterQueueAsync(message, ex.Message);
            await _channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false);
        }
        else
        {
            // ⚠️ Requeue for retry
            await _channel.BasicNackAsync(ea.DeliveryTag, false, requeue: true);
        }
    }
}
```

**DLQ Messages** require manual investigation:
- Check logs for root cause
- Fix data inconsistencies
- Replay message if appropriate

---

## 📊 Saga State Diagram

```
┌─────────────┐
│   PENDING   │ ← Booking created
└──────┬──────┘
       │
       ├──→ Inventory Reserved? ──No──→ ┌───────────┐
       │                                │ CANCELLED │ (Insufficient inventory)
       │                                └───────────┘
       Yes
       ↓
┌─────────────┐
│  RESERVED   │ ← Inventory locked for 15 minutes
└──────┬──────┘
       │
       ├──→ Payment Succeeded? ──No──→ ┌───────────┐
       │                                │ CANCELLED │ (Payment failed)
       │                                │ + Release │ (Compensating action)
       │                                │ Inventory │
       │                                └───────────┘
       Yes
       ↓
┌─────────────┐
│  CONFIRMED  │ ← Final success state ✅
└─────────────┘
```

### State Transitions Matrix

| Current State | Event | Next State | Compensating Action |
|---------------|-------|------------|---------------------|
| PENDING | InventoryReserved | RESERVED | None |
| PENDING | InventoryReservationFailed | CANCELLED | None (never reserved) |
| RESERVED | PaymentSucceeded | CONFIRMED | None |
| RESERVED | PaymentFailed | CANCELLED | Release inventory |
| CONFIRMED | - | (Final) | - |
| CANCELLED | - | (Final) | - |

---

## 🚀 Running and Testing the Saga

### Test Scenario 1: Successful Flow

```powershell
# Reset inventory
docker exec -it inventorydb psql -U inventoryservice -d inventorydb -c "
UPDATE \"InventoryReservations\" SET \"Status\" = 'RELEASED'; 
UPDATE \"InventoryItems\" SET \"AvailableQuantity\" = \"TotalQuantity\", \"ReservedQuantity\" = 0;"

# Run test (4 bookings, 1 concurrent)
.\scripts\testing\test-e2e-auth.ps1 -NumberOfFlows 4 -ConcurrentFlows 1

# Track in Seq
# http://localhost:5341
# Query: CorrelationId = '<from-test-output>'
```

**Expected Logs in Seq**:
```
[BookingService] Booking created: xxx
[BookingService] Event published: BookingCreatedEvent
[InventoryService] Received BookingCreated event
[InventoryService] Inventory reserved for ROOM-101
[InventoryService] Event published: InventoryReservedEvent
[PaymentService] Received InventoryReserved event
[PaymentService] Payment succeeded: xxx
[PaymentService] Event published: PaymentSucceededEvent
[BookingService] Received PaymentSucceeded event
[BookingService] Booking confirmed: xxx
[InventoryService] Received PaymentSucceeded event
[InventoryService] Reservation confirmed
```

### Test Scenario 2: Payment Failure (Compensating Actions)

```powershell
# Same setup as Scenario 1

# Run test with retries enabled
.\scripts\testing\test-e2e-auth.ps1 -NumberOfFlows 10 -ConcurrentFlows 2
```

**Expected Logs for Failed Payment**:
```
[PaymentService] Payment FAILED: xxx
[PaymentService] Event published: PaymentFailedEvent

[InventoryService] Received PaymentFailed event
[InventoryService] Releasing inventory for BookingId: xxx
[InventoryService] Inventory released, quantities restored
[InventoryService] Event published: InventoryReleasedEvent

[BookingService] Received PaymentFailed event
[BookingService] Cancelling booking: xxx
[BookingService] Booking cancelled with reason: Payment failed
```

### Test Scenario 3: Inventory Exhaustion

```powershell
# Reset inventory (same as Scenario 1)

# Run test to exhaust 4 rooms
.\scripts\testing\test-e2e-auth.ps1 -NumberOfFlows 8 -ConcurrentFlows 4
```

**Expected Logs for Insufficient Inventory**:
```
[InventoryService] Received BookingCreated event
[InventoryService] WARNING: Insufficient inventory for ROOM-101
[InventoryService] Event published: InventoryReservationFailedEvent

[BookingService] Received InventoryReservationFailed event
[BookingService] Cancelling booking: xxx
[BookingService] Booking cancelled with reason: Inventory reservation failed
[BookingService] Event published: BookingCancelledEvent
```

---

## 🎯 Best Practices

### 1. Design Compensating Actions Carefully ⚠️

```csharp
// ✅ GOOD: Compensations are symmetric
Reserve inventory → Release inventory
Charge card → Refund card
Send email → Send cancellation email

// ❌ BAD: Can't undo
Send email → ??? (can't unsend)
External API call → ??? (might not have undo endpoint)
```

**Solution for irreversible actions**:
- Execute them **last** in the saga
- Use two-phase approach (reserve → confirm)
- Implement apology workflows (send cancellation notice)

### 2. Keep Saga Steps Small and Focused 📦

```csharp
// ✅ GOOD: Single responsibility
public async Task ReserveInventoryAsync(Guid bookingId, string roomId)
{
    // Only reserves inventory, nothing else
}

// ❌ BAD: Multiple responsibilities
public async Task ReserveInventoryAndSendEmailAndLogAuditAsync(...)
{
    // Too much in one transaction - hard to compensate
}
```

### 3. Use Correlation IDs for Tracing 🔍

```csharp
// Always flow CorrelationId through events
var nextEvent = new InventoryReservedEvent
{
    CorrelationId = previousEvent.CorrelationId, // ← Same ID
    Data = new InventoryReservedData { /* ... */ }
};
```

### 4. Implement Idempotency Everywhere 🔁

```csharp
// Use unique business keys to detect duplicates
public async Task ProcessPaymentAsync(PaymentEvent @event)
{
    var existing = await _dbContext.Payments
        .FirstOrDefaultAsync(p => p.BookingId == @event.Data.BookingId);
    
    if (existing != null)
    {
        _logger.LogWarning("Payment already processed for {BookingId}", 
            @event.Data.BookingId);
        return; // ✅ Skip duplicate
    }
    
    // Process payment...
}
```

### 5. Set Timeouts on Reservations ⏰

```csharp
public class InventoryReservation
{
    public DateTime ExpiresAt { get; set; } // 15 minutes from creation
    
    // Background job releases expired reservations
}
```

**Prevents**:
- Stuck reservations (if payment service is down)
- Inventory permanently locked
- Deadlocks in high-concurrency scenarios

### 6. Monitor Saga Completion Rates 📈

Track metrics:
- **Success rate**: % of sagas that complete successfully
- **Compensation rate**: % of sagas requiring rollback
- **Average duration**: Time from start to finish
- **Failure reasons**: Top causes of compensation

**Alert on**:
- Success rate < 90%
- Average duration > 10 seconds
- High compensation rate (indicates systemic issues)

---

## 🐛 Common Pitfalls and Solutions

### Pitfall 1: Circular Dependencies ⚠️

**Problem**:
```
ServiceA publishes EventX → ServiceB consumes
ServiceB publishes EventY → ServiceA consumes (circular!)
```

**Solution**:
- Keep event flow unidirectional
- Use separate events for different purposes
- Consider orchestration if choreography becomes too complex

### Pitfall 2: Lost Messages 📨

**Problem**: Event published but consumer never receives it.

**Solutions**:
- ✅ Use **Outbox Pattern** (implemented in this project)
- ✅ RabbitMQ persistent queues (durable: true)
- ✅ Publisher confirms in RabbitMQ
- ✅ Dead letter queues for failed messages

### Pitfall 3: Inconsistent State After Compensation ⚠️

**Problem**: Compensation fails, system stuck in inconsistent state.

**Solutions**:
- ✅ Retry compensating actions (critical)
- ✅ Log failures to DLQ for manual intervention
- ✅ Implement saga recovery workflows
- ✅ Use background jobs to fix inconsistencies

### Pitfall 4: Timeout Handling ⏱️

**Problem**: Service takes too long to respond, saga hangs.

**Solutions**:
- ✅ Set timeouts on HTTP calls (5-30 seconds)
- ✅ Set message expiration in RabbitMQ (TTL)
- ✅ Implement saga timeout detection
- ✅ Auto-trigger compensation after timeout

### Pitfall 5: Testing Complexity 🧪

**Problem**: Hard to test all compensation paths.

**Solutions**:
- ✅ Test each consumer independently (unit tests)
- ✅ Integration tests with real RabbitMQ
- ✅ Simulate failures (chaos engineering)
- ✅ Use test scripts (like test-e2e-auth.ps1)

---

## 📚 Further Learning

### Books
- **"Microservices Patterns" by Chris Richardson** - Chapter on Saga pattern
- **"Building Microservices" by Sam Newman** - Chapter on distributed transactions

### Online Resources
- [Microsoft: Saga Pattern](https://docs.microsoft.com/en-us/azure/architecture/reference-architectures/saga/saga)
- [Martin Fowler: Saga Pattern](https://martinfowler.com/articles/patterns-of-distributed-systems/saga.html)
- [Chris Richardson: Microservices.io](https://microservices.io/patterns/data/saga.html)

### Related Patterns in This Project
- **Outbox Pattern**: `/docs/phase6-advanced/OUTBOX_PATTERN_IMPLEMENTATION.md`
- **Event Choreography**: `/brief/02-communication/event-choreography.md`
- **Correlation Tracking**: `/docs/CORRELATION_ID_GUIDE.md`

---

## 🎓 Interview Questions

### Q1: What is the Saga pattern and why is it needed?

**Answer**: The Saga pattern manages distributed transactions in microservices by breaking them into a sequence of local transactions. Each service performs its transaction and publishes events to trigger the next step. If a step fails, compensating transactions undo previous steps. It's needed because:
- Microservices have separate databases (no shared transactions)
- 2PC (two-phase commit) is too slow and doesn't scale
- We need eventual consistency without blocking services

### Q2: Choreography vs Orchestration - which is better?

**Answer**: **Neither is universally better**:

**Choreography** (used in this project):
- Good for: Simple workflows (3-5 steps), loosely coupled services
- Bad for: Complex workflows, many services involved
- Example: Booking → Inventory → Payment

**Orchestration**:
- Good for: Complex workflows, need centralized visibility
- Bad for: Adding orchestrator complexity, single point of failure
- Example: Multi-step approval workflows

**Recommendation**: Start with choreography, move to orchestration if workflow becomes too complex.

### Q3: How do you ensure idempotency in saga steps?

**Answer**: Multiple strategies:
1. **Check existing records**: Query by business key before creating
2. **Use unique constraints**: Database prevents duplicates
3. **Store message IDs**: Track processed event IDs
4. **Status checks**: `if (booking.Status == "CONFIRMED") return;`
5. **Optimistic concurrency**: Use version/timestamp fields

**Example from this project**:
```csharp
if (booking.Status == "CONFIRMED") {
    _logger.LogInfo("Already confirmed");
    return; // Safe to process duplicate
}
```

### Q4: What happens if a compensating action fails?

**Answer**: Critical scenario! Solutions:
1. **Retry with exponential backoff** (Polly library)
2. **Send to Dead Letter Queue** after max retries
3. **Manual intervention** via admin dashboard
4. **Background reconciliation job** to fix inconsistencies
5. **Alert operations team** for urgent issues

**In this project**: Compensating actions use Polly retry (3 attempts), then DLQ, then manual investigation.

### Q5: How do you test Saga patterns?

**Answer**: Multiple levels:
1. **Unit tests**: Test each event handler independently
2. **Integration tests**: Test with real RabbitMQ (docker-compose)
3. **End-to-end tests**: Full flow with all services (test-e2e-auth.ps1)
4. **Chaos testing**: Randomly kill services to test compensation
5. **Monitoring**: Track success rates, duration, failure reasons in production

**This project provides**: E2E test script with configurable concurrency and failure simulation.

---

## ✅ Summary

### Key Takeaways

1. **Saga Pattern** = Distributed transaction management without ACID
2. **Choreography** = Each service reacts to events (no orchestrator)
3. **Compensating Actions** = Undo previous steps when failure occurs
4. **Eventual Consistency** = System is consistent eventually, not immediately
5. **Idempotency** = Safe to process same event multiple times

### This Project Implements

✅ Choreography-based saga  
✅ Compensating actions (release inventory, cancel booking)  
✅ Graceful error handling (business vs technical failures)  
✅ Correlation ID tracking through entire saga  
✅ Dead letter queues for failed messages  
✅ Idempotent event handlers  
✅ Retry policies with Polly  
✅ End-to-end testing scripts  

### Next Steps

1. ✅ Review the actual code in:
   - `src/BookingService/Consumers/PaymentFailedConsumer.cs`
   - `src/InventoryService/Consumers/PaymentFailedConsumer.cs`
   - `src/BookingService/Consumers/InventoryReservationFailedConsumer.cs`

2. ✅ Run the tests:
   ```powershell
   .\scripts\testing\test-e2e-auth.ps1 -NumberOfFlows 10 -ConcurrentFlows 2
   ```

3. ✅ Track flows in Seq:
   ```
   http://localhost:5341
   Query: CorrelationId = 'your-guid-here'
   ```

4. 📚 Read related documentation:
   - `docs/COMPLETE_SYSTEM_FLOW.md`
   - `docs/CORRELATION_ID_GUIDE.md`
   - `docs/phase6-advanced/OUTBOX_PATTERN_IMPLEMENTATION.md`

---

**Status**: ✅ Complete Implementation  
**Last Updated**: November 12, 2025  
**Complexity Level**: Intermediate to Advanced  
**Interview Ready**: ✅ Yes
