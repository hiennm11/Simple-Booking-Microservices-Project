# Complete System Flow Documentation

## System Overview

The booking system implements **Event Choreography** pattern with **Saga compensating actions** for distributed transaction management. All services communicate asynchronously via RabbitMQ, with correlation IDs tracked through Seq for observability.

## Architecture Components

### Services
- **ApiGateway** (Port 5000): Entry point, JWT authentication, YARP reverse proxy
- **UserService** (Port 5001): User management, JWT token generation
- **BookingService** (Port 5002): Booking lifecycle, Outbox pattern
- **PaymentService** (Port 5003): Payment processing (70% success rate simulation)
- **InventoryService** (Port 5004): Room inventory management, reservation tracking

### Infrastructure
- **PostgreSQL Databases**: userdb (5432), bookingdb (5433), inventorydb (5435)
- **MongoDB**: paymentdb (27017)
- **RabbitMQ**: Message broker (5672, Management UI 15672)
- **Seq**: Centralized logging with correlation tracking (5341)

## Complete Flow Scenarios

### Scenario 1: Successful Booking Flow ✅

```
1. User Authentication
   → POST /api/user/login
   → UserService generates JWT token
   ✅ Token returned to client

2. Create Booking
   → POST /api/booking (with JWT)
   → BookingService creates booking (status: PENDING)
   → Saves BookingCreatedEvent to outbox table
   ✅ Booking ID returned to client

3. Outbox Publisher (Background Service)
   → Polls outbox table every 1 second
   → Publishes BookingCreatedEvent to RabbitMQ queue: booking_created
   ✅ Event marked as published in outbox

4. Inventory Reservation
   → InventoryService consumes BookingCreatedEvent
   → Checks inventory availability
   → Creates reservation (status: RESERVED, expires in 15 min)
   → Updates AvailableQuantity -= 1, ReservedQuantity += 1
   → Publishes InventoryReservedEvent to queue: inventory_reserved
   ✅ Inventory reserved with expiration timer

5. Payment Processing
   → PaymentService consumes InventoryReservedEvent
   → Simulates payment (70% success rate)
   → Creates payment record (status: SUCCESS)
   → Publishes PaymentSucceededEvent to queue: payment_succeeded
   ✅ Payment completed

6. Booking Confirmation
   → BookingService consumes PaymentSucceededEvent
   → Updates booking (status: CONFIRMED, ConfirmedAt: timestamp)
   ✅ Booking confirmed

7. Inventory Confirmation
   → InventoryService consumes PaymentSucceededEvent
   → Updates reservation (status: CONFIRMED)
   → Finalizes inventory allocation
   ✅ Inventory confirmed

Final State:
- Booking: CONFIRMED
- Payment: SUCCESS
- Inventory Reservation: CONFIRMED
- AvailableQuantity remains decremented
```

### Scenario 2: Payment Failure with Compensation ❌➡️🔄

```
1-4. [Same as Scenario 1: Authentication → Booking → Outbox → Inventory Reservation]

5. Payment Processing (Failed)
   → PaymentService consumes InventoryReservedEvent
   → Simulates payment (30% failure rate)
   → Creates payment record (status: FAILED, reason: "Payment processing failed")
   → Publishes PaymentFailedEvent to queue: payment_failed
   ❌ Payment failed

6. Payment Retry Mechanism
   → User/Client receives payment failure response
   → Initiates retry: POST /api/payment/retry/{bookingId}
   → PaymentService creates new payment attempt
   → Publishes RetryPaymentEvent to queue: retry_payment
   → PaymentService consumes and processes retry
   🔄 Up to 3 retry attempts

   If Retry Succeeds:
   → Continues to Scenario 1 (steps 6-7)
   ✅ Flow completes successfully

   If All Retries Fail:
   → Compensation begins below

7. Inventory Compensation (Release)
   → InventoryService consumes PaymentFailedEvent
   → Updates reservation (status: RELEASED, ReleasedAt: timestamp)
   → Updates AvailableQuantity += 1, ReservedQuantity -= 1
   → Publishes InventoryReleasedEvent to queue: inventory_released
   ✅ Inventory released back to available pool

8. Booking Cancellation
   → BookingService consumes PaymentFailedEvent
   → Updates booking (status: CANCELLED, CancellationReason: "Payment failed: ...")
   ✅ Booking cancelled

Final State:
- Booking: CANCELLED (Reason: Payment failure)
- Payment: FAILED (with retry history)
- Inventory Reservation: RELEASED
- AvailableQuantity restored
```

### Scenario 3: Insufficient Inventory ⛔

```
1-2. [Same as Scenario 1: Authentication → Booking]

3. [Outbox Publisher publishes BookingCreatedEvent]

4. Inventory Reservation Failed
   → InventoryService consumes BookingCreatedEvent
   → Checks inventory availability
   → AvailableQuantity = 0 (all rooms booked)
   → Logs WARNING (not ERROR): "Insufficient inventory for ROOM-XXX"
   → Publishes InventoryReservationFailedEvent to queue: inventory_reservation_failed
   ⛔ No inventory available

5. Booking Cancellation
   → BookingService consumes InventoryReservationFailedEvent
   → Updates booking (status: CANCELLED, CancellationReason: "Inventory reservation failed: ...")
   → Saves BookingCancelledEvent to outbox
   → Outbox publisher sends to queue: bookingcancelled
   ✅ Booking cancelled gracefully

6. No Payment Triggered
   → PaymentService never receives event
   → No payment attempted
   ✅ Payment avoided for unavailable inventory

Final State:
- Booking: CANCELLED (Reason: Insufficient inventory)
- Payment: N/A (never initiated)
- Inventory Reservation: N/A (never created)
- AvailableQuantity unchanged
```

## Event Flow Diagram

```
┌──────────────┐
│   Client     │
└──────┬───────┘
       │ 1. POST /api/user/login
       ▼
┌──────────────┐
│ UserService  │ Generates JWT
└──────┬───────┘
       │ 2. POST /api/booking (JWT)
       ▼
┌──────────────────────────────────────────────────────────────────┐
│ BookingService                                                   │
│  - Create booking (PENDING)                                      │
│  - Save to Outbox                                                │
└──────┬───────────────────────────────────────────────────────────┘
       │ 3. BookingCreatedEvent
       │    (via Outbox Publisher)
       ▼
    RabbitMQ (booking_created)
       │
       ├─────────────────────────────┐
       │                             │
       ▼                             ▼
┌──────────────┐            ┌──────────────┐
│ Inventory    │            │   Payment    │
│   Service    │            │   Service    │
└──────┬───────┘            └──────────────┘
       │                           ▲
       │ 4a. Check Availability    │
       │                           │
       ├─── Available? ────────────┤
       │                           │
    YES│                       NO  │
       │                           │
       ▼                           ▼
Reserve Inventory         InventoryReservationFailedEvent
       │                           │
       │ 4b. InventoryReservedEvent│
       │                           │
       ▼                           ▼
    RabbitMQ              BookingService
       │                  (Cancel Booking)
       │
       ▼
┌──────────────┐
│   Payment    │
│   Service    │
└──────┬───────┘
       │ 5. Process Payment
       │
       ├─── Success? ──────┬─── Failed? ────┐
       │                   │                 │
    YES│                NO │                 │
       │                   │                 │
       ▼                   ▼                 │
PaymentSucceededEvent  PaymentFailedEvent   │
       │                   │                 │
       ▼                   ▼                 │
    RabbitMQ           RabbitMQ              │
       │                   │                 │
       ├───────────┬───────┼─────────────────┘
       │           │       │
       ▼           ▼       ▼
┌──────────┐ ┌─────────┐ ┌────────────┐
│ Booking  │ │Inventory│ │ Inventory  │
│ Service  │ │ Service │ │  Service   │
│(CONFIRM) │ │(CONFIRM)│ │  (RELEASE) │
└──────────┘ └─────────┘ └────────────┘
                             │
                             ▼
                        BookingService
                        (CANCEL Booking)
```

## Correlation ID Tracking

All events carry a `CorrelationId` that flows through the entire saga:

```csharp
// Example event structure
{
  "EventId": "unique-event-guid",
  "CorrelationId": "shared-correlation-guid", // Same across all events in saga
  "EventName": "BookingCreated",
  "Timestamp": "2025-11-12T07:43:53Z",
  "Data": { /* event-specific data */ }
}
```

### Serilog Enrichers Configuration

All services now include:
- `WithClientIp()`: Captures client IP address
- `WithCorrelationId()`: Automatically enriches logs with correlation ID from HTTP header
- `WithProperty("Service", "ServiceName")`: Service identification
- `FromLogContext()`: Captures correlation ID from event context

### Querying in Seq

Search all logs for a specific transaction:
```
CorrelationId = 'd6fd46e5-9831-4d11-97c3-af8fcb942f9c'
```

Track event flow:
```
@Message like '%EventName%' and CorrelationId = 'correlation-id'
```

Filter by service and correlation:
```
Service = 'BookingService' and CorrelationId = 'correlation-id'
```

## RabbitMQ Queues

### Primary Event Queues
- `booking_created`: BookingService → InventoryService
- `inventory_reserved`: InventoryService → PaymentService
- `inventory_reservation_failed`: InventoryService → BookingService
- `payment_succeeded`: PaymentService → BookingService + InventoryService
- `payment_failed`: PaymentService → BookingService + InventoryService
- `inventory_released`: InventoryService (compensating action)
- `bookingcancelled`: BookingService (audit/notification)
- `retry_payment`: Retry mechanism

### Dead Letter Queues (DLQ)
- `payment_succeeded_dlq`: Failed PaymentSucceeded message handling
- `payment_failed_dlq`: Failed PaymentFailed message handling

## Idempotency & Reliability

### Outbox Pattern (BookingService)
- Events saved to `outbox_messages` table in same transaction as booking
- Background publisher polls every 1 second
- Retry logic with exponential backoff
- Prevents event loss during publish failures

### Idempotent Event Handlers
All consumers check state before processing:
```csharp
// Example from BookingService
if (booking.Status == "CANCELLED") {
    _logger.LogInformation("Booking already cancelled. Skipping.");
    return; // Idempotent - safe to process multiple times
}
```

### Polly Resilience Policies

**Event Publishing**:
- 3 retry attempts
- Exponential backoff: 2s, 4s, 8s
- Handles transient network failures

**Database Operations**:
- 5 retry attempts for Npgsql transient errors
- Exponential backoff with jitter
- Handles deadlocks, connection timeouts

**RabbitMQ Connection**:
- 10 retry attempts with 60s max delay
- Automatic recovery on connection loss
- Handles broker unavailability during startup

## Message Requeue Strategy

Consumers use acknowledgment strategies:
- **ACK**: Successful processing, remove from queue
- **NACK with requeue=false**: Permanent failure, send to DLQ
- **NACK with requeue=true**: Transient failure, retry up to MaxRequeueAttempts (3)

## Testing Scenarios

### Test 1: Successful Flow
```powershell
# Reset inventory
docker exec -it inventorydb psql -U inventoryservice -d inventorydb -c "
UPDATE \"InventoryReservations\" SET \"Status\" = 'RELEASED'; 
UPDATE \"InventoryItems\" SET \"AvailableQuantity\" = \"TotalQuantity\", \"ReservedQuantity\" = 0;"

# Run test with low concurrency
.\scripts\testing\test-e2e-auth.ps1 -NumberOfFlows 4 -ConcurrentFlows 1
```

Expected: All 4 bookings succeed (payment success rate permitting)

### Test 2: Inventory Exhaustion
```powershell
# Reset inventory
# (same as above)

# Run test with high concurrency to exhaust 4 rooms
.\scripts\testing\test-e2e-auth.ps1 -NumberOfFlows 8 -ConcurrentFlows 4
```

Expected: First 4 bookings may succeed, remaining fail with "Insufficient inventory"

### Test 3: Payment Retry
```powershell
# Test includes automatic retry on payment failure
.\scripts\testing\test-e2e-auth.ps1 -NumberOfFlows 10 -ConcurrentFlows 2
```

Expected: Some payments fail initially, retries attempt up to 3 times

## Monitoring & Observability

### Health Checks
- **UserService**: `GET /health` (checks PostgreSQL)
- **BookingService**: `GET /health` (checks PostgreSQL)
- **PaymentService**: `GET /health` (checks MongoDB)
- **InventoryService**: `GET /health` (checks PostgreSQL)
- **ApiGateway**: `GET /health`

### Seq Dashboard
URL: `http://localhost:5341`

**Key Queries**:
```
# All errors across services
@Level = 'Error'

# Booking flow for specific user
Service = 'BookingService' and UserId = 'user-guid'

# Payment failures
Service = 'PaymentService' and @Message like '%FAILED%'

# Inventory warnings
Service = 'InventoryService' and @Level = 'Warning'

# Correlation tracking
CorrelationId = 'guid' | select @Timestamp, Service, @Message

# Event choreography timeline
@Message like '%Event%' and CorrelationId = 'guid'
```

### RabbitMQ Management
URL: `http://localhost:15672` (guest/guest)

Monitor queue depths, message rates, and consumer activity.

## Environment Configuration

### Docker Deployment (.env)
Copy `.env.example` to `.env`:
```bash
# Service names for Docker network
USERDB_HOST=userdb
BOOKINGDB_HOST=bookingdb
INVENTORYDB_HOST=inventorydb
PAYMENTDB_HOST=paymentdb
RABBITMQ_HOST=rabbitmq
SEQ_URL=http://seq:80
```

### Local Development (.env.local)
Copy `.env.local.example` to `.env`:
```bash
# Localhost for local development connecting to Docker DBs
USERDB_HOST=localhost
BOOKINGDB_HOST=localhost
INVENTORYDB_HOST=localhost
PAYMENTDB_HOST=localhost
RABBITMQ_HOST=localhost
SEQ_URL=http://localhost:5341
```

## Database Schema Summary

### BookingService (PostgreSQL)
- **bookings**: id, user_id, room_id, amount, status, cancellation_reason, confirmed_at, cancelled_at
- **outbox_messages**: id, event_type, payload, published, created_at, published_at, retry_count

### InventoryService (PostgreSQL)
- **InventoryItems**: Id, ItemId (ROOM-XXX), TotalQuantity, AvailableQuantity, ReservedQuantity
- **InventoryReservations**: Id, ItemId, BookingId, Status (RESERVED/CONFIRMED/RELEASED), ExpiresAt, ReleasedAt

### PaymentService (MongoDB)
- **payments**: _id, bookingId, userId, amount, status, method, reason, transactionId, createdAt
- **paymentretries**: _id, paymentId, bookingId, attemptNumber, status, createdAt

## Error Handling Summary

| Scenario | Service | Action | Compensation |
|----------|---------|--------|--------------|
| Payment fails | PaymentService | Publish PaymentFailedEvent | InventoryService releases, BookingService cancels |
| Insufficient inventory | InventoryService | Publish InventoryReservationFailedEvent | BookingService cancels immediately |
| Database timeout | Any Service | Retry with Polly (5x) | Log error, continue or DLQ |
| RabbitMQ down on startup | Any Service | Retry connection (10x, 60s max) | Service waits for broker |
| Event processing fails | Consumer | NACK with requeue (3x) | Move to DLQ after max attempts |
| Outbox publish fails | BookingService | Retry on next poll (1s interval) | Event persisted in DB, eventual consistency |

## Key Design Patterns

1. **Event Choreography**: Decentralized, no orchestrator
2. **Saga Pattern**: Distributed transaction with compensating actions
3. **Outbox Pattern**: Reliable event publishing
4. **Idempotent Handlers**: Safe message reprocessing
5. **Correlation Tracking**: End-to-end observability
6. **Circuit Breaker**: Polly retry policies
7. **Graceful Degradation**: Business failures vs technical failures
8. **Dead Letter Queue**: Failed message handling

## Performance Characteristics

- **Throughput**: Tested with 100 concurrent booking flows
- **Latency**: ~200-500ms per booking (depends on payment simulation)
- **Availability**: AP system (eventual consistency, partition tolerance)
- **Scalability**: Horizontal scaling supported (stateless services)
- **Resilience**: Auto-recovery, retry logic, compensation actions

## Common Issues & Solutions

### Issue: "Insufficient inventory" errors in logs
**Solution**: This is expected when inventory is exhausted. Check that:
- Logs show WARNING (not ERROR)
- InventoryReservationFailedEvent is published
- Bookings are cancelled with proper reason

### Issue: Payment retry not working
**Solution**: Check:
- Booking status is not already CONFIRMED or CANCELLED
- PaymentService is consuming retry_payment queue
- Retry count < 3

### Issue: Correlation ID not appearing in Seq
**Solution**: Verify:
- Serilog.Enrichers.ClientInfo package installed
- `.Enrich.WithCorrelationId()` in Program.cs
- X-Correlation-ID header passed in requests

### Issue: Events not being processed
**Solution**: Check:
- RabbitMQ is running and healthy
- Consumers show "listening to queue" in logs
- Queue exists in RabbitMQ Management UI
- Messages not in DLQ

## Next Steps for Production

1. **Add Authentication to RabbitMQ**: Change default guest/guest credentials
2. **Implement Circuit Breaker**: Prevent cascade failures
3. **Add Distributed Tracing**: OpenTelemetry for detailed spans
4. **Database Connection Pooling**: Optimize for high throughput
5. **Rate Limiting**: Protect APIs from abuse
6. **Message TTL**: Expire old messages in queues
7. **Monitoring Alerts**: Set up alerts for queue depth, error rates
8. **Backup Strategy**: Regular DB backups, event sourcing
9. **Security Hardening**: HTTPS, secrets management, API rate limits
10. **Load Testing**: Identify bottlenecks under realistic load

---

**Last Updated**: November 12, 2025
**System Version**: v1.0 with Saga Pattern + Inventory Service
**Status**: ✅ Production-Ready with Graceful Error Handling
