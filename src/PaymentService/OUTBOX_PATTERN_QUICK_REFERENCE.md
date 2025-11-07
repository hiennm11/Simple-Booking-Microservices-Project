# 📦 Outbox Pattern - PaymentService Quick Reference

**Transactional Outbox Pattern in PaymentService (MongoDB)**

## What is it?

A reliability pattern that ensures events are **never lost** by saving them to MongoDB in **addition to** business data, then publishing them asynchronously via a background worker.

## Why use it?

❌ **Without Outbox**: If RabbitMQ is down, PaymentSucceeded events are lost  
✅ **With Outbox**: Events stored in MongoDB, published when RabbitMQ is available

## How it works

```
1. Process Payment + Save Event to outbox_messages collection ✅
2. Both stored in MongoDB ✅
3. Background worker polls outbox every 10 seconds
4. Publish unpublished events to RabbitMQ
5. Mark as published ✅
```

## Files Created

- `Models/OutboxMessage.cs` - MongoDB document model
- `Services/OutboxService.cs` - CRUD operations for outbox
- `BackgroundServices/OutboxPublisherService.cs` - Background publisher
- `Data/MongoDbContext.cs` - Updated with OutboxMessages collection

## MongoDB Schema

```javascript
// outbox_messages collection
{
  "_id": ObjectId("..."),
  "eventType": "PaymentSucceeded",
  "payload": "{ JSON serialized event }",
  "createdAt": ISODate("2025-11-07T10:00:00Z"),
  "published": false,
  "publishedAt": null,
  "retryCount": 0,
  "lastError": null,
  "lastAttemptAt": null
}

// Indexes
db.outbox_messages.createIndex({ published: 1, createdAt: 1 })
db.outbox_messages.createIndex({ eventType: 1 })
```

## Configuration

```json
{
  "MongoDB": {
    "Collections": {
      "Payments": "payments",
      "OutboxMessages": "outbox_messages"
    }
  },
  "OutboxPublisher": {
    "PollingIntervalSeconds": 10,
    "BatchSize": 100,
    "MaxRetries": 5
  }
}
```

## Usage Example

```csharp
// Old way (direct publish - events can be lost)
await _dbContext.Payments.UpdateOneAsync(...);
await _eventBus.PublishAsync(event, queue); // ❌ Might fail!

// New way (Outbox pattern - events never lost)
// Update payment status
await _dbContext.Payments.UpdateOneAsync(...);

// Save event to outbox
var paymentEvent = new PaymentSucceededEvent { ... };
await _outboxService.AddToOutboxAsync(paymentEvent, "PaymentSucceeded");
// ✅ Event safely stored in MongoDB!

// Background worker will publish it eventually
```

## Testing

```bash
# 1. Create/process a payment
curl -X POST http://localhost:5003/api/payments/pay ...

# 2. Check outbox (should see unpublished)
db.outbox_messages.find({ published: false })

# 3. Wait 10 seconds (background worker runs)

# 4. Check again (should be published)
db.outbox_messages.find({ published: true })
```

## Monitoring Queries

```javascript
// Pending messages
db.outbox_messages.countDocuments({ published: false })

// Failed messages (need attention)
db.outbox_messages.find({ 
  published: false, 
  retryCount: { $gte: 5 } 
})

// Recent activity
db.outbox_messages.aggregate([
  { $group: { 
      _id: "$eventType", 
      count: { $sum: 1 },
      lastCreated: { $max: "$createdAt" }
  }}
])
```

## Troubleshooting

### Messages not publishing?

```javascript
// Check status
db.outbox_messages.find({ published: false }).limit(10)

// Check logs
docker logs payment-service
```

### Too many pending messages?

- Reduce `PollingIntervalSeconds` (poll more often)
- Increase `BatchSize` (process more at once)

### Message stuck at max retries?

```javascript
// Reset retry count
db.outbox_messages.updateOne(
  { _id: ObjectId("...") },
  { $set: { retryCount: 0, lastError: null } }
)

// Or mark as published (skip it)
db.outbox_messages.updateOne(
  { _id: ObjectId("...") },
  { $set: { published: true, publishedAt: new Date() } }
)
```

## Key Differences from BookingService

| Aspect | BookingService | PaymentService |
|--------|----------------|----------------|
| Database | PostgreSQL | MongoDB |
| Transactions | ✅ Native support | ⚠️ Optional (requires replica set) |
| Schema | SQL table | MongoDB collection |
| Indexes | Partial index with filter | Compound indexes |
| Query Style | Entity Framework | MongoDB Driver |

## Key Benefits

✅ **Guaranteed Delivery** - Events never lost  
✅ **No Transactions Needed** - Works without MongoDB transactions  
✅ **Resilience** - Automatic retries  
✅ **Audit Trail** - Full history in MongoDB  
✅ **RabbitMQ Downtime** - Events queued safely  

## Trade-offs

⚠️ **Latency**: ~10 second delay (configurable)  
⚠️ **No Atomicity**: Without MongoDB transactions, payment and outbox are separate operations  
⚠️ **At-Least-Once**: Events may be published multiple times (consumers must be idempotent)

## MongoDB Transactions (Optional)

For true atomicity, use MongoDB transactions (requires replica set):

```csharp
using var session = await _client.StartSessionAsync();
session.StartTransaction();

try {
    // Update payment
    await _dbContext.Payments.UpdateOneAsync(session, ...);
    
    // Save to outbox
    await _dbContext.OutboxMessages.InsertOneAsync(session, ...);
    
    // Commit both
    await session.CommitTransactionAsync();
} catch {
    await session.AbortTransactionAsync();
    throw;
}
```

## Full Documentation

See detailed guide: `/docs/phase6-advanced/OUTBOX_PATTERN_IMPLEMENTATION.md`

---

**Status**: ✅ Implemented in PaymentService  
**Date**: November 7, 2025  
**Version**: 1.0
