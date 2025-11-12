# DLQ Integration with Outbox Pattern - Complete Flow

## Problem Identified ✅

The original DLQ implementation only handled failures **during message consumption** from RabbitMQ queues, but **didn't handle failures in the Outbox Pattern itself**. 

When the OutboxPublisher couldn't publish a message after max retries, it would:
- Just log a warning
- Skip the message
- Leave it in the outbox table/collection forever with `published = false`
- Never send it to a Dead Letter Queue

## Solution Implemented ✅

Updated **both BookingService and PaymentService** OutboxPublisher services to:
1. Detect when an outbox message exceeds max retries
2. Store it in the Dead Letter Messages table/collection
3. Mark it as published in the outbox (to remove it from the polling loop)
4. Log the operation for monitoring

## Complete Message Flow

### Scenario 1: Normal Flow (Everything Works)
```
1. Service creates event (e.g., PaymentFailed)
2. Event saved to Outbox table/collection
3. OutboxPublisher polls for unpublished messages
4. OutboxPublisher publishes to RabbitMQ queue
5. OutboxPublisher marks message as published
6. Consumer receives message from RabbitMQ
7. Consumer processes successfully
8. Consumer ACKs message
```

### Scenario 2: Outbox Publishing Failure (NEW - DLQ Integration)
```
1. Service creates event
2. Event saved to Outbox
3. OutboxPublisher polls for unpublished messages
4. OutboxPublisher tries to publish to RabbitMQ → FAILS (e.g., RabbitMQ down)
5. OutboxPublisher marks as failed, increments retry count
6. [Repeat steps 3-5 until retry count reaches max (3 for Payment, 5 for Booking)]
7. OutboxPublisher detects max retries exceeded
8. OutboxPublisher stores message in DeadLetterMessages table/collection ← NEW
9. OutboxPublisher marks outbox message as published (removes from polling) ← NEW
10. Message now in DLQ for manual investigation ← NEW
```

### Scenario 3: Consumer Processing Failure (Original DLQ Implementation)
```
1. Service creates event
2. Event saved to Outbox
3. OutboxPublisher publishes to RabbitMQ successfully
4. Consumer receives message from RabbitMQ
5. Consumer processing fails with exception
6. Consumer NACKs with requeue=true
7. [Repeat steps 5-6 up to 3 times]
8. After 3 failed attempts:
   - Consumer creates DLQ message with metadata
   - Consumer publishes to DLQ queue (e.g., payment_failed_dlq)
   - Consumer ACKs original message (removes from main queue)
9. DeadLetterQueueHandler consumes from DLQ queue
10. DeadLetterQueueHandler stores in DeadLetterMessages table
11. DeadLetterQueueHandler ACKs DLQ message
```

## Updated Components

### BookingService (PostgreSQL)

**Files Modified:**
- ✅ `BackgroundServices/OutboxPublisherService.cs` - Added DLQ routing for failed outbox messages
- ✅ `Models/DeadLetterMessage.cs` - Already existed
- ✅ Database table: `dead_letter_messages` - Already exists via migration

**New Method:**
```csharp
private async Task SendToDeadLetterQueueAsync(
    Models.OutboxMessage message,
    IServiceScope scope,
    CancellationToken cancellationToken)
{
    var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
    
    var deadLetterMessage = new Models.DeadLetterMessage
    {
        SourceQueue = "outbox_" + message.EventType.ToLower(),
        EventType = message.EventType,
        Payload = message.Payload,
        ErrorMessage = message.LastError ?? "Failed after max retry attempts",
        AttemptCount = message.RetryCount,
        FirstAttemptAt = message.CreatedAt,
        FailedAt = DateTime.UtcNow,
        Resolved = false
    };
    
    dbContext.DeadLetterMessages.Add(deadLetterMessage);
    await dbContext.SaveChangesAsync(cancellationToken);
}
```

### PaymentService (MongoDB)

**Files Created:**
- ✅ `Models/DeadLetterMessage.cs` - New model for MongoDB

**Files Modified:**
- ✅ `BackgroundServices/OutboxPublisherService.cs` - Added DLQ routing
- ✅ `Data/MongoDbContext.cs` - Added DeadLetterMessages collection with indexes

**New Method:**
```csharp
private async Task SendToDeadLetterQueueAsync(
    Models.OutboxMessage message,
    IServiceScope scope,
    CancellationToken cancellationToken)
{
    var mongoDbContext = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
    
    var deadLetterMessage = new Models.DeadLetterMessage
    {
        SourceQueue = "outbox_" + message.EventType.ToLower(),
        EventType = message.EventType,
        Payload = message.Payload,
        ErrorMessage = message.LastError ?? "Failed after max retry attempts",
        AttemptCount = message.RetryCount,
        FirstAttemptAt = message.CreatedAt,
        FailedAt = DateTime.UtcNow,
        Resolved = false
    };
    
    await mongoDbContext.DeadLetterMessages.InsertOneAsync(deadLetterMessage, cancellationToken);
}
```

## MongoDB Indexes Added

For efficient querying of dead letter messages in PaymentService:

1. **Resolved + FailedAt Index:** `idx_resolved_failed`
   - Fast queries for unresolved messages
   - Sorted by failure time

2. **EventType Index:**
   - Filter messages by event type
   - Analyze patterns by event

3. **SourceQueue Index:**
   - Track which queues are problematic
   - Group by source for investigation

## Testing the DLQ Integration

### Test Outbox Publisher DLQ

To test the outbox-to-DLQ flow, you can:

1. **Simulate RabbitMQ being down** (stop RabbitMQ container temporarily)
2. **Create a booking** (this saves event to outbox)
3. **Wait for OutboxPublisher to retry** (it will fail to publish)
4. **Check logs in Seq:** Look for retry attempts and eventual DLQ routing
5. **Query the DLQ:**

**BookingService (PostgreSQL):**
```sql
SELECT * FROM dead_letter_messages 
WHERE source_queue LIKE 'outbox_%' 
ORDER BY failed_at DESC;
```

**PaymentService (MongoDB):**
```javascript
db.dead_letter_messages.find({
  source_queue: { $regex: /^outbox_/ }
}).sort({ failed_at: -1 })
```

### Monitor Outbox Failures

**Seq Query for Outbox DLQ Events:**
```
@Message like "%exceeded max retries%" 
and @Message like "%Moving to Dead Letter%"
| select MessageId, MaxRetries, Service
```

**Seq Query for DLQ Storage:**
```
@Message like "%stored in Dead Letter%" 
and source_queue like "outbox_%"
| select MessageId, EventType, DLQId
```

## Benefits

### 1. No Lost Messages
- Messages that fail in the Outbox Publisher are captured
- Complete audit trail maintained
- Nothing disappears silently

### 2. Complete Coverage
- **Outbox failures:** Captured in DLQ via OutboxPublisher
- **Consumer failures:** Captured in DLQ via Consumer retry logic
- **Both paths lead to the same DLQ storage**

### 3. Unified Investigation
- All failed messages in one place
- Same tooling for investigation regardless of failure point
- Consistent resolution workflow

### 4. Prevents Infinite Retries
- Outbox messages don't retry forever
- System performance protected
- Clear failure indication

## Monitoring Queries

### Count DLQ Messages by Source

**PostgreSQL:**
```sql
SELECT 
    source_queue,
    event_type,
    COUNT(*) as total,
    SUM(CASE WHEN resolved THEN 1 ELSE 0 END) as resolved_count,
    SUM(CASE WHEN NOT resolved THEN 1 ELSE 0 END) as unresolved_count
FROM dead_letter_messages
GROUP BY source_queue, event_type
ORDER BY unresolved_count DESC;
```

**MongoDB:**
```javascript
db.dead_letter_messages.aggregate([
  {
    $group: {
      _id: { source_queue: "$source_queue", event_type: "$event_type" },
      total: { $sum: 1 },
      resolved_count: { $sum: { $cond: ["$resolved", 1, 0] } },
      unresolved_count: { $sum: { $cond: ["$resolved", 0, 1] } }
    }
  },
  { $sort: { unresolved_count: -1 } }
])
```

### Outbox Failures Only

**PostgreSQL:**
```sql
SELECT * FROM dead_letter_messages
WHERE source_queue LIKE 'outbox_%'
AND resolved = false
ORDER BY failed_at DESC;
```

**MongoDB:**
```javascript
db.dead_letter_messages.find({
  source_queue: { $regex: /^outbox_/ },
  resolved: false
}).sort({ failed_at: -1 })
```

## Configuration

Both services use the same Outbox configuration:

**BookingService:**
```json
"OutboxPublisher": {
  "PollingIntervalSeconds": 10,
  "BatchSize": 100,
  "MaxRetries": 5
}
```

**PaymentService:**
```json
"OutboxPublisher": {
  "PollingIntervalSeconds": 10,
  "BatchSize": 100,
  "MaxRetries": 3
}
```

After `MaxRetries` is reached, the message goes to DLQ.

## Summary

The DLQ now provides **complete coverage** for both:
1. **Publishing failures** (Outbox → RabbitMQ) - NEW ✅
2. **Consumption failures** (RabbitMQ → Consumer) - Already existed ✅

All failed messages end up in the `dead_letter_messages` table/collection for investigation, regardless of where they failed in the pipeline.

---

**Status:** Complete and Tested ✅
**Build Status:** Both services build successfully ✅
