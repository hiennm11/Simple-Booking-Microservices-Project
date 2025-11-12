# Dead Letter Queue Handler Implementation ✅

## Overview
This document describes the implementation of the Dead Letter Queue (DLQ) Handler functionality that captures failed messages after maximum retry attempts, stores them in the database for manual investigation, and provides a foundation for message recovery and monitoring.

## Purpose
When messages fail processing after multiple retry attempts (due to bugs, data issues, or transient failures that persist), they need to be:
1. Removed from the main processing queue to prevent blocking other messages
2. Captured and stored for investigation
3. Made available for manual review and potential reprocessing
4. Tracked with full error context and metadata

## Implementation Summary

### 1. Dead Letter Message Model ✅

**File:** `src/BookingService/Models/DeadLetterMessage.cs`

A comprehensive entity that stores all information about failed messages:

```csharp
public class DeadLetterMessage : BaseEntity
{
    public string SourceQueue { get; set; }      // Original queue name
    public string EventType { get; set; }         // Type of event (PaymentFailed, etc.)
    public string Payload { get; set; }           // Complete message payload as JSON
    public string ErrorMessage { get; set; }      // Error from last attempt
    public string? StackTrace { get; set; }       // Stack trace if available
    public int AttemptCount { get; set; }         // Number of processing attempts
    public DateTime FirstAttemptAt { get; set; }  // When first tried
    public DateTime FailedAt { get; set; }        // When finally failed
    public bool Resolved { get; set; }            // Manual resolution flag
    public DateTime? ResolvedAt { get; set; }     // When resolved
    public string? ResolutionNotes { get; set; }  // Resolution details
    public string? ResolvedBy { get; set; }       // Who resolved it
}
```

### 2. Dead Letter Queue Handler Service ✅

**File:** `src/BookingService/Consumers/DeadLetterQueueHandler.cs`

A background service that:
1. Connects to RabbitMQ with retry logic
2. Listens to multiple DLQ queues:
   - `payment_failed_dlq`
   - `payment_succeeded_dlq`
3. Consumes failed messages
4. Extracts metadata from message headers
5. Stores messages in PostgreSQL database
6. Acknowledges messages after storage
7. Logs all operations for monitoring

#### Key Features:

**Connection Resilience:**
- 10 retry attempts with exponential backoff
- Automatic recovery on connection loss
- Graceful degradation under failures

**Metadata Extraction:**
- `x-retry-count`: Number of processing attempts
- `x-first-attempt`: Original attempt timestamp
- `x-error-message`: Error description
- `x-stack-trace`: Exception stack trace
- `x-original-queue`: Source queue name
- `x-failed-at`: Final failure timestamp

**Database Storage:**
- Stores complete message context
- Maintains processing history
- Enables investigation and recovery
- Tracks resolution status

### 3. Enhanced PaymentFailedConsumer ✅

**File:** `src/BookingService/Consumers/PaymentFailedConsumer.cs`

Updated to properly route failed messages to DLQ:

**Changes:**
1. **Per-Message Retry Tracking:** Uses dictionaries to track retry count and first attempt time per message
2. **DLQ Routing:** Sends messages to DLQ after 3 failed attempts
3. **Metadata Propagation:** Adds comprehensive headers when sending to DLQ
4. **Cleanup:** Removes tracking data after successful processing or DLQ routing

**Key Methods:**
```csharp
// Sends message to DLQ with metadata
private async Task SendToDeadLetterQueueAsync(
    BasicDeliverEventArgs originalEvent,
    string message,
    string errorMessage,
    string? stackTrace)

// Cleans up tracking dictionaries
private void CleanupMessageTracking(ulong deliveryTag)
```

### 4. Database Changes ✅

**Updated:** `src/BookingService/Data/BookingDbContext.cs`

Added `DeadLetterMessages` DbSet and entity configuration with:
- Complete column mappings
- Indexes for efficient querying:
  - Event type index
  - Source queue index
  - Failed timestamp index
  - Partial index for unresolved messages (performance optimization)

**Migration:** `src/BookingService/Migrations/20251111000000_AddDeadLetterMessages.cs`

Creates `dead_letter_messages` table with all necessary columns and indexes.

### 5. Configuration Updates ✅

**File:** `src/BookingService/appsettings.json`

Added DLQ configuration:
```json
"RabbitMQ": {
  "Queues": {
    "BookingCreated": "booking_created",
    "PaymentSucceeded": "payment_succeeded",
    "PaymentFailed": "payment_failed"
  },
  "DeadLetterQueues": {
    "PaymentFailed": "payment_failed_dlq",
    "PaymentSucceeded": "payment_succeeded_dlq"
  }
}
```

### 6. Service Registration ✅

**File:** `src/BookingService/Program.cs`

Registered `DeadLetterQueueHandler` as a hosted service:
```csharp
builder.Services.AddHostedService<DeadLetterQueueHandler>();
```

## Architecture Flow

### Normal Message Processing
```
1. Consumer receives message from queue
2. Processes message successfully
3. Acknowledges message (ACK)
4. Message removed from queue
```

### Failed Message Flow (NEW)
```
1. Consumer receives message from queue
2. Processing fails with exception
3. Increment retry count for this message
4. If retry count < 3:
   - NACK with requeue=true
   - Message goes back to queue
5. If retry count >= 3:
   - Create message with metadata headers
   - Publish to DLQ queue
   - ACK original message (remove from main queue)
6. DeadLetterQueueHandler receives from DLQ
7. Stores in database with full context
8. ACK DLQ message (remove from DLQ)
9. Message available for manual investigation
```

## Benefits

### 1. Prevents Queue Blocking
- Failed messages don't block processing of good messages
- Queue remains healthy and operational
- No poison pill scenario

### 2. Comprehensive Debugging
- Full message payload preserved
- Complete error context with stack traces
- Attempt history and timestamps
- Easy to investigate root causes

### 3. Recovery Options
- Messages can be manually reprocessed
- Data can be fixed and message replayed
- Patterns can be identified for automated fixes

### 4. Monitoring & Alerting
- Database queries for failure rates
- Alert on high DLQ volumes
- Track resolution SLAs
- Identify systemic issues

### 5. Audit Trail
- Complete history of failures
- Resolution tracking
- Compliance and accountability

## Usage Examples

### Query Unresolved DLQ Messages
```sql
SELECT 
    id,
    event_type,
    source_queue,
    error_message,
    attempt_count,
    failed_at
FROM dead_letter_messages
WHERE resolved = false
ORDER BY failed_at DESC;
```

### Query Failure Patterns
```sql
SELECT 
    event_type,
    source_queue,
    COUNT(*) as failure_count,
    MAX(failed_at) as last_failure
FROM dead_letter_messages
WHERE failed_at > NOW() - INTERVAL '24 hours'
GROUP BY event_type, source_queue
ORDER BY failure_count DESC;
```

### Mark Message as Resolved
```sql
UPDATE dead_letter_messages
SET 
    resolved = true,
    resolved_at = NOW(),
    resolved_by = 'admin@example.com',
    resolution_notes = 'Data corrected and message reprocessed manually'
WHERE id = 'message-guid-here';
```

### View Message Details for Investigation
```sql
SELECT 
    id,
    event_type,
    payload::json,
    error_message,
    stack_trace,
    attempt_count,
    first_attempt_at,
    failed_at
FROM dead_letter_messages
WHERE id = 'message-guid-here';
```

## Testing Instructions

### 1. Trigger Message Failures

Temporarily introduce a bug in `PaymentFailedConsumer`:

```csharp
private async Task ProcessPaymentFailedAsync(PaymentFailedEvent paymentEvent)
{
    // Force failure for testing
    throw new Exception("Test DLQ: Simulated processing failure");
    
    // ... rest of the code
}
```

### 2. Create Bookings and Process Payments

```bash
POST http://localhost:5000/booking/api/bookings
# Create several bookings
# Process payments until one fails
```

### 3. Observe Message Processing

**In Seq Logs (http://localhost:5341):**
- Search for "Requeuing message" (attempts 1-2)
- Search for "Moving to DLQ" (after 3 attempts)
- Search for "Received message from DLQ"
- Search for "Dead letter message stored in database"

**In RabbitMQ Management (http://localhost:15672):**
- Check `payment_failed` queue - message should disappear
- Check `payment_failed_dlq` queue - message should appear then disappear
- Monitor message rates and counts

### 4. Query the Database

```sql
-- Check DLQ messages
SELECT * FROM dead_letter_messages ORDER BY failed_at DESC;

-- View the payload
SELECT payload::json FROM dead_letter_messages WHERE id = 'guid';
```

### 5. Remove Test Code

Don't forget to remove the test exception after verification!

## Monitoring and Observability

### Key Metrics to Track

1. **DLQ Message Rate:**
   - Messages per hour/day
   - Alert if rate exceeds threshold

2. **Resolution Time:**
   - Time from `failed_at` to `resolved_at`
   - Track SLA compliance

3. **Failure Patterns:**
   - Most common error messages
   - Most problematic event types
   - Time patterns (certain times of day)

4. **Unresolved Count:**
   - Total unresolved messages
   - Age of oldest unresolved message

### Seq Queries

**DLQ Messages Stored:**
```
@Message = "Dead letter message stored in database"
| select EventType, SourceQueue
| group by EventType, SourceQueue
```

**DLQ Errors:**
```
@Message like "%DeadLetterQueueHandler%"
and @Level = "Error"
```

## Future Enhancements

### 1. Admin API for DLQ Management
```csharp
[HttpGet("api/dlq")]
public async Task<ActionResult<List<DeadLetterMessage>>> GetUnresolved()
{
    // Query unresolved messages
}

[HttpPost("api/dlq/{id}/resolve")]
public async Task<ActionResult> ResolveMessage(Guid id, [FromBody] ResolutionRequest request)
{
    // Mark message as resolved
}

[HttpPost("api/dlq/{id}/retry")]
public async Task<ActionResult> RetryMessage(Guid id)
{
    // Republish message to original queue
}
```

### 2. Automatic Resolution
- Detect patterns in failures
- Apply fixes automatically
- Retry with corrected data

### 3. Dashboard
- Real-time DLQ metrics
- Failure trend graphs
- Alert configuration UI

### 4. DLQ for PaymentService
- Implement similar handler in PaymentService
- Unified DLQ monitoring across services

### 5. Message Replay System
- Bulk replay of corrected messages
- Selective replay with filters
- Replay simulation (dry run)

## Files Created/Modified

### New Files:
- ✅ `src/BookingService/Models/DeadLetterMessage.cs`
- ✅ `src/BookingService/Consumers/DeadLetterQueueHandler.cs`
- ✅ `src/BookingService/Migrations/20251111000000_AddDeadLetterMessages.cs`
- ✅ `docs/phase3-event-integration/DLQ_HANDLER_IMPLEMENTATION.md`

### Modified Files:
- ✅ `src/BookingService/Consumers/PaymentFailedConsumer.cs` - Enhanced with DLQ routing
- ✅ `src/BookingService/Data/BookingDbContext.cs` - Added DeadLetterMessages entity
- ✅ `src/BookingService/Migrations/BookingDbContextModelSnapshot.cs` - Updated model
- ✅ `src/BookingService/appsettings.json` - Added DLQ configuration
- ✅ `src/BookingService/Program.cs` - Registered DLQ handler service

## Success Criteria ✅

- ✅ DeadLetterMessage model created with comprehensive fields
- ✅ DeadLetterQueueHandler service implemented
- ✅ Service consumes from multiple DLQ queues
- ✅ Messages stored in PostgreSQL with metadata
- ✅ PaymentFailedConsumer routes to DLQ after max retries
- ✅ Message metadata propagated through headers
- ✅ Database migration created
- ✅ Configuration updated
- ✅ Service registered in Program.cs
- ✅ Build succeeds without errors
- ✅ Logging and monitoring in place

---

**Implementation Status:** Complete and Ready for Testing! 🎉

**Next Steps:**
1. Deploy the changes
2. Run the database migration
3. Test with simulated failures
4. Monitor DLQ metrics in production
5. Consider implementing admin API for DLQ management
