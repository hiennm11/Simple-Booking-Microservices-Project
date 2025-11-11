# Payment Failed Event Implementation - Complete ✅

## Overview
This document describes the implementation of the PaymentFailed event functionality that handles payment failures and automatically cancels bookings when payments fail.

## Implementation Summary

### 1. PaymentService Changes ✅

#### Modified Files:
- **`src/PaymentService/Services/PaymentServiceImpl.cs`**

#### What Was Added:
When payment processing fails (10% failure rate in simulation), the PaymentService now:
1. Updates payment status to "FAILED"
2. Stores error message
3. Creates a `PaymentFailedEvent` with details:
   - PaymentId
   - BookingId
   - Amount
   - Reason (error message)
   - Status ("FAILED")
4. Saves the event to the Outbox for guaranteed delivery
5. Logs the event publishing

#### Code Changes:
```csharp
// Save PaymentFailed event to outbox
var paymentFailedEvent = new PaymentFailedEvent
{
    EventId = Guid.NewGuid(),
    EventName = "PaymentFailed",
    Timestamp = DateTime.UtcNow,
    Data = new PaymentFailedData
    {
        PaymentId = payment.Id,
        BookingId = payment.BookingId,
        Amount = payment.Amount,
        Reason = payment.ErrorMessage ?? "Payment processing failed",
        Status = "FAILED"
    }
};

await _outboxService.AddToOutboxAsync(paymentFailedEvent, "PaymentFailed");
```

### 2. BookingService Changes ✅

#### New Files Created:
- **`src/BookingService/Consumers/PaymentFailedConsumer.cs`**

#### What Was Added:
A new background consumer service that:
1. Connects to RabbitMQ with retry logic (10 attempts)
2. Listens to the `payment_failed` queue
3. Deserializes PaymentFailedEvent messages
4. Updates booking status to "CANCELLED"
5. Records cancellation reason from the payment failure
6. Sets CancelledAt timestamp
7. Handles message acknowledgement with retry logic (3 attempts)
8. Implements dead-letter queue handling after max retries

#### Key Features:
- **Resilience Patterns:**
  - Connection retry with exponential backoff
  - Message processing retry (3 attempts)
  - Automatic RabbitMQ recovery
  - Dead-letter queue for failed messages

- **Idempotency:**
  - Checks if booking is already cancelled
  - Skips update if already in CANCELLED state
  - Prevents duplicate processing

- **Error Handling:**
  - Logs all events and errors
  - Gracefully handles invalid messages
  - Proper message acknowledgement (ACK/NACK)

#### Updated Files:
- **`src/BookingService/Program.cs`** - Registered PaymentFailedConsumer as hosted service
- **`src/BookingService/appsettings.json`** - Added PaymentFailed queue configuration

### 3. Configuration Changes ✅

#### BookingService appsettings.json:
```json
"RabbitMQ": {
  "Queues": {
    "BookingCreated": "booking_created",
    "PaymentSucceeded": "payment_succeeded",
    "PaymentFailed": "payment_failed"  // ← Added
  }
}
```

#### PaymentService appsettings.json:
Already had the PaymentFailed queue configured.

## Event Flow

### Complete Booking → Payment → Update Flow

#### Scenario 1: Payment Success (90% probability)
```
1. BookingService creates booking (Status: PENDING)
2. BookingService publishes BookingCreated event
3. PaymentService processes payment → SUCCESS
4. PaymentService publishes PaymentSucceeded event
5. BookingService receives event → Updates booking (Status: CONFIRMED)
```

#### Scenario 2: Payment Failure (10% probability) ✨ NEW
```
1. BookingService creates booking (Status: PENDING)
2. BookingService publishes BookingCreated event
3. PaymentService processes payment → FAILED
4. PaymentService publishes PaymentFailed event ← NEW
5. BookingService receives event → Updates booking (Status: CANCELLED) ← NEW
```

## Event Contract

### PaymentFailedEvent (Already exists in Shared.Contracts)
```csharp
public class PaymentFailedEvent
{
    public Guid EventId { get; set; }
    public string EventName { get; set; } = "PaymentFailed";
    public DateTime Timestamp { get; set; }
    public PaymentFailedData Data { get; set; }
}

public class PaymentFailedData
{
    public Guid PaymentId { get; set; }
    public Guid BookingId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; }
    public string Status { get; set; } = "FAILED";
}
```

## Database Updates

### Booking Model Fields (Already existed):
- `Status` - Updated to "CANCELLED"
- `CancelledAt` - Set to current timestamp
- `CancellationReason` - Set to payment failure reason
- `UpdatedAt` - Set to current timestamp

### Payment Model Fields:
- `Status` - Set to "FAILED"
- `ErrorMessage` - Contains failure reason
- `ProcessedAt` - Set when processing completes
- `UpdatedAt` - Set to current timestamp

## Testing Instructions

### Manual Testing

1. **Create a Booking:**
```bash
POST http://localhost:5000/booking/api/bookings
Authorization: Bearer <your-jwt-token>
Content-Type: application/json

{
  "userId": "user-guid",
  "roomId": "ROOM-101",
  "amount": 500000
}
```

2. **Process Payment (may fail):**
```bash
POST http://localhost:5000/payment/api/payment/pay
Authorization: Bearer <your-jwt-token>
Content-Type: application/json

{
  "bookingId": "booking-guid-from-step-1",
  "amount": 500000
}
```

3. **Check Booking Status:**
```bash
GET http://localhost:5000/booking/api/bookings/{bookingId}
Authorization: Bearer <your-jwt-token>
```

Expected results:
- If payment succeeds: `Status = "CONFIRMED"`
- If payment fails: `Status = "CANCELLED"`, `CancellationReason = "Payment failed: Payment processing failed"`

### Monitor Event Flow

1. **RabbitMQ Management UI:**
   - URL: http://localhost:15672
   - Credentials: guest/guest
   - Check queues:
     - `payment_succeeded`
     - `payment_failed` ← NEW

2. **Seq Logging:**
   - URL: http://localhost:5341
   - Search for events:
     - "PaymentFailed event saved to outbox"
     - "Processing PaymentFailed event"
     - "Booking status updated to CANCELLED"

### Test Payment Failure Specifically

Since payment has a 90% success rate, you may need to call the payment endpoint multiple times to trigger a failure. Each call has a 10% chance of failure.

```powershell
# PowerShell script to trigger payment failure
for ($i = 1; $i -le 20; $i++) {
    Write-Host "Attempt $i..."
    # Create booking and process payment
    # Check if it failed
}
```

## Architecture Highlights

### Outbox Pattern
Both PaymentSucceeded and PaymentFailed events use the Outbox pattern:
1. Event saved to outbox collection in MongoDB
2. OutboxPublisherService polls for pending events
3. Events published to RabbitMQ
4. Events marked as processed after successful publish
5. Retry logic for failed publishes

### Resilience Patterns
- **Retry with Exponential Backoff:** Both connection and message processing
- **Circuit Breaker:** Implicit through max retry attempts
- **Graceful Degradation:** Messages sent to DLQ after max retries
- **Idempotency:** Duplicate event handling

### Event-Driven Architecture
- **Decoupled Services:** BookingService doesn't know about PaymentService internals
- **Asynchronous Processing:** Non-blocking event handling
- **Eventual Consistency:** Status updates happen asynchronously
- **Choreography Pattern:** Each service reacts to events independently

## Benefits

1. **Automatic Compensation:** Bookings are automatically cancelled when payments fail
2. **Reliable Messaging:** Outbox pattern ensures no event loss
3. **Resilient Processing:** Retry logic handles transient failures
4. **Audit Trail:** All events logged with timestamps and reasons
5. **Monitoring:** Easy to track payment failures in Seq and RabbitMQ
6. **Idempotent:** Safe to replay events without side effects

## Files Modified Summary

### PaymentService (1 file):
- ✅ `Services/PaymentServiceImpl.cs` - Added PaymentFailed event publishing

### BookingService (3 files):
- ✅ `Consumers/PaymentFailedConsumer.cs` - New consumer for PaymentFailed events
- ✅ `Program.cs` - Registered PaymentFailedConsumer
- ✅ `appsettings.json` - Added PaymentFailed queue configuration

### Shared (0 files):
- `Contracts/PaymentFailedEvent.cs` - Already existed

## Next Steps (Optional Enhancements)

1. **Notification Service:**
   - Send email/SMS when payment fails
   - Notify user about cancellation

2. **Retry Payment:**
   - Allow users to retry failed payments
   - Store payment attempt history

3. **Dead Letter Queue Handler:**
   - Implement DLQ consumer for failed messages
   - Store in database for manual investigation
   - Admin dashboard for reviewing failed events

4. **Advanced Monitoring:**
   - Dashboard for payment success/failure rates
   - Alerts for high failure rates
   - Payment failure analytics

5. **Compensation Saga:**
   - More complex compensation logic
   - Refund processing
   - Inventory management

## Success Criteria ✅

- ✅ PaymentService publishes PaymentFailed event when payment fails
- ✅ BookingService consumes PaymentFailed event
- ✅ Booking status updated to CANCELLED automatically
- ✅ Cancellation reason recorded
- ✅ Outbox pattern used for reliable event delivery
- ✅ Retry logic implemented for resilience
- ✅ Configuration updated for payment_failed queue
- ✅ Logging and monitoring in place

---

**Implementation Status:** Complete and Ready for Testing! 🎉
