# 📦 Outbox Pattern - Quick Reference

**Transactional Outbox Pattern in BookingService**

## What is it?

A reliability pattern that ensures events are **never lost** by saving them to the database in the **same transaction** as business data, then publishing them asynchronously via a background worker.

## Why use it?

❌ **Without Outbox**: If RabbitMQ is down, events are lost  
✅ **With Outbox**: Events stored in DB, published when RabbitMQ is available

## How it works

```
1. Create Booking + Save Event to Outbox (same transaction) ✅
2. Commit transaction (both saved atomically) ✅
3. Background worker polls outbox every 10 seconds
4. Publish unpublished events to RabbitMQ
5. Mark as published ✅
```

## Files Created

- `Models/OutboxMessage.cs` - Outbox entity
- `Services/OutboxService.cs` - CRUD operations
- `BackgroundServices/OutboxPublisherService.cs` - Background publisher
- `Data/BookingDbContext.cs` - Updated with OutboxMessages DbSet
- `Migrations/xxx_AddOutboxPattern.cs` - Database migration

## Database Schema

```sql
CREATE TABLE outbox_messages (
    id UUID PRIMARY KEY,
    event_type VARCHAR(100) NOT NULL,
    payload JSONB NOT NULL,
    created_at TIMESTAMP NOT NULL,
    published BOOLEAN DEFAULT FALSE,
    published_at TIMESTAMP,
    retry_count INTEGER DEFAULT 0,
    last_error TEXT,
    last_attempt_at TIMESTAMP
);
```

## Configuration

```json
{
  "OutboxPublisher": {
    "PollingIntervalSeconds": 10,  // How often to check
    "BatchSize": 100,              // Messages per cycle
    "MaxRetries": 5                // Retry attempts
  }
}
```

## Usage Example

```csharp
// Old way (direct publish - events can be lost)
_dbContext.Bookings.Add(booking);
await _dbContext.SaveChangesAsync();
await _eventBus.PublishAsync(event, queue); // ❌ Might fail!

// New way (Outbox pattern - events never lost)
using var transaction = await _dbContext.Database.BeginTransactionAsync();
try {
    _dbContext.Bookings.Add(booking);
    await _outboxService.AddToOutboxAsync(event, "BookingCreated");
    await _dbContext.SaveChangesAsync();
    await transaction.CommitAsync(); // ✅ Both saved together!
} catch {
    await transaction.RollbackAsync();
    throw;
}
```

## Testing

```bash
# 1. Apply migration
dotnet ef database update

# 2. Create a booking
curl -X POST http://localhost:5002/api/bookings ...

# 3. Check outbox (should see unpublished)
SELECT * FROM outbox_messages WHERE published = false;

# 4. Wait 10 seconds (background worker runs)

# 5. Check again (should be published)
SELECT * FROM outbox_messages WHERE published = true;
```

## Monitoring Queries

```sql
-- Pending messages
SELECT COUNT(*) FROM outbox_messages WHERE published = false;

-- Failed messages (need attention)
SELECT * FROM outbox_messages 
WHERE published = false AND retry_count >= 5;

-- Recent activity
SELECT event_type, COUNT(*), MAX(created_at) 
FROM outbox_messages 
GROUP BY event_type;
```

## Troubleshooting

### Messages not publishing?
- Check background service logs
- Verify RabbitMQ connection
- Check retry count: `SELECT retry_count FROM outbox_messages WHERE published = false`

### Too many pending messages?
- Reduce `PollingIntervalSeconds` (poll more often)
- Increase `BatchSize` (process more at once)
- Check RabbitMQ capacity

### Message stuck at max retries?
```sql
-- Reset retry count
UPDATE outbox_messages 
SET retry_count = 0, last_error = NULL 
WHERE id = 'message-id';
```

## Key Benefits

✅ **Guaranteed Delivery** - Events never lost  
✅ **Atomicity** - Business data + events saved together  
✅ **Resilience** - Automatic retries  
✅ **Audit Trail** - Full history in database  
✅ **RabbitMQ Downtime** - Events queued safely  

## Trade-offs

⚠️ **Latency**: ~10 second delay (configurable)  
⚠️ **Complexity**: More code and components  
⚠️ **At-Least-Once**: Events may be published multiple times (consumers must be idempotent)

## Full Documentation

See detailed guide: `/docs/phase6-advanced/OUTBOX_PATTERN_IMPLEMENTATION.md`

---

**Status**: ✅ Implemented in BookingService  
**Date**: November 7, 2025  
**Version**: 1.0
