# Complete DLQ Flow - All Failure Points Covered ✅

## Overview
The Dead Letter Queue now captures failures at **all three critical points** in the payment/booking flow:

1. **Outbox Publishing Failures** - When OutboxPublisher can't publish to RabbitMQ
2. **Consumer Processing Failures** - When consumers can't process messages from RabbitMQ
3. **Payment Retry Failures** - When payment retries are exhausted (NEW ✅)

## Three DLQ Entry Points

### 1. Outbox Publisher → DLQ
**Trigger:** OutboxPublisher fails to publish message after max retries (3-5 attempts)

**Flow:**
```
Event → Outbox → Try publish → FAIL (RabbitMQ down/error)
→ Retry (up to max) → Still failing
→ Store in DeadLetterMessages
→ Mark outbox message as published (remove from queue)
```

**Source Queue:** `outbox_{event_type}`
**Event Types:** `PaymentSucceeded`, `PaymentFailed`, `BookingCreated`

### 2. Message Consumer → DLQ
**Trigger:** Consumer fails to process message after 3 attempts

**Flow:**
```
RabbitMQ Queue → Consumer receives → Processing fails
→ NACK with requeue (attempt 1)
→ Retry → Still failing (attempt 2)
→ Retry → Still failing (attempt 3)
→ Send to DLQ queue with metadata
→ ACK original message
→ DeadLetterQueueHandler consumes from DLQ
→ Store in DeadLetterMessages table/collection
```

**Source Queue:** `payment_failed`, `payment_succeeded`, `booking_created`
**DLQ Queues:** `payment_failed_dlq`, `payment_succeeded_dlq`

### 3. Payment Retry Exhausted → DLQ (NEW ✅)
**Trigger:** Payment retry API called when retry count >= max (3 attempts)

**Flow:**
```
API: POST /payment/retry → Check retry count
→ If retry_count >= 3:
   → Log error: Max retries reached
   → Create DeadLetterMessage with payment details
   → Store in DeadLetterMessages collection
   → Update payment status to "PERMANENTLY_FAILED"
   → Return response (don't throw exception)
→ Payment can't be retried again via API
→ Requires manual investigation/resolution
```

**Source Queue:** `payment_retry`
**Event Type:** `PaymentRetryFailed`

## Code Changes for Payment Retry DLQ

### Updated: `PaymentServiceImpl.RetryPaymentAsync()`

**Before (Problematic):**
```csharp
if (existingPayment.RetryCount >= maxRetries)
{
    _logger.LogWarning("Max retries reached...");
    throw new InvalidOperationException("Max retries reached"); // ❌ Throws, no DLQ
}
```

**After (Fixed):**
```csharp
if (existingPayment.RetryCount >= maxRetries)
{
    _logger.LogError("Max retries reached. Moving to DLQ...");
    
    // ✅ Store in DLQ for investigation
    await StorePaymentInDeadLetterQueueAsync(existingPayment, 
        "Maximum retry attempts reached");
    
    // ✅ Update status to indicate permanent failure
    existingPayment.Status = "PERMANENTLY_FAILED";
    existingPayment.ErrorMessage = "Max retries reached. Moved to DLQ.";
    
    // ✅ Update in database
    await _dbContext.Payments.UpdateOneAsync(...);
    
    // ✅ Return response instead of throwing
    return MapToResponse(existingPayment);
}
```

### New Method: `StorePaymentInDeadLetterQueueAsync()`

```csharp
private async Task StorePaymentInDeadLetterQueueAsync(Payment payment, string reason)
{
    var deadLetterMessage = new DeadLetterMessage
    {
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
}
```

## Payment Status Values

| Status | Meaning | Next Action |
|--------|---------|-------------|
| `PENDING` | Payment being processed | Wait for result |
| `SUCCESS` | Payment succeeded | Booking confirmed |
| `FAILED` | Payment failed but can retry | Call retry API (if retries < 3) |
| `PERMANENTLY_FAILED` | Max retries exhausted | Manual investigation required |

## Complete End-to-End Scenarios

### Scenario A: Payment Succeeds
```
1. Create booking → Status: PENDING
2. Process payment → SUCCESS
3. Event: PaymentSucceeded → Outbox
4. OutboxPublisher → RabbitMQ
5. BookingService consumes → Update booking: CONFIRMED
✅ Happy path - no DLQ
```

### Scenario B: Payment Fails, Retry Succeeds
```
1. Create booking → Status: PENDING
2. Process payment → FAILED
3. Event: PaymentFailed → Outbox
4. OutboxPublisher → RabbitMQ
5. BookingService consumes → Update booking: CANCELLED
6. User calls retry API → SUCCESS
7. Event: PaymentSucceeded → Outbox
8. BookingService consumes → Update booking: CONFIRMED
✅ Resolved via retry - no DLQ
```

### Scenario C: Payment Fails, All Retries Fail (NEW - DLQ Capture)
```
1. Create booking → Status: PENDING
2. Process payment → FAILED (RetryCount: 0)
3. Event: PaymentFailed → Booking: CANCELLED
4. User calls retry API → FAILED (RetryCount: 1)
5. Event: PaymentFailed → Booking: stays CANCELLED
6. User calls retry API → FAILED (RetryCount: 2)
7. Event: PaymentFailed → Booking: stays CANCELLED
8. User calls retry API → FAILED (RetryCount: 3)
9. Event: PaymentFailed → Booking: stays CANCELLED
10. User calls retry API (4th time):
    ❌ RetryCount (3) >= MaxRetries (3)
    → Store payment in DeadLetterMessages ✅
    → Update payment status: PERMANENTLY_FAILED ✅
    → Return response with error message ✅
11. Payment now in DLQ for manual investigation
```

### Scenario D: Outbox Publishing Fails
```
1. Payment processed → Event saved to Outbox
2. OutboxPublisher tries to publish → RabbitMQ connection fails
3. Retry 1 → Still failing
4. Retry 2 → Still failing
5. Retry 3 → Still failing (max retries reached)
6. OutboxPublisher:
   → Store outbox message in DeadLetterMessages ✅
   → Mark outbox message as published (remove from queue) ✅
7. Event in DLQ for manual investigation
```

### Scenario E: Consumer Processing Fails
```
1. Event published to RabbitMQ queue
2. Consumer receives → Processing throws exception
3. Consumer NACKs with requeue (attempt 1)
4. Consumer receives again → Still failing (attempt 2)
5. Consumer receives again → Still failing (attempt 3)
6. Consumer:
   → Create DLQ message with error details ✅
   → Publish to DLQ queue (payment_failed_dlq) ✅
   → ACK original message (remove from main queue) ✅
7. DeadLetterQueueHandler:
   → Consume from DLQ queue ✅
   → Store in DeadLetterMessages table ✅
8. Message in DLQ for manual investigation
```

## Querying DLQ Messages

### All Failed Payment Retries (MongoDB)
```javascript
db.dead_letter_messages.find({
  event_type: "PaymentRetryFailed",
  resolved: false
}).sort({ failed_at: -1 })
```

### Payment Details from DLQ
```javascript
db.dead_letter_messages.aggregate([
  { $match: { event_type: "PaymentRetryFailed" } },
  { $addFields: { 
      payment_data: { $convert: { input: "$payload", to: "json" } }
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

### All DLQ Messages by Source
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

## API Response Changes

### Before (Threw Exception)
```
POST /api/payment/retry
Status: 400 Bad Request
{
  "error": "Maximum retry attempts (3) reached for this payment"
}
```

### After (Returns Response)
```
POST /api/payment/retry
Status: 200 OK
{
  "id": "payment-guid",
  "bookingId": "booking-guid",
  "amount": 500000,
  "status": "PERMANENTLY_FAILED",
  "errorMessage": "Maximum retry attempts (3) reached. Payment moved to Dead Letter Queue.",
  "retryCount": 3,
  "lastRetryAt": "2025-11-11T10:30:00Z"
}
```

## Benefits of This Approach

1. **No Exceptions for Business Logic:** Max retries is a business rule, not an error
2. **Complete Audit Trail:** All permanently failed payments captured in DLQ
3. **Manual Recovery Possible:** Admin can investigate and manually process
4. **Better UX:** Client gets clear status instead of exception
5. **Monitoring:** Can track permanently failed payments easily
6. **Prevents Lost Data:** Nothing disappears without a trace

## Resolution Workflow

### For Permanently Failed Payments

1. **Query DLQ for payment retry failures:**
   ```javascript
   db.dead_letter_messages.find({
     event_type: "PaymentRetryFailed",
     resolved: false
   })
   ```

2. **Investigate the payment:**
   - Check payment method validity
   - Verify customer has funds
   - Check for payment gateway issues
   - Review error messages

3. **Resolve the issue:**
   - Fix data issues
   - Manually process payment through admin panel
   - Or refund/cancel the booking

4. **Mark as resolved:**
   ```javascript
   db.dead_letter_messages.updateOne(
     { _id: ObjectId("dlq-message-id") },
     {
       $set: {
         resolved: true,
         resolved_at: new Date(),
         resolved_by: "admin@example.com",
         resolution_notes: "Manually processed payment after fixing payment method"
       }
     }
   )
   ```

## Monitoring Alerts

Set up alerts for:

1. **High DLQ Volume:**
   ```
   Count(dead_letter_messages where resolved = false) > 10
   ```

2. **Permanently Failed Payments:**
   ```
   Count(payments where status = "PERMANENTLY_FAILED") > 5
   ```

3. **Outbox to DLQ Rate:**
   ```
   Count(dead_letter_messages where source_queue LIKE "outbox_%") > 3
   ```

4. **Consumer Failures:**
   ```
   Count(dead_letter_messages where source_queue NOT LIKE "outbox_%") > 5
   ```

## Summary

✅ **Outbox Publishing Failures** → DLQ (already implemented)
✅ **Consumer Processing Failures** → DLQ (already implemented)
✅ **Payment Retry Exhausted** → DLQ (newly implemented)

All failure points now captured. Nothing gets lost. Complete observability and recoverability!

---

**Status:** Complete ✅
**Build Status:** Successful ✅
