# Dead Letter Queue (DLQ)

## 📚 What is a Dead Letter Queue?

A **Dead Letter Queue (DLQ)** is a holding area for messages that cannot be processed successfully after multiple retry attempts. Instead of losing these messages or endlessly retrying, they are moved to a special queue for manual investigation and resolution.

### Why DLQ Matters

```
❌ Without DLQ:
Message processing fails → Retry → Fails again → Retry → Fails again
→ Either: Lost forever OR Blocks queue forever

✅ With DLQ:
Message processing fails → Retry (3 attempts) → Still failing
→ Move to DLQ → Continue processing other messages
→ Manual investigation → Fix root cause → Replay message ✅
```

**Benefits**:
- ✅ No message loss
- ✅ Queue doesn't get blocked
- ✅ Complete audit trail
- ✅ Manual recovery possible
- ✅ Root cause analysis

---

## 🎯 Three DLQ Entry Points in This Project

### 1. Outbox Publisher → DLQ

**Trigger**: OutboxPublisher fails to publish to RabbitMQ after retries

**Scenario**:
```
Event saved in Outbox → OutboxPublisher tries to publish to RabbitMQ
→ RabbitMQ down/error → Retry 3 times → Still failing
→ Store in DeadLetterMessages → Mark outbox as published
→ Manual investigation required
```

**Source Queue**: `outbox_{event_type}`  
**Event Types**: `PaymentSucceeded`, `PaymentFailed`, `BookingCreated`

---

### 2. Message Consumer → DLQ

**Trigger**: Consumer fails to process message after 3 attempts

**Scenario**:
```
RabbitMQ Queue → Consumer receives → Processing throws exception
→ NACK with requeue (attempt 1) → Retry → Still failing (attempt 2)
→ Retry → Still failing (attempt 3)
→ Send to DLQ queue with metadata → ACK original message
→ DeadLetterQueueHandler stores in database
→ Manual investigation required
```

**Source Queue**: `payment_failed`, `payment_succeeded`, `booking_created`  
**DLQ Queues**: `payment_failed_dlq`, `payment_succeeded_dlq`

---

### 3. Payment Retry Exhausted → DLQ

**Trigger**: Payment retry API called when retry count >= max attempts (3)

**Scenario**:
```
Payment failed → User calls retry API → Still fails (retry_count: 1)
→ User calls retry API again → Still fails (retry_count: 2)
→ User calls retry API again → Still fails (retry_count: 3)
→ User calls retry API (4th time):
   → RetryCount (3) >= MaxRetries (3)
   → Store in DeadLetterMessages
   → Update payment status: PERMANENTLY_FAILED
   → Return response (don't throw exception)
→ Manual investigation required
```

**Source Queue**: `payment_retry`  
**Event Type**: `PaymentRetryFailed`

---

## 💾 DLQ Data Model

### DeadLetterMessage Entity

**PostgreSQL** (BookingService):
```csharp
public class DeadLetterMessage
{
    public Guid Id { get; set; }
    public string SourceQueue { get; set; }      // e.g., "payment_failed"
    public string EventType { get; set; }        // e.g., "PaymentFailed"
    public string Payload { get; set; }          // Original message JSON
    public string ErrorMessage { get; set; }     // Why it failed
    public int AttemptCount { get; set; }        // Number of attempts
    public DateTime FirstAttemptAt { get; set; } // First attempt timestamp
    public DateTime FailedAt { get; set; }       // When moved to DLQ
    public bool Resolved { get; set; }           // false by default
    public DateTime? ResolvedAt { get; set; }    // When resolved
    public string? ResolvedBy { get; set; }      // Who resolved it
    public string? ResolutionNotes { get; set; } // How it was resolved
}
```

**MongoDB** (PaymentService):
```csharp
public class DeadLetterMessage
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }
    
    [BsonElement("source_queue")]
    public string SourceQueue { get; set; }
    
    [BsonElement("event_type")]
    public string EventType { get; set; }
    
    [BsonElement("payload")]
    public string Payload { get; set; }
    
    [BsonElement("error_message")]
    public string ErrorMessage { get; set; }
    
    [BsonElement("attempt_count")]
    public int AttemptCount { get; set; }
    
    [BsonElement("first_attempt_at")]
    public DateTime FirstAttemptAt { get; set; }
    
    [BsonElement("failed_at")]
    public DateTime FailedAt { get; set; }
    
    [BsonElement("resolved")]
    public bool Resolved { get; set; }
    
    [BsonElement("resolved_at")]
    public DateTime? ResolvedAt { get; set; }
    
    [BsonElement("resolved_by")]
    public string? ResolvedBy { get; set; }
    
    [BsonElement("resolution_notes")]
    public string? ResolutionNotes { get; set; }
}
```

---

## 🔧 Implementation Details

### Consumer with DLQ Logic

**File**: `BookingService/Consumers/PaymentSucceededConsumer.cs`

```csharp
public class PaymentSucceededConsumer : BackgroundService
{
    private const int MAX_REQUEUE_ATTEMPTS = 3;
    private int _retryCount = 0;

    private async Task HandleMessageAsync(BasicDeliverEventArgs ea)
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);

        try
        {
            var paymentEvent = JsonSerializer.Deserialize<PaymentSucceededEvent>(message);
            
            if (paymentEvent?.Data == null)
            {
                _logger.LogWarning("Invalid event format - not requeuing");
                _channel!.BasicNack(ea.DeliveryTag, false, requeue: false);
                return;
            }

            // ✅ Process with retry policy
            await _resiliencePipeline.ExecuteAsync(async ct =>
            {
                await ProcessPaymentSucceededAsync(paymentEvent);
            });

            // ✅ Success - acknowledge
            _channel!.BasicAck(ea.DeliveryTag, false);
            _retryCount = 0;
            
            _logger.LogInformation(
                "Event processed successfully for BookingId: {BookingId}",
                paymentEvent.Data.BookingId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing event: {Message}", message);
            
            _retryCount++;
            
            if (_retryCount >= MAX_REQUEUE_ATTEMPTS)
            {
                // ❌ Max retries reached - send to DLQ
                _logger.LogError(
                    "Message failed after {Attempts} attempts. Moving to DLQ.",
                    MAX_REQUEUE_ATTEMPTS);
                
                // Create DLQ message
                await CreateDeadLetterQueueMessageAsync(message, ex.Message);
                
                // Publish to DLQ queue
                await PublishToDLQQueueAsync(message, "payment_succeeded_dlq");
                
                // ACK original message (remove from main queue)
                _channel!.BasicNack(ea.DeliveryTag, false, requeue: false);
                _retryCount = 0;
            }
            else
            {
                // ⚠️ Requeue for retry
                _logger.LogWarning(
                    "Requeuing message. Attempt {Attempt}/{Max}",
                    _retryCount, MAX_REQUEUE_ATTEMPTS);
                
                _channel!.BasicNack(ea.DeliveryTag, false, requeue: true);
            }
        }
    }

    private async Task CreateDeadLetterQueueMessageAsync(string payload, string errorMessage)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        var deadLetterMessage = new DeadLetterMessage
        {
            Id = Guid.NewGuid(),
            SourceQueue = "payment_succeeded",
            EventType = "PaymentSucceeded",
            Payload = payload,
            ErrorMessage = errorMessage,
            AttemptCount = _retryCount,
            FirstAttemptAt = DateTime.UtcNow.AddSeconds(-_retryCount * 5), // Estimate
            FailedAt = DateTime.UtcNow,
            Resolved = false
        };

        await dbContext.DeadLetterMessages.AddAsync(deadLetterMessage);
        await dbContext.SaveChangesAsync();
        
        _logger.LogInformation(
            "Dead letter message created with Id: {DLQId}",
            deadLetterMessage.Id);
    }
}
```

---

### Payment Retry Exhausted → DLQ

**File**: `PaymentService/Services/PaymentServiceImpl.cs`

```csharp
public async Task<PaymentResponse> RetryPaymentAsync(Guid paymentId)
{
    var existingPayment = await _dbContext.Payments
        .Find(p => p.Id == paymentId)
        .FirstOrDefaultAsync();

    if (existingPayment == null)
        throw new NotFoundException($"Payment {paymentId} not found");

    const int maxRetries = 3;

    // ✅ Check if retry limit reached
    if (existingPayment.RetryCount >= maxRetries)
    {
        _logger.LogError(
            "Max retries ({MaxRetries}) reached for PaymentId: {PaymentId}. Moving to DLQ.",
            maxRetries, paymentId);
        
        // ✅ Store in DLQ for investigation
        await StorePaymentInDeadLetterQueueAsync(existingPayment, 
            "Maximum retry attempts reached");
        
        // ✅ Update status to indicate permanent failure
        existingPayment.Status = "PERMANENTLY_FAILED";
        existingPayment.ErrorMessage = "Max retries reached. Moved to DLQ for investigation.";
        existingPayment.UpdatedAt = DateTime.UtcNow;
        
        var updateFilter = Builders<Payment>.Filter.Eq(p => p.Id, paymentId);
        var update = Builders<Payment>.Update
            .Set(p => p.Status, existingPayment.Status)
            .Set(p => p.ErrorMessage, existingPayment.ErrorMessage)
            .Set(p => p.UpdatedAt, existingPayment.UpdatedAt);
        
        await _dbContext.Payments.UpdateOneAsync(updateFilter, update);
        
        // ✅ Return response instead of throwing
        return MapToResponse(existingPayment);
    }

    // Continue with normal retry logic...
}

private async Task StorePaymentInDeadLetterQueueAsync(Payment payment, string reason)
{
    var deadLetterMessage = new DeadLetterMessage
    {
        Id = Guid.NewGuid(),
        SourceQueue = "payment_retry",
        EventType = "PaymentRetryFailed",
        Payload = JsonSerializer.Serialize(new
        {
            PaymentId = payment.Id,
            BookingId = payment.BookingId,
            Amount = payment.Amount,
            PaymentMethod = payment.PaymentMethod,
            RetryCount = payment.RetryCount,
            LastRetryAt = payment.LastRetryAt,
            OriginalCreatedAt = payment.CreatedAt,
            FailureReason = payment.ErrorMessage
        }),
        ErrorMessage = reason,
        AttemptCount = payment.RetryCount,
        FirstAttemptAt = payment.CreatedAt,
        FailedAt = DateTime.UtcNow,
        Resolved = false
    };

    await _dbContext.DeadLetterMessages.InsertOneAsync(deadLetterMessage);
    
    _logger.LogInformation(
        "Payment stored in DLQ. PaymentId: {PaymentId}, DLQId: {DLQId}",
        payment.Id, deadLetterMessage.Id);
}
```

---

## 📊 Querying DLQ Messages

### PostgreSQL Queries (BookingService)

**All Unresolved DLQ Messages**:
```sql
SELECT *
FROM "DeadLetterMessages"
WHERE "Resolved" = false
ORDER BY "FailedAt" DESC;
```

**DLQ Messages by Source Queue**:
```sql
SELECT "SourceQueue", COUNT(*) AS "Count"
FROM "DeadLetterMessages"
WHERE "Resolved" = false
GROUP BY "SourceQueue"
ORDER BY "Count" DESC;
```

**Recent DLQ Activity (Last 24 Hours)**:
```sql
SELECT *
FROM "DeadLetterMessages"
WHERE "FailedAt" > NOW() - INTERVAL '24 hours'
ORDER BY "FailedAt" DESC;
```

---

### MongoDB Queries (PaymentService)

**All Failed Payment Retries**:
```javascript
db.dead_letter_messages.find({
  event_type: "PaymentRetryFailed",
  resolved: false
}).sort({ failed_at: -1 })
```

**Payment Details from DLQ**:
```javascript
db.dead_letter_messages.aggregate([
  { $match: { event_type: "PaymentRetryFailed" } },
  { $addFields: { 
      payment_data: { $convert: { input: "$payload", to: "string" } }
  }},
  { $project: {
      _id: 1,
      payment_id: "$payment_data.PaymentId",
      booking_id: "$payment_data.BookingId",
      amount: "$payment_data.Amount",
      retry_count: "$payment_data.RetryCount",
      failed_at: 1,
      error_message: 1,
      resolved: 1
  }}
])
```

**DLQ Messages by Source**:
```javascript
db.dead_letter_messages.aggregate([
  {
    $group: {
      _id: "$source_queue",
      count: { $sum: 1 },
      unresolved: { $sum: { $cond: ["$resolved", 0, 1] } }
    }
  },
  { $sort: { unresolved: -1 } }
])
```

---

## 🔄 Resolution Workflow

### Step 1: Identify Failed Messages

**Query unresolved DLQ messages**:

```sql
-- PostgreSQL
SELECT "Id", "SourceQueue", "EventType", "ErrorMessage", "FailedAt"
FROM "DeadLetterMessages"
WHERE "Resolved" = false
ORDER BY "FailedAt" DESC
LIMIT 10;
```

```javascript
// MongoDB
db.dead_letter_messages.find(
  { resolved: false },
  { _id: 1, source_queue: 1, event_type: 1, error_message: 1, failed_at: 1 }
).sort({ failed_at: -1 }).limit(10)
```

---

### Step 2: Investigate Root Cause

**Examine the payload and error**:

```sql
-- Get full details
SELECT "Payload", "ErrorMessage", "AttemptCount"
FROM "DeadLetterMessages"
WHERE "Id" = 'dlq-message-guid';
```

**Common Issues**:
1. **Data Validation**: Invalid booking ID, negative amount
2. **Service Unavailable**: Database down, RabbitMQ unreachable
3. **Business Logic**: Booking already cancelled, payment method invalid
4. **Network**: Connection timeout, DNS resolution failed

---

### Step 3: Fix the Issue

**Based on root cause**:

1. **Data Issue**: Update booking/payment data in database
2. **Service Issue**: Verify service is healthy, restart if needed
3. **Business Logic**: Resolve booking state inconsistency
4. **Network**: Check infrastructure, firewall rules

---

### Step 4: Replay or Resolve

**Option A: Manual Processing**
```csharp
// Extract payload from DLQ
var dlqMessage = await _dbContext.DeadLetterMessages
    .FirstOrDefaultAsync(m => m.Id == dlqMessageId);

// Manually process
var paymentEvent = JsonSerializer.Deserialize<PaymentSucceededEvent>(
    dlqMessage.Payload);
await ProcessPaymentSucceededAsync(paymentEvent);

// Mark as resolved
dlqMessage.Resolved = true;
dlqMessage.ResolvedAt = DateTime.UtcNow;
dlqMessage.ResolvedBy = "admin@example.com";
dlqMessage.ResolutionNotes = "Manually processed after fixing booking data";
await _dbContext.SaveChangesAsync();
```

**Option B: Republish to Queue**
```csharp
// Republish to original queue for automatic processing
await _eventBus.PublishAsync(
    paymentEvent, 
    dlqMessage.SourceQueue);

// Mark as resolved
dlqMessage.Resolved = true;
dlqMessage.ResolvedAt = DateTime.UtcNow;
dlqMessage.ResolutionNotes = "Republished to queue after fixing root cause";
await _dbContext.SaveChangesAsync();
```

---

### Step 5: Mark as Resolved

```sql
-- PostgreSQL
UPDATE "DeadLetterMessages"
SET 
    "Resolved" = true,
    "ResolvedAt" = NOW(),
    "ResolvedBy" = 'admin@example.com',
    "ResolutionNotes" = 'Manually processed after fixing data issue'
WHERE "Id" = 'dlq-message-guid';
```

```javascript
// MongoDB
db.dead_letter_messages.updateOne(
  { _id: ObjectId("dlq-message-id") },
  {
    $set: {
      resolved: true,
      resolved_at: new Date(),
      resolved_by: "admin@example.com",
      resolution_notes: "Manually processed after fixing data issue"
    }
  }
)
```

---

## 📊 Monitoring and Alerts

### Key Metrics

1. **DLQ Message Count**: Number of unresolved messages
2. **DLQ Growth Rate**: Messages added per hour
3. **Resolution Time**: Average time from failure to resolution
4. **Resolution Rate**: % of messages resolved within SLA

---

### Seq Queries

**DLQ Message Creation Rate**:
```sql
select count(*) as DLQMessagesCreated
from stream
where @Message like '%Dead letter message created%'
  and @Timestamp > Now() - 1h
```

**Messages by Source Queue**:
```sql
select SourceQueue, count(*) as MessageCount
from stream
where @Message like '%Moving to DLQ%'
group by SourceQueue
order by MessageCount desc
```

---

### Alert Configuration

**Alert: High DLQ Volume**:
```sql
-- Trigger if > 10 unresolved DLQ messages
SELECT COUNT(*) AS UnresolvedCount
FROM "DeadLetterMessages"
WHERE "Resolved" = false
HAVING COUNT(*) > 10
```

**Alert: Old Unresolved Messages**:
```sql
-- Trigger if messages > 24 hours old
SELECT COUNT(*) AS OldMessagesCount
FROM "DeadLetterMessages"
WHERE "Resolved" = false
  AND "FailedAt" < NOW() - INTERVAL '24 hours'
HAVING COUNT(*) > 0
```

**Alert: Permanently Failed Payments**:
```javascript
// MongoDB
db.payments.countDocuments({
  status: "PERMANENTLY_FAILED",
  updated_at: { $gt: new Date(Date.now() - 3600000) } // Last hour
})
// Alert if count > 5
```

---

## 🎯 Best Practices

### 1. Set Appropriate Retry Limits

```csharp
// ❌ Too few - doesn't give transient errors a chance
const int MAX_REQUEUE_ATTEMPTS = 1;

// ✅ Good - allows for transient failures
const int MAX_REQUEUE_ATTEMPTS = 3;

// ⚠️ Too many - delays DLQ entry, blocks queue
const int MAX_REQUEUE_ATTEMPTS = 10;
```

**Rule of Thumb**: 3 attempts is standard for most scenarios.

---

### 2. Include Rich Context in DLQ

```csharp
// ✅ Good - includes all context
var deadLetterMessage = new DeadLetterMessage
{
    SourceQueue = "payment_succeeded",
    EventType = "PaymentSucceeded",
    Payload = originalMessage,  // Full original message
    ErrorMessage = ex.Message + "\n" + ex.StackTrace,
    AttemptCount = _retryCount,
    FirstAttemptAt = firstAttemptTime,
    FailedAt = DateTime.UtcNow
};
```

**Why?** More context = easier troubleshooting and resolution.

---

### 3. Don't Requeue Forever

```csharp
// ❌ Bad - infinite requeue
if (processingFailed)
{
    _channel.BasicNack(ea.DeliveryTag, false, requeue: true);
}

// ✅ Good - track attempts, move to DLQ after limit
_retryCount++;
if (_retryCount >= MAX_REQUEUE_ATTEMPTS)
{
    await CreateDeadLetterQueueMessageAsync(message, error);
    _channel.BasicNack(ea.DeliveryTag, false, requeue: false);
}
else
{
    _channel.BasicNack(ea.DeliveryTag, false, requeue: true);
}
```

---

### 4. Separate DLQ Queues per Event Type

```
payment_succeeded_dlq  ← Only failed PaymentSucceeded events
payment_failed_dlq     ← Only failed PaymentFailed events
booking_created_dlq    ← Only failed BookingCreated events
```

**Benefits**:
- Easier to identify which event type is failing
- Can have different resolution workflows
- Better monitoring and alerting

---

### 5. Regular DLQ Review

**Daily**:
- Check for new DLQ messages
- Resolve messages < 24 hours old

**Weekly**:
- Review unresolved messages > 1 week old
- Identify patterns (same error repeated)
- Update code to prevent future occurrences

**Monthly**:
- Analyze DLQ trends
- Measure resolution time SLA
- Review and update retry strategies

---

## ⚠️ Common Pitfalls

### 1. No DLQ - Messages Lost Forever

❌ **Problem**:
```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Processing failed");
    _channel.BasicNack(ea.DeliveryTag, false, requeue: false);
    // Message discarded forever!
}
```

✅ **Solution**:
```csharp
catch (Exception ex)
{
    if (_retryCount >= MAX_REQUEUE_ATTEMPTS)
    {
        await CreateDeadLetterQueueMessageAsync(message, ex.Message);
    }
    _channel.BasicNack(ea.DeliveryTag, false, requeue: _retryCount < MAX_REQUEUE_ATTEMPTS);
}
```

---

### 2. Infinite Requeue (No Limit)

❌ **Problem**:
```csharp
// Always requeue - will retry forever
_channel.BasicNack(ea.DeliveryTag, false, requeue: true);
```

✅ **Solution**:
```csharp
// Track attempts and give up after limit
_retryCount++;
bool shouldRequeue = _retryCount < MAX_REQUEUE_ATTEMPTS;
_channel.BasicNack(ea.DeliveryTag, false, requeue: shouldRequeue);
```

---

### 3. Not Logging DLQ Entry

❌ **Problem**:
```csharp
// Silent DLQ creation - hard to detect
await _dbContext.DeadLetterMessages.AddAsync(dlqMessage);
await _dbContext.SaveChangesAsync();
```

✅ **Solution**:
```csharp
await _dbContext.DeadLetterMessages.AddAsync(dlqMessage);
await _dbContext.SaveChangesAsync();

_logger.LogError(
    "Message moved to DLQ. DLQId: {DLQId}, SourceQueue: {SourceQueue}, Error: {Error}",
    dlqMessage.Id, dlqMessage.SourceQueue, dlqMessage.ErrorMessage);
```

---

### 4. No Resolution Workflow

❌ **Problem**: Messages accumulate in DLQ with no process to resolve them.

✅ **Solution**: Establish clear workflow:
1. Daily review of DLQ
2. Investigate root cause
3. Fix and replay/resolve
4. Mark as resolved
5. Prevent future occurrences

---

## 🎓 Key Takeaways

1. **DLQ prevents message loss** when processing fails after retries
2. **Three entry points**: Outbox publishing, consumer processing, payment retry exhausted
3. **Set retry limits** (3 attempts is standard) before moving to DLQ
4. **Include rich context** in DLQ messages for easier troubleshooting
5. **Establish resolution workflow** to regularly process DLQ messages
6. **Monitor DLQ metrics** (count, growth rate, resolution time)
7. **Separate DLQ queues** per event type for better organization
8. **Always log DLQ entry** for visibility and alerting

---

## 📚 Further Reading

- [RabbitMQ Dead Letter Exchanges](https://www.rabbitmq.com/dlx.html)
- [AWS SQS Dead Letter Queues](https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-dead-letter-queues.html)
- [Azure Service Bus Dead-letter queues](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-dead-letter-queues)
- [Martin Fowler: Event-Driven Architecture](https://martinfowler.com/articles/201701-event-driven.html)

---

## 🔗 Related Documentation

- [Retry Patterns with Polly](./retry-patterns-polly.md)
- [Connection Resilience](./connection-resilience.md)
- [Circuit Breaker](./circuit-breaker.md)
- [Complete DLQ Flow](/docs/phase3-event-integration/COMPLETE_DLQ_FLOW.md)
- [DLQ Implementation Guide](/docs/phase3-event-integration/DLQ_HANDLER_IMPLEMENTATION.md)

---

**Implementation Status**: ✅ Production Ready  
**Last Updated**: November 12, 2025
