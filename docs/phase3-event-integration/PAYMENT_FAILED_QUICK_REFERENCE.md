# Payment Failed Event - Quick Reference

## What Was Implemented

### PaymentService
- ✅ Publishes `PaymentFailedEvent` when payment processing fails (10% failure rate)
- ✅ Saves event to Outbox for guaranteed delivery
- ✅ Logs failure with reason

### BookingService
- ✅ New consumer: `PaymentFailedConsumer`
- ✅ Listens to `payment_failed` queue
- ✅ Updates booking status to `CANCELLED`
- ✅ Records cancellation reason
- ✅ Sets `CancelledAt` timestamp

## Event Flow Diagram

```
PaymentService                  RabbitMQ                BookingService
     |                             |                           |
     | Payment fails (10%)         |                           |
     |--------------------------> |                           |
     | PaymentFailedEvent          |                           |
     |                             |                           |
     |                             | Deliver event             |
     |                             |-------------------------->|
     |                             |                           |
     |                             |              Update Booking:
     |                             |              Status = CANCELLED
     |                             |              CancellationReason set
     |                             |              CancelledAt set
     |                             |                           |
     |                             | <-------------------------|
     |                             | ACK message               |
```

## Testing

### Create & Process Payment
```bash
# 1. Create booking
POST http://localhost:5000/booking/api/bookings
Authorization: Bearer <token>
{
  "userId": "<guid>",
  "roomId": "ROOM-101",
  "amount": 500000
}

# 2. Process payment (may fail 10% of time)
POST http://localhost:5000/payment/api/payment/pay
Authorization: Bearer <token>
{
  "bookingId": "<booking-id>",
  "amount": 500000
}

# 3. Check booking status
GET http://localhost:5000/booking/api/bookings/{bookingId}
Authorization: Bearer <token>
```

### Expected Results

**If payment succeeds (90%):**
- Payment Status: `SUCCESS`
- Booking Status: `CONFIRMED`

**If payment fails (10%):**
- Payment Status: `FAILED`
- Booking Status: `CANCELLED`
- Cancellation Reason: `"Payment failed: Payment processing failed"`

## Monitoring

### RabbitMQ
- URL: http://localhost:15672
- Check `payment_failed` queue for messages

### Seq
- URL: http://localhost:5341
- Search for: `"PaymentFailed"` or `"CANCELLED"`

## Configuration

### BookingService (appsettings.json)
```json
"RabbitMQ": {
  "Queues": {
    "PaymentFailed": "payment_failed"
  }
}
```

### PaymentService (appsettings.json)
```json
"RabbitMQ": {
  "Queues": {
    "PaymentFailed": "payment_failed"
  }
}
```

## Files Changed

1. `src/PaymentService/Services/PaymentServiceImpl.cs` - Publish event
2. `src/BookingService/Consumers/PaymentFailedConsumer.cs` - New consumer
3. `src/BookingService/Program.cs` - Register consumer
4. `src/BookingService/appsettings.json` - Queue config

## Key Features

- ✅ Automatic booking cancellation on payment failure
- ✅ Outbox pattern for reliable event delivery
- ✅ Retry logic with exponential backoff
- ✅ Idempotent event processing
- ✅ Dead letter queue for failed messages
- ✅ Comprehensive logging

---

**Status:** Complete and Ready! 🎉
