# Correlation ID Tracking Implementation

**Date**: November 12, 2025  
**Status**: ✅ **Fully Implemented**  
**Branch**: enhancement/payment-event

---

## 📋 Overview

Successfully implemented **Correlation ID Tracking** across the entire microservices system to enable distributed request tracing. Every request can now be tracked from the API Gateway through all services and events, making debugging and monitoring significantly easier.

---

## 🎯 What Was Implemented

### 1. **Shared Infrastructure Components**

#### **CorrelationIdMiddleware** (`src/Shared/Middleware/CorrelationIdMiddleware.cs`)
- Extracts correlation ID from `X-Correlation-ID` header or generates a new one
- Stores correlation ID in `HttpContext.Items` for downstream access
- Adds correlation ID to response headers for client tracking
- Pushes correlation ID to Serilog `LogContext` for automatic logging

**Key Features**:
```csharp
// Extracts or generates correlation ID
var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() 
                   ?? Guid.NewGuid().ToString();

// Adds to log context (all logs automatically include it)
using (LogContext.PushProperty("CorrelationId", correlationId))
{
    await _next(context);
}

// Returns to client in response headers
context.Response.Headers.Append("X-Correlation-ID", correlationId);
```

#### **CorrelationIdAccessor** (`src/Shared/Services/CorrelationIdAccessor.cs`)
- Service to access correlation ID from anywhere in the application
- Provides both string and Guid formats
- Uses `IHttpContextAccessor` under the hood

**Usage**:
```csharp
var correlationId = _correlationIdAccessor.GetCorrelationIdAsGuid();
```

### 2. **Event Contracts Updated**

All event contracts now include `CorrelationId` property:

- ✅ `BookingCreatedEvent`
- ✅ `PaymentSucceededEvent`
- ✅ `PaymentFailedEvent`

**Example**:
```csharp
public class BookingCreatedEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public Guid CorrelationId { get; set; } // ← NEW!
    public string EventName { get; set; } = "BookingCreated";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public BookingCreatedData Data { get; set; } = null!;
}
```

### 3. **Service Implementations Updated**

#### **API Gateway** (`src/ApiGateway/Program.cs`)
- Correlation ID middleware registered **first** in pipeline
- Generates correlation ID for all incoming requests
- Entry point for correlation tracking

```csharp
// Use Correlation ID middleware (MUST be first)
app.UseCorrelationId();
```

#### **BookingService** (`src/BookingService/`)
- **Program.cs**: Middleware registered
- **BookingServiceImpl.cs**: Extracts correlation ID and includes in events
- **PaymentSucceededConsumer.cs**: Extracts correlation ID from events and adds to log context
- **PaymentFailedConsumer.cs**: Extracts correlation ID from events and adds to log context

**Event Publishing**:
```csharp
var correlationId = _correlationIdAccessor.GetCorrelationIdAsGuid();

var bookingCreatedEvent = new BookingCreatedEvent
{
    EventId = Guid.NewGuid(),
    CorrelationId = correlationId, // ← Preserved across services
    // ...
};
```

**Event Consumption**:
```csharp
using (LogContext.PushProperty("CorrelationId", paymentEvent.CorrelationId))
{
    _logger.LogInformation("Processing PaymentSucceeded event...");
    // All logs here include correlation ID automatically
}
```

#### **PaymentService** (`src/PaymentService/`)
- **Program.cs**: Middleware registered
- **PaymentServiceImpl.cs**: Extracts correlation ID and includes in events
- **BookingCreatedConsumer.cs**: Extracts correlation ID from events and propagates to payment processing
- **ProcessPaymentRequest.cs**: Added `CorrelationId` property

**Payment Processing with Correlation**:
```csharp
var correlationId = request.CorrelationId != Guid.Empty 
    ? request.CorrelationId 
    : _correlationIdAccessor.GetCorrelationIdAsGuid();

using (LogContext.PushProperty("CorrelationId", correlationId))
{
    // Process payment...
    
    var paymentEvent = new PaymentSucceededEvent
    {
        CorrelationId = correlationId, // ← Preserved
        // ...
    };
}
```

### 4. **Dependencies Added**

Updated `src/Shared/Shared.csproj`:
```xml
<PackageReference Include="Serilog" Version="4.2.0" />
<PackageReference Include="Serilog.Extensions.Logging" Version="8.0.0" />
<FrameworkReference Include="Microsoft.AspNetCore.App" />
```

### 5. **Test Fixes**

Updated `test/PaymentService.Tests/Services/PaymentServiceImplTests.cs`:
- Added mock for `ICorrelationIdAccessor`
- Added mock for `IConfiguration`
- All tests now compile and run successfully

---

## 🔄 Request Flow with Correlation ID

### Example: Create Booking Flow

```
1. Client → API Gateway
   POST /api/bookings
   ↓
   Gateway generates: CorrelationId = "abc-123"
   Gateway logs: [CorrelationId: abc-123] Incoming request

2. API Gateway → BookingService
   POST /api/bookings
   Header: X-Correlation-ID: abc-123
   ↓
   BookingService extracts: CorrelationId = "abc-123"
   BookingService logs: [CorrelationId: abc-123] Creating booking

3. BookingService → RabbitMQ
   Publishes: BookingCreatedEvent { CorrelationId = "abc-123" }
   ↓
   BookingService logs: [CorrelationId: abc-123] Event saved to outbox

4. RabbitMQ → PaymentService
   BookingCreatedEvent { CorrelationId = "abc-123" }
   ↓
   PaymentService logs: [CorrelationId: abc-123] Processing payment

5. PaymentService → RabbitMQ
   Publishes: PaymentSucceededEvent { CorrelationId = "abc-123" }
   ↓
   PaymentService logs: [CorrelationId: abc-123] Payment succeeded

6. RabbitMQ → BookingService
   PaymentSucceededEvent { CorrelationId = "abc-123" }
   ↓
   BookingService logs: [CorrelationId: abc-123] Booking confirmed

7. API Gateway → Client
   Response Headers: X-Correlation-ID: abc-123
   Body: { id: "booking-456", status: "CONFIRMED" }
```

**All logs include the same correlation ID: `abc-123`**

---

## 🔍 Debugging with Seq

### Query by Correlation ID

In Seq dashboard:
```
CorrelationId = "abc-123"
```

**Results show complete timeline**:
```
10:00:00.000 [INF] [abc-123] [API Gateway] Incoming request: POST /api/bookings
10:00:00.050 [INF] [abc-123] [BookingService] Creating booking for user user-123
10:00:00.100 [INF] [abc-123] [BookingService] Booking booking-456 created
10:00:00.120 [INF] [abc-123] [BookingService] Event saved to outbox
10:00:00.150 [INF] [abc-123] [PaymentService] Processing payment for booking-456
10:00:30.000 [INF] [abc-123] [PaymentService] Payment payment-789 succeeded
10:00:30.050 [INF] [abc-123] [BookingService] Booking booking-456 confirmed
```

### Performance Analysis

```sql
-- Find slow requests
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

### Error Tracking

```sql
-- Find errors by correlation ID
@Level = "Error"
| group by CorrelationId
| select CorrelationId, count() as ErrorCount, first(@Message)
| order by ErrorCount desc
```

---

## ✅ Benefits Achieved

1. **End-to-End Traceability**
   - Track a single request across all microservices
   - See complete request timeline in one query

2. **Faster Debugging**
   - Client reports issue with correlation ID
   - Query Seq to see exact failure point
   - Root cause identified in seconds

3. **Performance Monitoring**
   - Measure total request duration
   - Identify bottlenecks per service
   - Track service-to-service latency

4. **Production Support**
   - Correlation ID returned to client
   - Support tickets include correlation ID
   - No need to search through millions of logs

5. **Automatic Logging**
   - All logs automatically tagged
   - No manual correlation ID passing needed
   - Consistent across all services

---

## 🧪 Testing Correlation ID Flow

### Manual Test

```powershell
# 1. Start services
docker-compose up -d

# 2. Create a booking with custom correlation ID
$correlationId = [guid]::NewGuid()
$response = Invoke-WebRequest `
    -Uri "http://localhost:5000/api/bookings" `
    -Method POST `
    -Headers @{ 
        "X-Correlation-ID" = $correlationId 
        "Authorization" = "Bearer $token"
    } `
    -Body (@{
        userId = "user-123"
        roomId = "room-456"
        amount = 500000
    } | ConvertTo-Json) `
    -ContentType "application/json"

# 3. Check response headers
$response.Headers["X-Correlation-ID"]

# 4. Query Seq
# http://localhost:5341
# Search: CorrelationId = "$correlationId"
```

### Verify in Seq

1. Open Seq: `http://localhost:5341`
2. Query: `CorrelationId = "your-guid-here"`
3. Verify logs from:
   - API Gateway
   - BookingService
   - PaymentService
   - BookingService (final confirmation)

---

## 📁 Files Changed

### Created
- `src/Shared/Middleware/CorrelationIdMiddleware.cs`
- `src/Shared/Services/CorrelationIdAccessor.cs`

### Modified
- `src/Shared/Shared.csproj` (added Serilog packages)
- `src/Shared/Contracts/BookingCreatedEvent.cs`
- `src/Shared/Contracts/PaymentSucceededEvent.cs`
- `src/Shared/Contracts/PaymentFailedEvent.cs`
- `src/ApiGateway/Program.cs`
- `src/BookingService/Program.cs`
- `src/BookingService/Services/BookingServiceImpl.cs`
- `src/BookingService/Consumers/PaymentSucceededConsumer.cs`
- `src/BookingService/Consumers/PaymentFailedConsumer.cs`
- `src/PaymentService/Program.cs`
- `src/PaymentService/DTOs/ProcessPaymentRequest.cs`
- `src/PaymentService/Services/PaymentServiceImpl.cs`
- `src/PaymentService/Consumers/BookingCreatedConsumer.cs`
- `test/PaymentService.Tests/Services/PaymentServiceImplTests.cs`

---

## 🎓 Key Concepts Implemented

### 1. **Correlation ID Generation**
- Generated at API Gateway (entry point)
- UUID/GUID format for global uniqueness
- Extracted from header if provided by client

### 2. **Context Propagation**
- HTTP: Via `X-Correlation-ID` header
- Events: Via `CorrelationId` property
- Logs: Via Serilog `LogContext`

### 3. **Preservation Across Boundaries**
- Synchronous: HTTP headers
- Asynchronous: Event payload
- Consistent throughout entire flow

### 4. **Automatic Logging Integration**
- Serilog `LogContext.PushProperty`
- No manual correlation ID in log messages
- Structured logging with correlation ID field

---

## 🚀 Next Steps

### Recommended Enhancements

1. **OpenTelemetry Integration**
   - Upgrade to W3C Trace Context standard
   - Add distributed tracing with spans
   - Visualize in Jaeger or Zipkin

2. **Correlation ID Validation**
   - Validate format of incoming correlation IDs
   - Reject invalid formats to prevent issues

3. **Metrics by Correlation ID**
   - Track request duration metrics
   - Group errors by correlation ID
   - Alert on slow correlation IDs

4. **Documentation Updates**
   - Add correlation ID to API documentation
   - Document expected header format
   - Provide client examples

---

## 📚 References

- **Documentation**: `/brief/02-communication/correlation-tracking.md`
- **Pattern**: Distributed request tracing
- **Standard**: Custom implementation (can be upgraded to W3C Trace Context)
- **Tools**: Serilog, Seq

---

## ✅ Build Status

```
Build succeeded with 10 warning(s) in 12.2s
✅ Shared
✅ ApiGateway
✅ BookingService
✅ PaymentService
✅ UserService
✅ PaymentService.Tests
```

All services compile and tests pass successfully.

---

## 🔧 Correlation ID Persistence

### Challenge: Retries Breaking Correlation
Initially, retry operations were generating new correlation IDs, breaking the trace chain:
- Initial request: `correlation-id-1`
- Retry 1: `correlation-id-2` ❌
- Retry 2: `correlation-id-3` ❌

### Solution: Store Correlation ID in Payment Model
Added `CorrelationId` field to the `Payment` model in MongoDB:
```csharp
[BsonElement("correlationId")]
[BsonRepresentation(BsonType.String)]
public Guid CorrelationId { get; set; }
```

Now retries preserve the original correlation ID:
- Initial request: `correlation-id-1`
- Retry 1: `correlation-id-1` ✅
- Retry 2: `correlation-id-1` ✅

**Example from logs:**
```
PaymentFailed:    correlationId: "f11a2420-59c8-4bf4-b730-bd0341b68778"
PaymentSucceeded: correlationId: "f11a2420-59c8-4bf4-b730-bd0341b68778"
```

Both events share the same correlation ID, enabling complete end-to-end tracing!

---

## ✅ Tested and Verified

### Test Results
```powershell
./scripts/testing/test-e2e-auth.ps1 -NumberOfFlows 1
```

**Results:**
- ✅ Success Rate: 100%
- ✅ Correlation ID generated at API Gateway
- ✅ Correlation ID flows through all services
- ✅ Correlation ID preserved across payment retries
- ✅ All logs tagged with correlation ID
- ✅ Complete trace available in Seq

### Verification in Logs
Query in Seq: `CorrelationId = "f11a2420-59c8-4bf4-b730-bd0341b68778"`

**Complete Flow:**
1. API Gateway receives request
2. BookingService creates booking
3. PaymentService processes payment (fails)
4. PaymentService retries payment (succeeds)
5. BookingService confirms booking

**All events share the same correlation ID!**

---

**Implementation Complete! 🎉**

The system now has full correlation ID tracking from API Gateway through all microservices and events, including retries. Every log entry is automatically tagged, making distributed debugging and monitoring significantly easier.
