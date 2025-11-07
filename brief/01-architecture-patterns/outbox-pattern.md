# 📦 Outbox Pattern - Guaranteed Event Delivery

**Category**: Architecture Patterns  
**Difficulty**: Advanced  
**Implementation Status**: ✅ Complete (November 7, 2025)

---

## 📖 What is the Outbox Pattern?

The **Transactional Outbox Pattern** solves the **dual-write problem** in distributed systems by ensuring that business data and events are saved **atomically** in a single transaction.

### The Dual-Write Problem

When you need to:
1. ✅ Save data to database (e.g., create a booking)
2. ✅ Publish an event to message broker (e.g., BookingCreated event)

**Without Outbox Pattern** - Two separate operations (❌ Not atomic):

```csharp
// ❌ PROBLEM: Not atomic!
await _dbContext.SaveChangesAsync();     // 1. Save to DB
await _eventBus.PublishAsync(event);     // 2. Publish event

// What if RabbitMQ is down? Event is LOST forever!
```

**With Outbox Pattern** - Single transaction (✅ Atomic):

```csharp
// ✅ SOLUTION: Single transaction!
using var transaction = await _dbContext.Database.BeginTransactionAsync();
await _dbContext.Bookings.AddAsync(booking);        // 1. Save business data
await _dbContext.OutboxMessages.AddAsync(@event);   // 2. Save event
await transaction.CommitAsync();                     // 3. Both or neither!

// Background job publishes events from outbox later
```

---

## 🎯 Why Do We Need It?

### Problem Scenarios

| Scenario | Database | Message Broker | Result |
|----------|----------|---------------|--------|
| 1 | ✅ Success | ✅ Success | ✅ Perfect |
| 2 | ✅ Success | ❌ Down | ❌ Event lost forever |
| 3 | ❌ Failed | ✅ Success | ❌ Event sent for non-existent data |
| 4 | ✅ Success | ⏱️ Timeout | ❓ Uncertain state |

**Without outbox, Scenario 2 means lost events and data inconsistency!**

### Benefits

✅ **Guaranteed Delivery** - Events never lost, stored in database  
✅ **Atomicity** - Business data + events saved together  
✅ **Resilience** - Survives message broker downtime  
✅ **Audit Trail** - Complete history in database  
✅ **Eventual Consistency** - Events published when broker available  
✅ **Horizontal Scaling** - Multiple service instances supported

---

## 🏗️ How It Works

### Architecture Flow

```
1. API Request
   │
   ▼
2. Begin Database Transaction
   ├─→ Save Business Data (Booking)
   └─→ Save Event to Outbox Table
   │
   ▼
3. Commit Transaction (Atomic!)
   │
   ▼
4. Background Worker (Polls every 10s)
   ├─→ SELECT * FROM outbox WHERE published = false
   ├─→ Publish to RabbitMQ
   ├─→ UPDATE published = true
   └─→ Retry on failure
```

### Database Schema

**Outbox Table** (PostgreSQL in BookingService):

```sql
CREATE TABLE outbox_messages (
    id UUID PRIMARY KEY,
    event_type VARCHAR(100) NOT NULL,
    payload JSONB NOT NULL,
    created_at TIMESTAMP NOT NULL,
    published BOOLEAN NOT NULL DEFAULT FALSE,
    published_at TIMESTAMP NULL,
    retry_count INTEGER NOT NULL DEFAULT 0,
    last_error TEXT NULL,
    last_attempt_at TIMESTAMP NULL
);

-- Performance index for unpublished messages
CREATE INDEX idx_outbox_published_created 
ON outbox_messages(published, created_at) 
WHERE published = false;
```

**Outbox Collection** (MongoDB in PaymentService):

```javascript
{
  _id: ObjectId("..."),
  eventType: "PaymentSucceeded",
  payload: {
    paymentId: "guid",
    bookingId: "guid",
    amount: 500000
  },
  createdAt: ISODate("2025-11-07T10:00:00Z"),
  published: false,
  publishedAt: null,
  retryCount: 0,
  lastError: null,
  lastAttemptAt: null
}

// Index for query performance
db.outbox_messages.createIndex({ published: 1, createdAt: 1 })
```

---

## 📝 Implementation in This Project

### Service Comparison

| Aspect | BookingService | PaymentService |
|--------|----------------|----------------|
| **Database** | PostgreSQL | MongoDB |
| **Atomicity** | ✅ Full ACID transactions | ⚠️ Best-effort (no replica set) |
| **Outbox Storage** | `outbox_messages` table | `outbox_messages` collection |
| **Transaction Support** | EF Core transactions | Optional MongoDB transactions |
| **Background Worker** | `OutboxPublisherService` | `OutboxPublisherService` |
| **Polling Interval** | 10 seconds | 10 seconds |
| **Max Retries** | 5 attempts | 5 attempts |

### BookingService Implementation

#### 1. Outbox Entity Model

**File**: `src/BookingService/Models/OutboxMessage.cs`

```csharp
[Table("outbox_messages")]
public class OutboxMessage
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("event_type")]
    [MaxLength(100)]
    public string EventType { get; set; } = string.Empty;

    [Column("payload", TypeName = "jsonb")]
    public string Payload { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("published")]
    public bool Published { get; set; }

    [Column("published_at")]
    public DateTime? PublishedAt { get; set; }

    [Column("retry_count")]
    public int RetryCount { get; set; }

    [Column("last_error")]
    public string? LastError { get; set; }

    [Column("last_attempt_at")]
    public DateTime? LastAttemptAt { get; set; }
}
```

#### 2. Outbox Service

**File**: `src/BookingService/Services/OutboxService.cs`

```csharp
public interface IOutboxService
{
    Task AddToOutboxAsync<T>(T @event, string eventType);
    Task<List<OutboxMessage>> GetUnpublishedMessagesAsync(int batchSize = 100);
    Task MarkAsPublishedAsync(Guid messageId);
    Task MarkAsFailedAsync(Guid messageId, string error);
}

public class OutboxService : IOutboxService
{
    private readonly BookingDbContext _dbContext;
    private readonly ILogger<OutboxService> _logger;

    public async Task AddToOutboxAsync<T>(T @event, string eventType)
    {
        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            Payload = JsonSerializer.Serialize(@event),
            CreatedAt = DateTime.UtcNow,
            Published = false
        };

        _dbContext.OutboxMessages.Add(outboxMessage);
        // SaveChangesAsync called by caller in same transaction
    }

    public async Task<List<OutboxMessage>> GetUnpublishedMessagesAsync(int batchSize = 100)
    {
        return await _dbContext.OutboxMessages
            .Where(m => !m.Published)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync();
    }

    public async Task MarkAsPublishedAsync(Guid messageId)
    {
        var message = await _dbContext.OutboxMessages.FindAsync(messageId);
        if (message != null)
        {
            message.Published = true;
            message.PublishedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }
    }
}
```

#### 3. Using Outbox in Business Logic

**File**: `src/BookingService/Services/BookingServiceImpl.cs`

```csharp
public async Task<BookingResponse> CreateBookingAsync(CreateBookingRequest request)
{
    // Begin explicit transaction for atomicity
    using var transaction = await _dbContext.Database.BeginTransactionAsync();
    
    try
    {
        // 1. Save business data
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

        // 2. Save event to outbox (same transaction!)
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
                Amount = booking.Amount
            }
        };

        await _outboxService.AddToOutboxAsync(bookingEvent, "BookingCreated");

        // 3. Commit both together - atomic!
        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        _logger.LogInformation(
            "Booking {BookingId} and event saved to outbox atomically",
            booking.Id
        );

        return new BookingResponse { /* ... */ };
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        _logger.LogError(ex, "Failed to create booking");
        throw;
    }
}
```

#### 4. Background Publisher

**File**: `src/BookingService/BackgroundServices/OutboxPublisherService.cs`

```csharp
public class OutboxPublisherService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxPublisherService> _logger;
    private readonly TimeSpan _publishInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox Publisher Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in outbox publisher");
            }

            await Task.Delay(_publishInterval, stoppingToken);
        }
    }

    private async Task PublishPendingMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService>();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        var messages = await outboxService.GetUnpublishedMessagesAsync();

        _logger.LogInformation("Found {Count} unpublished messages", messages.Count);

        foreach (var message in messages)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                // Deserialize event based on type
                object @event = message.EventType switch
                {
                    "BookingCreated" => JsonSerializer.Deserialize<BookingCreatedEvent>(message.Payload),
                    _ => throw new InvalidOperationException($"Unknown event type: {message.EventType}")
                };

                // Publish to RabbitMQ
                await eventBus.PublishAsync(@event, "booking_created");

                // Mark as published
                await outboxService.MarkAsPublishedAsync(message.Id);

                _logger.LogInformation(
                    "Successfully published message {MessageId} of type {EventType}",
                    message.Id,
                    message.EventType
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to publish message {MessageId}, retry count: {RetryCount}",
                    message.Id,
                    message.RetryCount
                );

                await outboxService.MarkAsFailedAsync(message.Id, ex.Message);

                // Alert if max retries reached
                if (message.RetryCount >= 5)
                {
                    _logger.LogCritical(
                        "Message {MessageId} exceeded max retries, manual intervention needed",
                        message.Id
                    );
                }
            }
        }
    }
}
```

---

## ⚙️ Configuration

**File**: `src/BookingService/appsettings.json`

```json
{
  "OutboxPublisher": {
    "Enabled": true,
    "PublishIntervalSeconds": 10,
    "BatchSize": 100,
    "MaxRetries": 5
  }
}
```

**Registration** in `Program.cs`:

```csharp
// Register outbox service
builder.Services.AddScoped<IOutboxService, OutboxService>();

// Register background publisher
builder.Services.AddHostedService<OutboxPublisherService>();
```

---

## 🧪 Testing the Outbox Pattern

### Test Scenario 1: Normal Flow

```bash
# 1. Start services
docker-compose up -d

# 2. Create booking
POST http://localhost:5000/booking/api/bookings
{
  "userId": "user-guid",
  "roomId": "ROOM-101",
  "amount": 500000
}

# 3. Check outbox table immediately
SELECT * FROM outbox_messages WHERE published = false;
# Should see 1 unpublished message

# 4. Wait 10 seconds for background worker

# 5. Check again
SELECT * FROM outbox_messages WHERE published = true;
# Message should be published now
```

### Test Scenario 2: RabbitMQ Down

```bash
# 1. Stop RabbitMQ
docker stop rabbitmq

# 2. Create booking (should succeed!)
POST http://localhost:5000/booking/api/bookings

# 3. Check outbox - event stored but not published
SELECT * FROM outbox_messages WHERE published = false;

# 4. Start RabbitMQ
docker start rabbitmq

# 5. Wait 10-20 seconds, check again
SELECT * FROM outbox_messages WHERE published = true;
# Event published automatically!
```

### Test Scenario 3: Check Retry Logic

```sql
-- Messages with retries
SELECT id, event_type, retry_count, last_error, last_attempt_at
FROM outbox_messages
WHERE retry_count > 0;

-- Failed messages needing manual intervention
SELECT * FROM outbox_messages
WHERE retry_count >= 5 AND published = false;
```

---

## 📊 Monitoring Queries

### Dashboard Statistics

```sql
SELECT 
    'Total Messages' as metric, COUNT(*) as count 
FROM outbox_messages
UNION ALL
SELECT 
    'Published', COUNT(*) 
FROM outbox_messages 
WHERE published = true
UNION ALL
SELECT 
    'Pending', COUNT(*) 
FROM outbox_messages 
WHERE published = false
UNION ALL
SELECT 
    'Failed (>= 5 retries)', COUNT(*) 
FROM outbox_messages 
WHERE retry_count >= 5;
```

### Alert Queries

```sql
-- Critical: Messages stuck after max retries
SELECT COUNT(*) as critical_count
FROM outbox_messages
WHERE published = false AND retry_count >= 5;

-- Warning: High pending count
SELECT COUNT(*) as pending_count
FROM outbox_messages
WHERE published = false;

-- Performance: Average publish latency
SELECT AVG(EXTRACT(EPOCH FROM (published_at - created_at))) as avg_latency_seconds
FROM outbox_messages
WHERE published = true;
```

---

## 🎯 Best Practices

### 1. Idempotent Event Handlers

Events may be delivered multiple times (at-least-once delivery):

```csharp
public async Task HandlePaymentSucceeded(PaymentSucceededEvent @event)
{
    // Check if already processed
    var booking = await _dbContext.Bookings.FindAsync(@event.BookingId);
    
    if (booking.Status == "CONFIRMED")
    {
        _logger.LogInformation("Already processed, skipping");
        return; // Idempotent!
    }
    
    // Process event...
}
```

### 2. Cleanup Old Messages

Messages accumulate over time:

```csharp
// Cleanup job (run daily)
public async Task CleanupOldMessagesAsync()
{
    var cutoffDate = DateTime.UtcNow.AddDays(-30);
    
    var oldMessages = await _dbContext.OutboxMessages
        .Where(m => m.Published && m.PublishedAt < cutoffDate)
        .ToListAsync();
    
    _dbContext.OutboxMessages.RemoveRange(oldMessages);
    await _dbContext.SaveChangesAsync();
    
    _logger.LogInformation("Deleted {Count} old messages", oldMessages.Count);
}
```

### 3. Monitoring and Alerts

Set up alerts for:
- ⚠️ Pending messages > 100
- 🔴 Failed messages (retry_count >= 5)
- 📊 Publish latency > 1 minute

---

## 🎓 Key Takeaways

1. **Outbox Pattern = Guaranteed Event Delivery**
   - Events saved to database first
   - Published by background worker

2. **Solves Dual-Write Problem**
   - Business data + events in single transaction
   - Atomicity guaranteed

3. **Trade-offs**
   - ✅ Reliability, audit trail
   - ❌ ~10 second delay, storage overhead

4. **Idempotency Required**
   - Consumers must handle duplicates
   - Essential for at-least-once delivery

5. **Monitoring is Critical**
   - Track pending count
   - Alert on failures
   - Cleanup old messages

---

## 📚 Further Reading

- **Full Implementation Guide**: `/docs/phase6-advanced/OUTBOX_PATTERN_IMPLEMENTATION.md`
- **Quick Reference (BookingService)**: `/src/BookingService/OUTBOX_PATTERN_QUICK_REFERENCE.md`
- **Quick Reference (PaymentService)**: `/src/PaymentService/OUTBOX_PATTERN_QUICK_REFERENCE.md`

### External Resources
- [Microservices.io - Transactional Outbox](https://microservices.io/patterns/data/transactional-outbox.html)
- [Martin Fowler - Event Sourcing](https://martinfowler.com/eaaDev/EventSourcing.html)

---

**Last Updated**: November 7, 2025  
**Implementation Status**: ✅ Complete in both BookingService and PaymentService  
**Code Location**: `/src/BookingService/`, `/src/PaymentService/`
