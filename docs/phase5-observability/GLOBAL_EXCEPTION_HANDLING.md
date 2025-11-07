# Global Exception Handling Implementation

## Overview

This document describes the global exception handling middleware implemented across all services in the Booking System microservices architecture.

## Architecture

### Shared Middleware

The global exception handling is implemented as a shared middleware component in the `Shared` project, making it reusable across all services:

```
src/Shared/
├── Middleware/
│   └── GlobalExceptionHandlerMiddleware.cs
└── Extensions/
    └── ExceptionHandlingExtensions.cs
```

### Implementation Details

**Location:** `src/Shared/Middleware/GlobalExceptionHandlerMiddleware.cs`

**Key Features:**
- ✅ Centralized error handling across all services
- ✅ Standardized error response format
- ✅ Correlation ID tracking for distributed tracing
- ✅ Exception type mapping to HTTP status codes
- ✅ Environment-aware stack trace inclusion (development only)
- ✅ Structured logging with Serilog
- ✅ Request context preservation (path, method)

## Exception Type Mapping

| Exception Type | HTTP Status Code | Error Code | Description |
|---------------|------------------|------------|-------------|
| `ArgumentNullException` | 400 Bad Request | MISSING_PARAMETER | Required parameter is missing |
| `ArgumentException` | 400 Bad Request | INVALID_ARGUMENT | Invalid argument provided |
| `InvalidOperationException` | 400 Bad Request | INVALID_OPERATION | Operation cannot be performed |
| `UnauthorizedAccessException` | 401 Unauthorized | UNAUTHORIZED | Unauthorized access |
| `KeyNotFoundException` | 404 Not Found | NOT_FOUND | Resource not found |
| `TimeoutException` | 408 Request Timeout | TIMEOUT | Request timeout |
| `NotImplementedException` | 501 Not Implemented | NOT_IMPLEMENTED | Feature not implemented |
| All other exceptions | 500 Internal Server Error | INTERNAL_ERROR | Internal server error |

## Error Response Format

All unhandled exceptions are converted to a standardized JSON response:

```json
{
  "success": false,
  "errorCode": "INTERNAL_ERROR",
  "message": "An internal server error occurred",
  "correlationId": "0HMVR8K7G9876",
  "timestamp": "2025-11-06T14:30:00Z",
  "path": "/api/bookings/123",
  "stackTrace": "... (only in Development environment)"
}
```

### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| `success` | boolean | Always `false` for errors |
| `errorCode` | string | Machine-readable error code |
| `message` | string | Human-readable error message |
| `correlationId` | string | Request trace identifier for tracking |
| `timestamp` | datetime | UTC timestamp when error occurred |
| `path` | string | Request path that caused the error |
| `stackTrace` | string | Stack trace (Development environment only) |

## Integration

### Services Using Global Exception Handler

- ✅ **UserService** - Handles authentication and user management errors
- ✅ **BookingService** - Handles booking-related errors
- ✅ **PaymentService** - Handles payment processing errors
- ✅ **ApiGateway** - Handles gateway-level errors

### Usage in Program.cs

Each service registers the middleware in its startup pipeline:

```csharp
using Shared.Extensions;

var app = builder.Build();

// Must be early in the pipeline to catch all exceptions
app.UseGlobalExceptionHandler();

// Other middleware...
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

### Middleware Order

The exception handler should be placed **early** in the middleware pipeline:

```
1. UseGlobalExceptionHandler()  ← Catches all exceptions
2. UseCors()
3. UseRateLimiter()
4. UseAuthentication()
5. UseAuthorization()
6. MapControllers()
```

## Logging Integration

The middleware integrates with Serilog to provide structured logging:

```
[Error] Unhandled exception occurred. 
  CorrelationId: 0HMVR8K7G9876
  Method: POST
  Path: /api/bookings
  Message: Value cannot be null. (Parameter 'userId')
  Exception: System.ArgumentNullException: Value cannot be null. (Parameter 'userId')
     at BookingService.Controllers.BookingController.CreateBooking(...)
```

### Log Properties

- **Service** - The service name (UserService, BookingService, etc.)
- **CorrelationId** - Request trace identifier
- **Method** - HTTP method (GET, POST, PUT, DELETE)
- **Path** - Request path
- **Exception** - Full exception details with stack trace

## Example Scenarios

### 1. Missing Required Parameter

**Request:**
```http
POST /api/bookings
Content-Type: application/json

{
  "roomId": "ROOM-101",
  "amount": 500000
  // Missing userId
}
```

**Response:**
```http
HTTP/1.1 400 Bad Request
Content-Type: application/json

{
  "success": false,
  "errorCode": "MISSING_PARAMETER",
  "message": "Required parameter is missing",
  "correlationId": "0HMVR8K7G9876",
  "timestamp": "2025-11-06T14:30:00Z",
  "path": "/api/bookings"
}
```

### 2. Resource Not Found

**Request:**
```http
GET /api/bookings/nonexistent-id
```

**Response:**
```http
HTTP/1.1 404 Not Found
Content-Type: application/json

{
  "success": false,
  "errorCode": "NOT_FOUND",
  "message": "Resource not found",
  "correlationId": "0HMVR8K7G9877",
  "timestamp": "2025-11-06T14:31:00Z",
  "path": "/api/bookings/nonexistent-id"
}
```

### 3. Internal Server Error

**Request:**
```http
POST /api/payments/pay
Content-Type: application/json

{
  "bookingId": "invalid-guid",
  "amount": 500000
}
```

**Response:**
```http
HTTP/1.1 500 Internal Server Error
Content-Type: application/json

{
  "success": false,
  "errorCode": "INTERNAL_ERROR",
  "message": "An internal server error occurred",
  "correlationId": "0HMVR8K7G9878",
  "timestamp": "2025-11-06T14:32:00Z",
  "path": "/api/payments/pay",
  "stackTrace": "System.FormatException: Guid should contain 32 digits..."
}
```

## Monitoring and Observability

### Seq Queries

**All Errors in Last Hour:**
```
@Exception != null AND @Timestamp > Now() - 1h
```

**Errors by Service:**
```
@Exception != null
| group by Service
| select count() as ErrorCount, Service
| order by ErrorCount desc
```

**Top Error Paths:**
```
@Exception != null
| group by Path
| select count() as ErrorCount, Path
| order by ErrorCount desc
| limit 10
```

**Correlation ID Tracking:**
```
CorrelationId = "0HMVR8K7G9876"
```

### Metrics to Monitor

1. **Error Rate** - Total errors per minute/hour
2. **Error Types** - Distribution of error codes
3. **Affected Endpoints** - Which paths have most errors
4. **Response Times** - Average error response time
5. **Correlation Chains** - Trace errors across services

## Testing Exception Handling

### Manual Testing

Use the following curl commands to test exception handling:

```bash
# Test missing parameter (400)
curl -X POST http://localhost:5000/api/bookings \
  -H "Content-Type: application/json" \
  -d '{"roomId":"ROOM-101","amount":500000}'

# Test not found (404)
curl http://localhost:5000/api/bookings/nonexistent-id

# Test unauthorized (401)
curl http://localhost:5000/api/bookings \
  -H "Authorization: Bearer invalid-token"
```

### PowerShell Testing Script

```powershell
# Test Exception Handling
$baseUrl = "http://localhost:5000"

# Test 1: Missing parameter
Write-Host "Test 1: Missing Parameter" -ForegroundColor Cyan
$response = Invoke-WebRequest -Uri "$baseUrl/api/bookings" `
    -Method POST `
    -ContentType "application/json" `
    -Body '{"roomId":"ROOM-101"}' `
    -SkipHttpErrorCheck
$response.Content | ConvertFrom-Json | ConvertTo-Json -Depth 5

# Test 2: Not found
Write-Host "`nTest 2: Not Found" -ForegroundColor Cyan
$response = Invoke-WebRequest -Uri "$baseUrl/api/bookings/nonexistent-id" `
    -SkipHttpErrorCheck
$response.Content | ConvertFrom-Json | ConvertTo-Json -Depth 5
```

## Benefits

### 1. Consistency
- All services return errors in the same format
- Clients can rely on consistent error structure
- Easier to build generic error handling in frontend

### 2. Debugging
- Correlation IDs enable tracing requests across services
- Structured logging makes troubleshooting easier
- Stack traces available in development

### 3. Security
- Stack traces hidden in production
- Sensitive information not exposed to clients
- Standardized error messages prevent information leakage

### 4. Maintainability
- Single implementation shared across all services
- Easy to extend with new exception types
- Changes propagate to all services automatically

## Best Practices

### 1. Throw Specific Exceptions

Instead of generic exceptions, throw specific types for better error mapping:

```csharp
// ❌ Bad
throw new Exception("User not found");

// ✅ Good
throw new KeyNotFoundException($"User with ID {userId} not found");
```

### 2. Add Context to Exceptions

Include relevant context in exception messages:

```csharp
// ❌ Bad
throw new ArgumentNullException("userId");

// ✅ Good
throw new ArgumentNullException(nameof(userId), 
    $"User ID is required for booking creation");
```

### 3. Use Custom Exceptions

For domain-specific errors, create custom exception types:

```csharp
public class InsufficientFundsException : Exception
{
    public decimal RequiredAmount { get; }
    public decimal AvailableAmount { get; }
    
    public InsufficientFundsException(decimal required, decimal available)
        : base($"Insufficient funds: required {required}, available {available}")
    {
        RequiredAmount = required;
        AvailableAmount = available;
    }
}
```

Then add mapping in the middleware:

```csharp
InsufficientFundsException => (
    HttpStatusCode.PaymentRequired, 
    exception.Message, 
    "INSUFFICIENT_FUNDS"
)
```

## Future Enhancements

### Planned Improvements

- [ ] **Custom Exception Types** - Domain-specific exception classes
- [ ] **Circuit Breaker Integration** - Handle transient failures gracefully
- [ ] **Retry Hints** - Include retry-after headers for transient errors
- [ ] **Error Categories** - Group errors by category (client/server/network)
- [ ] **Localization** - Multi-language error messages
- [ ] **Error Analytics** - Aggregate error patterns and trends
- [ ] **PII Sanitization** - Automatically remove sensitive data from logs

### Potential Extensions

1. **Problem Details (RFC 7807)** - Use standardized problem details format
2. **Error Documentation Links** - Include links to error documentation
3. **Suggested Actions** - Provide actionable guidance to fix errors
4. **Rate Limiting Headers** - Add retry-after for rate limit errors
5. **Client Request ID** - Support client-provided request IDs

## Related Documentation

- [Phase 5: Observability](PHASE5_SUMMARY.md) - Complete observability guide
- [Seq Quick Reference](SEQ_2025_QUICK_REFERENCE.md) - Seq querying guide
- [Testing Guide](../../general/E2E_TESTING_GUIDE.md) - End-to-end testing

## Summary

The global exception handling middleware provides:

✅ **Centralized** - Single implementation across all services  
✅ **Consistent** - Standardized error responses  
✅ **Observable** - Integrated with Serilog and Seq  
✅ **Traceable** - Correlation ID tracking  
✅ **Secure** - Environment-aware stack traces  
✅ **Maintainable** - Easy to extend and modify  

This foundation ensures robust error handling and improved debugging capabilities across the entire microservices architecture.
