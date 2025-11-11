# 🔗 Correlation ID Tracking

**Category**: Communication Patterns  
**Difficulty**: Intermediate  
**Focus**: Distributed request tracing across microservices

---

## 📖 What is a Correlation ID?

A **Correlation ID** is a unique identifier that tracks a single request/transaction as it flows through multiple microservices. It's like a **tracking number** for your request.

```
User Request → Correlation ID: abc-123
   ↓
BookingService (abc-123)
   ↓
RabbitMQ (abc-123)
   ↓
PaymentService (abc-123)
   ↓
BookingService (abc-123)

All logs tagged with abc-123 = Complete request history
```

---

## 🎯 Why Correlation IDs?

### Without Correlation ID

```
BookingService logs:
[INFO] Created booking booking-456
[INFO] Published event

PaymentService logs:
[INFO] Processing payment
[INFO] Payment succeeded

BookingService logs:
[INFO] Updated booking status

❓ Problem: How do we know these logs are related?
❓ Which booking triggered which payment?
❓ How long did the entire workflow take?
```

### With Correlation ID

```
BookingService logs:
[INFO] [CorrelationId: abc-123] Created booking booking-456
[INFO] [CorrelationId: abc-123] Published BookingCreatedEvent

PaymentService logs:
[INFO] [CorrelationId: abc-123] Processing payment for booking booking-456
[INFO] [CorrelationId: abc-123] Payment succeeded

BookingService logs:
[INFO] [CorrelationId: abc-123] Updated booking booking-456 status to CONFIRMED

✅ Solution: Search "abc-123" in Seq → See complete workflow!
```

---

## 🏗️ Implementation in Your Project

### 1. Generate Correlation ID

**API Gateway** (Entry Point):

```csharp
app.Use(async (context, next) =>
{
    // Generate correlation ID if not provided by client
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                       ?? Guid.NewGuid().ToString();
    
    // Store in HttpContext for downstream use
    context.Items["CorrelationId"] = correlationId;
    
    // Add to response headers (for client)
    context.Response.Headers.Add("X-Correlation-ID", correlationId);
    
    // Add to log context (Serilog)
    using (LogContext.PushProperty("CorrelationId", correlationId))
    {
        await next();
    }
});
```

**Result**: Every request gets a correlation ID, automatically logged.

### 2. Forward to Downstream Services

**BookingService Endpoint**:

```csharp
[HttpPost]
public async Task<IActionResult> CreateBooking(CreateBookingRequest request)
{
    // Extract correlation ID from HttpContext
    var correlationId = HttpContext.Items["CorrelationId"]?.ToString()
                       ?? Guid.NewGuid().ToString();
    
    // Add to log context
    using (LogContext.PushProperty("CorrelationId", correlationId))
    {
        _logger.LogInformation("Creating booking for user {UserId}", request.UserId);
        
        var booking = await _bookingService.CreateAsync(request, correlationId);
        
        return CreatedAtAction(nameof(GetBooking), new { id = booking.Id }, booking);
    }
}
```

### 3. Include in Events

**BookingCreatedEvent**:

```csharp
public class BookingCreatedEvent : IntegrationEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public Guid CorrelationId { get; set; } // ← Correlation ID
    public Guid BookingId { get; set; }
    public string UserId { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

**Publishing Event**:

```csharp
public async Task CreateBookingAsync(CreateBookingRequest request, string correlationId)
{
    var booking = new Booking { /* ... */ };
    await _dbContext.Bookings.AddAsync(booking);
    await _dbContext.SaveChangesAsync();
    
    // Publish event with correlation ID
    await _eventBus.PublishAsync(new BookingCreatedEvent
    {
        CorrelationId = Guid.Parse(correlationId),
        BookingId = booking.Id,
        UserId = booking.UserId,
        Amount = booking.Amount
    });
}
```

### 4. Consume Event with Correlation ID

**PaymentService Consumer**:

```csharp
public class BookingCreatedEventHandler : IEventHandler<BookingCreatedEvent>
{
    public async Task HandleAsync(BookingCreatedEvent @event)
    {
        // Add correlation ID to log context
        using (LogContext.PushProperty("CorrelationId", @event.CorrelationId))
        {
            _logger.LogInformation(
                "Processing payment for booking {BookingId}",
                @event.BookingId
            );
            
            var payment = await _paymentService.ProcessAsync(@event);
            
            // Publish next event with same correlation ID
            await _eventBus.PublishAsync(new PaymentSucceededEvent
            {
                CorrelationId = @event.CorrelationId, // ← Preserve correlation ID
                BookingId = @event.BookingId,
                PaymentId = payment.Id
            });
        }
    }
}
```

---

## 🔍 Tracing with Seq

### Query by Correlation ID

**Seq Query**:

```
CorrelationId = "abc-123"
```

**Results**:

```
10:00:00.000  [INF] [abc-123] [API Gateway] Incoming request: POST /api/bookings
10:00:00.050  [INF] [abc-123] [BookingService] Creating booking for user user-123
10:00:00.100  [INF] [abc-123] [BookingService] Booking booking-456 created
10:00:00.120  [INF] [abc-123] [BookingService] Published BookingCreatedEvent
10:00:00.150  [INF] [abc-123] [PaymentService] Received BookingCreatedEvent
10:00:30.000  [INF] [abc-123] [PaymentService] Payment payment-789 succeeded
10:00:30.020  [INF] [abc-123] [PaymentService] Published PaymentSucceededEvent
10:00:30.050  [INF] [abc-123] [BookingService] Received PaymentSucceededEvent
10:00:30.100  [INF] [abc-123] [BookingService] Booking booking-456 confirmed

Total Time: 30.1 seconds
Services Involved: API Gateway, BookingService, PaymentService
```

### Visualize Request Flow

**Seq Dashboard Query**:

```
CorrelationId = "abc-123"
| order by @Timestamp
| select @Timestamp, Service, @Message
| chart timeline
```

**Timeline Visualization**:

```
API Gateway     ●────────────────────────────────────────────────────
BookingService      ●───●               ●────●
PaymentService              ●──────────────────────────●

                ↑       ↑           ↑               ↑
             Request  Booking    Payment        Confirmed
                      Created   Processing
```

---

## 🛡️ Best Practices

### 1. Generate at Entry Point

```csharp
// ✅ Good: Generate at API Gateway
app.Use(async (context, next) =>
{
    var correlationId = Guid.NewGuid().ToString();
    context.Items["CorrelationId"] = correlationId;
    await next();
});

// ❌ Bad: Generate in every service (multiple IDs!)
public async Task CreateBooking()
{
    var correlationId = Guid.NewGuid().ToString(); // New ID, loses traceability!
}
```

### 2. Preserve Across Events

```csharp
// ✅ Good: Pass correlation ID to next event
await _eventBus.PublishAsync(new PaymentSucceededEvent
{
    CorrelationId = @event.CorrelationId, // ← Same ID
    // ...
});

// ❌ Bad: Generate new ID
await _eventBus.PublishAsync(new PaymentSucceededEvent
{
    CorrelationId = Guid.NewGuid(), // ← New ID, breaks tracing!
    // ...
});
```

### 3. Include in All Logs

```csharp
// ✅ Good: Correlation ID in log context
using (LogContext.PushProperty("CorrelationId", correlationId))
{
    _logger.LogInformation("Processing request");
    // All logs within this scope include correlation ID
}

// ❌ Bad: Manual correlation ID in every log
_logger.LogInformation("Processing request, CorrelationId: {Id}", correlationId);
_logger.LogInformation("Saved to database, CorrelationId: {Id}", correlationId);
// Tedious and error-prone
```

### 4. Return to Client

```csharp
// ✅ Good: Return correlation ID in response headers
context.Response.Headers.Add("X-Correlation-ID", correlationId);

// Client can use this for support tickets:
// "My request failed, correlation ID: abc-123"
```

### 5. Use Meaningful Format

```csharp
// ✅ Good: GUID (unique, collision-free)
var correlationId = Guid.NewGuid().ToString();
// "3fa85f64-5717-4562-b3fc-2c963f66afa6"

// ✅ Good: Timestamp + Random (sortable)
var correlationId = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
// "20251107120000-3fa85f645717456"

// ❌ Bad: Sequential integer (not unique across services)
var correlationId = Interlocked.Increment(ref _counter);
```

---

## 📊 Real-World Use Cases

### 1. Debugging Failed Request

**User reports**: "My booking failed!"

**Steps**:
1. Get correlation ID from response headers
2. Query Seq: `CorrelationId = "abc-123"`
3. See complete timeline:
   - Booking created ✅
   - Payment failed ❌ (credit card declined)
   - Booking cancelled ✅
4. Root cause identified in seconds!

### 2. Performance Analysis

**Query** slow requests:

```
CorrelationId = *
| group by CorrelationId
| select 
    CorrelationId,
    min(@Timestamp) as StartTime,
    max(@Timestamp) as EndTime,
    EndTime - StartTime as Duration
| where Duration > 5000ms
| order by Duration desc
```

**Result**:

```
CorrelationId     Duration
abc-123           30.5s    ← Payment processing slow
def-456           15.2s
ghi-789           10.8s
```

### 3. Error Rate by Flow

**Query** failure rate per correlation ID:

```
@Level = "Error"
| group by CorrelationId
| select CorrelationId, count() as ErrorCount
| order by ErrorCount desc
```

---

## 🎯 Advanced Patterns

### Distributed Tracing (OpenTelemetry)

For more advanced tracing, use OpenTelemetry:

```csharp
// Add OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation();
        tracing.AddHttpClientInstrumentation();
        tracing.AddJaegerExporter();
    });

// Automatic trace propagation across services
// Visualize in Jaeger UI
```

**Benefits over simple correlation ID**:
- Parent-child span relationships
- Timing breakdown per service
- Automatic instrumentation
- Industry-standard (W3C Trace Context)

---

## 🎓 Key Takeaways

1. **Correlation ID = Request Tracking Number**
   - Unique identifier for entire request flow
   - Follows request across all services

2. **Generate at Entry Point**
   - API Gateway creates correlation ID
   - All downstream services use same ID

3. **Include in Events**
   - Pass correlation ID in event payload
   - Preserve across entire workflow

4. **Use Seq for Tracing**
   - Query by correlation ID
   - See complete request timeline
   - Debug issues quickly

5. **Return to Client**
   - Include in response headers
   - Client can reference for support

---

## 📚 Further Reading

- **Structured Logging**: [Structured Logging](../05-observability/structured-logging.md)
- **Distributed Tracing**: [Monitoring Metrics](../05-observability/monitoring-metrics.md)
- **Project Docs**: `/docs/phase5-observability/`

### External Resources

- [Martin Fowler - Distributed Tracing](https://martinfowler.com/articles/patterns-of-distributed-systems/correlation-id.html)
- [OpenTelemetry Documentation](https://opentelemetry.io/docs/)
- [W3C Trace Context](https://www.w3.org/TR/trace-context/)

---

## ❓ Interview Questions

1. What is a correlation ID and why is it important?
2. How do you implement correlation ID tracking across microservices?
3. How do you preserve correlation IDs across async events?
4. How do you use correlation IDs for debugging?
5. What's the difference between correlation ID and trace ID?
6. How do you handle correlation IDs in load balancers?

---

**Last Updated**: November 11, 2025  
**Status**: ✅ Implemented in project  
**Pattern**: Distributed request tracing
