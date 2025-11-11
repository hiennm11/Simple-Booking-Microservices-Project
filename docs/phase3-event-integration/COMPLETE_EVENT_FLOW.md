# Complete Event Flow - With Payment Failed Event

## Overview

This document shows the complete event flow in the booking system, including the newly implemented Payment Failed event handling.

## Event Flow Scenarios

### Scenario 1: Successful Payment (90% probability)

```
┌──────────────┐
│   Client     │
└──────┬───────┘
       │
       │ 1. POST /bookings (create booking)
       ▼
┌──────────────────┐
│ BookingService   │
└──────┬───────────┘
       │
       │ 2. Save to DB (Status: PENDING)
       │
       │ 3. Publish BookingCreated event
       ▼
┌──────────────────┐
│    RabbitMQ      │
│ Queue: booking_  │
│       created    │
└──────┬───────────┘
       │
       │ 4. Optional: Auto-process payment
       │    (or manual via API call)
       ▼
┌──────────────────┐
│ PaymentService   │
└──────┬───────────┘
       │
       │ 5. Process payment (90% success)
       │
       │ 6. Save payment to MongoDB (Status: SUCCESS)
       │
       │ 7. Publish PaymentSucceeded event to Outbox
       ▼
┌──────────────────┐
│    MongoDB       │
│  Outbox Table    │
└──────┬───────────┘
       │
       │ 8. OutboxPublisher picks up event
       ▼
┌──────────────────┐
│    RabbitMQ      │
│ Queue: payment_  │
│      succeeded   │
└──────┬───────────┘
       │
       │ 9. PaymentSucceededConsumer receives event
       ▼
┌──────────────────┐
│ BookingService   │
└──────┬───────────┘
       │
       │ 10. Update booking (Status: CONFIRMED)
       │     Set ConfirmedAt timestamp
       │
       │ 11. Save to PostgreSQL
       ▼
┌──────────────────┐
│   PostgreSQL     │
│   Booking DB     │
└──────────────────┘

Result: Booking CONFIRMED ✅
```

### Scenario 2: Failed Payment (10% probability) 🆕

```
┌──────────────┐
│   Client     │
└──────┬───────┘
       │
       │ 1. POST /bookings (create booking)
       ▼
┌──────────────────┐
│ BookingService   │
└──────┬───────────┘
       │
       │ 2. Save to DB (Status: PENDING)
       │
       │ 3. Publish BookingCreated event
       ▼
┌──────────────────┐
│    RabbitMQ      │
│ Queue: booking_  │
│       created    │
└──────┬───────────┘
       │
       │ 4. Optional: Auto-process payment
       │    (or manual via API call)
       ▼
┌──────────────────┐
│ PaymentService   │
└──────┬───────────┘
       │
       │ 5. Process payment (10% failure) ❌
       │
       │ 6. Save payment to MongoDB (Status: FAILED)
       │
       │ 7. Publish PaymentFailed event to Outbox 🆕
       ▼
┌──────────────────┐
│    MongoDB       │
│  Outbox Table    │
└──────┬───────────┘
       │
       │ 8. OutboxPublisher picks up event
       ▼
┌──────────────────┐
│    RabbitMQ      │
│ Queue: payment_  │
│       failed     │ 🆕
└──────┬───────────┘
       │
       │ 9. PaymentFailedConsumer receives event 🆕
       ▼
┌──────────────────┐
│ BookingService   │
└──────┬───────────┘
       │
       │ 10. Update booking (Status: CANCELLED) 🆕
       │     Set CancellationReason
       │     Set CancelledAt timestamp
       │
       │ 11. Save to PostgreSQL
       ▼
┌──────────────────┐
│   PostgreSQL     │
│   Booking DB     │
└──────────────────┘

Result: Booking CANCELLED ❌
```

## Event Catalog

### 1. BookingCreated Event

**Publisher:** BookingService  
**Consumer:** PaymentService (optional)  
**Queue:** `booking_created`

```json
{
  "eventId": "guid",
  "eventName": "BookingCreated",
  "timestamp": "2025-11-11T10:00:00Z",
  "data": {
    "bookingId": "guid",
    "userId": "guid",
    "roomId": "ROOM-101",
    "amount": 500000,
    "status": "PENDING"
  }
}
```

### 2. PaymentSucceeded Event

**Publisher:** PaymentService  
**Consumer:** BookingService  
**Queue:** `payment_succeeded`

```json
{
  "eventId": "guid",
  "eventName": "PaymentSucceeded",
  "timestamp": "2025-11-11T10:00:05Z",
  "data": {
    "paymentId": "guid",
    "bookingId": "guid",
    "amount": 500000,
    "status": "SUCCESS"
  }
}
```

### 3. PaymentFailed Event 🆕

**Publisher:** PaymentService  
**Consumer:** BookingService  
**Queue:** `payment_failed`

```json
{
  "eventId": "guid",
  "eventName": "PaymentFailed",
  "timestamp": "2025-11-11T10:00:05Z",
  "data": {
    "paymentId": "guid",
    "bookingId": "guid",
    "amount": 500000,
    "reason": "Payment processing failed",
    "status": "FAILED"
  }
}
```

## State Transitions

### Booking States

```
PENDING ──────┬──────> CONFIRMED (on PaymentSucceeded)
              │
              └──────> CANCELLED (on PaymentFailed) 🆕
```

### Payment States

```
PENDING ──────┬──────> SUCCESS (90% probability)
              │
              └──────> FAILED (10% probability)
```

## Database Schema Changes

### Booking Table (PostgreSQL)

| Field | Type | Description |
|-------|------|-------------|
| Id | GUID | Primary key |
| UserId | GUID | User who made booking |
| RoomId | VARCHAR | Room identifier |
| Amount | DECIMAL | Booking amount |
| Status | VARCHAR | PENDING/CONFIRMED/CANCELLED |
| ConfirmedAt | TIMESTAMP | When confirmed (nullable) |
| CancelledAt | TIMESTAMP | When cancelled (nullable) 🆕 |
| CancellationReason | VARCHAR | Why cancelled (nullable) 🆕 |
| CreatedAt | TIMESTAMP | Creation time |
| UpdatedAt | TIMESTAMP | Last update time |

### Payment Collection (MongoDB)

| Field | Type | Description |
|-------|------|-------------|
| Id | ObjectId | Primary key |
| BookingId | GUID | Related booking |
| Amount | Decimal | Payment amount |
| Status | String | PENDING/SUCCESS/FAILED |
| PaymentMethod | String | Payment method |
| TransactionId | String | Transaction ID (nullable) |
| ErrorMessage | String | Error message (nullable) 🆕 |
| CreatedAt | DateTime | Creation time |
| ProcessedAt | DateTime | Processing time (nullable) |
| UpdatedAt | DateTime | Last update time |

### OutboxMessage Collection (MongoDB)

| Field | Type | Description |
|-------|------|-------------|
| Id | ObjectId | Primary key |
| EventType | String | PaymentSucceeded/PaymentFailed 🆕 |
| Payload | String | JSON event data |
| IsProcessed | Boolean | Processing status |
| ProcessedAt | DateTime | When processed (nullable) |
| RetryCount | Int | Retry attempts |
| LastError | String | Last error message (nullable) |
| CreatedAt | DateTime | Creation time |

## RabbitMQ Queues

| Queue Name | Durable | Consumer | Publisher |
|------------|---------|----------|-----------|
| booking_created | Yes | PaymentService | BookingService |
| payment_succeeded | Yes | BookingService | PaymentService |
| payment_failed 🆕 | Yes | BookingService | PaymentService |

## Monitoring & Observability

### Key Metrics to Monitor

1. **Payment Success Rate:** Should be ~90%
2. **Payment Failure Rate:** Should be ~10%
3. **Event Processing Time:** Average time from publish to consume
4. **Booking Cancellation Rate:** How many bookings get cancelled
5. **Queue Depth:** Messages waiting in RabbitMQ queues

### Log Queries in Seq

**All Payment Events:**
```
EventName = "PaymentSucceeded" OR EventName = "PaymentFailed"
```

**Failed Payments:**
```
EventName = "PaymentFailed"
```

**Cancelled Bookings:**
```
Status = "CANCELLED"
```

**Booking Journey (by BookingId):**
```
BookingId = "your-booking-guid"
```

### RabbitMQ Monitoring

1. Check queue depths at http://localhost:15672
2. Monitor message rates (publish/consume)
3. Check for unacked messages
4. Monitor dead letter queues

## Error Handling

### Retry Strategy

**Connection Retries (RabbitMQ):**
- Max Attempts: 10
- Initial Delay: 5 seconds
- Backoff: Exponential
- Max Delay: 60 seconds

**Message Processing Retries:**
- Max Attempts: 3
- Initial Delay: 2 seconds
- Backoff: Exponential

**Dead Letter Handling:**
- After 3 failed processing attempts
- Message moved to DLQ (not requeued)
- TODO: Store in database for manual review

### Idempotency

Both consumers check current state before updating:

**PaymentSucceededConsumer:**
- Skip if booking already CONFIRMED

**PaymentFailedConsumer:** 🆕
- Skip if booking already CANCELLED

## Testing Checklist

- [ ] Create booking (Status: PENDING)
- [ ] Process payment successfully (Status: CONFIRMED)
- [ ] Create booking (Status: PENDING)
- [ ] Process payment with failure (Status: CANCELLED) 🆕
- [ ] Verify RabbitMQ queues receive messages
- [ ] Verify Seq logs show event flow
- [ ] Verify MongoDB outbox messages processed
- [ ] Verify PostgreSQL booking status updates
- [ ] Test payment retry multiple times
- [ ] Monitor payment success/failure rates

## Architecture Benefits

1. **Saga Pattern:** Choreography-based saga with compensation
2. **Eventual Consistency:** Asynchronous state updates
3. **Resilience:** Retry logic and error handling
4. **Decoupling:** Services don't call each other directly
5. **Scalability:** Event-driven architecture scales well
6. **Observability:** Complete audit trail in logs
7. **Reliability:** Outbox pattern ensures event delivery

---

**Implementation Complete!** 🎉

The system now handles both successful and failed payments automatically, updating booking status accordingly through event-driven architecture.
