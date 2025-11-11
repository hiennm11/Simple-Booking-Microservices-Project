# Retry Payment Feature - Complete Implementation ✅

## Overview

This feature allows users to retry failed payments for bookings that were cancelled due to payment failures. It includes attempt tracking, maximum retry limits, and automatic event publishing for successful retries.

## Implementation Summary

### Changes Made

#### 1. Payment Model Updates ✅
**File:** `src/PaymentService/Models/Payment.cs`

Added retry tracking fields:
```csharp
[BsonElement("retryCount")]
public int RetryCount { get; set; } = 0;

[BsonElement("lastRetryAt")]
public DateTime? LastRetryAt { get; set; }
```

#### 2. New DTO ✅
**File:** `src/PaymentService/DTOs/RetryPaymentRequest.cs`

Request DTO for retry payment endpoint:
```csharp
public class RetryPaymentRequest
{
    [Required]
    public Guid BookingId { get; set; }
    
    public string? PaymentMethod { get; set; }  // Optional: change payment method
}
```

#### 3. PaymentResponse Updates ✅
**File:** `src/PaymentService/DTOs/PaymentResponse.cs`

Added retry information to response:
```csharp
public int RetryCount { get; set; }
public DateTime? LastRetryAt { get; set; }
```

#### 4. Service Interface Update ✅
**File:** `src/PaymentService/Services/IPaymentService.cs`

Added retry payment method:
```csharp
Task<PaymentResponse> RetryPaymentAsync(RetryPaymentRequest request);
```

#### 5. Business Logic Implementation ✅
**File:** `src/PaymentService/Services/PaymentServiceImpl.cs`

Implemented `RetryPaymentAsync` with:
- Find the latest failed payment for the booking
- Validate retry eligibility (max 3 retries)
- Update retry tracking (count and timestamp)
- Allow optional payment method change
- Process payment with same simulation logic
- Publish appropriate event (PaymentSucceeded or PaymentFailed)
- Track retry attempt number in failure reason

#### 6. Controller Endpoint ✅
**File:** `src/PaymentService/Controllers/PaymentController.cs`

Added new endpoint:
```csharp
[HttpPost("retry")]
public async Task<ActionResult<PaymentResponse>> RetryPayment([FromBody] RetryPaymentRequest request)
```

#### 7. API Gateway ✅
The existing wildcard route `/api/payment/{**catch-all}` automatically handles the retry endpoint.

## Key Features

### 1. Maximum Retry Limit
- **Max Attempts:** 3 retries per payment
- **Enforcement:** Throws `InvalidOperationException` if max retries exceeded
- **User Feedback:** Clear error message indicating retry limit reached

### 2. Retry Tracking
- **Retry Count:** Incremented with each attempt
- **Last Retry Timestamp:** Records when last retry was attempted
- **Payment History:** All retry attempts preserved in same payment document

### 3. Payment Method Flexibility
- **Optional Change:** Users can specify a different payment method for retry
- **Default Behavior:** Uses original payment method if not specified

### 4. Event Publishing
- **Success:** Publishes `PaymentSucceeded` event (booking gets CONFIRMED)
- **Failure:** Publishes `PaymentFailed` event with retry count in reason
- **Outbox Pattern:** All events use reliable outbox for guaranteed delivery

### 5. Idempotency
- **Single Payment:** Only retries the most recent failed payment
- **Status Check:** Validates payment is actually FAILED before retrying
- **Booking Check:** Ensures payment exists for the booking

## API Endpoints

### Retry Payment

**Endpoint:** `POST /api/payment/retry`  
**Gateway URL:** `http://localhost:5000/api/payment/retry`

**Authentication:** Required (JWT Bearer token)

**Request Body:**
```json
{
  "bookingId": "guid-of-booking",
  "paymentMethod": "CREDIT_CARD"  // Optional
}
```

**Success Response (200 OK):**
```json
{
  "id": "payment-guid",
  "bookingId": "booking-guid",
  "amount": 500000,
  "status": "SUCCESS",
  "paymentMethod": "CREDIT_CARD",
  "transactionId": "TXN-abc123def456",
  "errorMessage": null,
  "createdAt": "2025-11-11T10:00:00Z",
  "processedAt": "2025-11-11T10:05:30Z",
  "retryCount": 1,
  "lastRetryAt": "2025-11-11T10:05:25Z"
}
```

**Failure Response (400 Bad Request):**
```json
{
  "id": "payment-guid",
  "bookingId": "booking-guid",
  "amount": 500000,
  "status": "FAILED",
  "paymentMethod": "CREDIT_CARD",
  "transactionId": null,
  "errorMessage": "Payment retry failed",
  "createdAt": "2025-11-11T10:00:00Z",
  "processedAt": "2025-11-11T10:05:30Z",
  "retryCount": 2,
  "lastRetryAt": "2025-11-11T10:05:25Z"
}
```

**Error Response (400 Bad Request - Max Retries):**
```json
{
  "message": "Maximum retry attempts (3) reached for this payment"
}
```

**Error Response (400 Bad Request - No Failed Payment):**
```json
{
  "message": "No failed payment found for booking {bookingId}"
}
```

## Event Flow

### Scenario 1: Successful Retry

```
1. User calls POST /api/payment/retry with bookingId
2. PaymentService finds failed payment
3. Validates retry count < 3
4. Updates retry tracking (count++, timestamp)
5. Processes payment → SUCCESS (90% probability)
6. Updates payment status to SUCCESS
7. Generates transaction ID
8. Saves PaymentSucceeded event to outbox
9. Returns successful payment response
10. OutboxPublisher publishes event to RabbitMQ
11. BookingService consumes event
12. Booking status updated to CONFIRMED
```

### Scenario 2: Failed Retry

```
1. User calls POST /api/payment/retry with bookingId
2. PaymentService finds failed payment
3. Validates retry count < 3
4. Updates retry tracking (count++, timestamp)
5. Processes payment → FAILED (10% probability)
6. Updates payment status to FAILED
7. Saves PaymentFailed event to outbox with retry count
8. Returns failed payment response
9. OutboxPublisher publishes event to RabbitMQ
10. BookingService consumes event
11. Booking remains CANCELLED
```

### Scenario 3: Max Retries Reached

```
1. User calls POST /api/payment/retry with bookingId
2. PaymentService finds failed payment
3. Checks retry count = 3 (max reached)
4. Throws InvalidOperationException
5. Returns 400 Bad Request with error message
6. No payment processing attempted
7. No events published
```

## Database Schema

### Payment Document (MongoDB)

| Field | Type | Description |
|-------|------|-------------|
| Id | GUID | Payment identifier |
| BookingId | GUID | Related booking |
| Amount | Decimal | Payment amount |
| Status | String | PENDING/SUCCESS/FAILED |
| PaymentMethod | String | Payment method |
| TransactionId | String | Transaction ID (nullable) |
| ErrorMessage | String | Error message (nullable) |
| CreatedAt | DateTime | Initial creation time |
| UpdatedAt | DateTime | Last update time |
| ProcessedAt | DateTime | Last processing time (nullable) |
| **RetryCount** | **Int** | **Number of retry attempts** |
| **LastRetryAt** | **DateTime** | **Last retry timestamp (nullable)** |

## Testing Instructions

### Test Case 1: Successful Payment Retry

```bash
# 1. Create booking
POST http://localhost:5000/api/bookings
Authorization: Bearer <token>
{
  "userId": "user-guid",
  "roomId": "ROOM-101",
  "amount": 500000
}

# 2. Process payment (wait for failure)
POST http://localhost:5000/api/payment/pay
Authorization: Bearer <token>
{
  "bookingId": "booking-guid",
  "amount": 500000
}

# Response: Status = "FAILED"

# 3. Check booking is CANCELLED
GET http://localhost:5000/api/bookings/{bookingId}
Authorization: Bearer <token>

# 4. Retry payment
POST http://localhost:5000/api/payment/retry
Authorization: Bearer <token>
{
  "bookingId": "booking-guid"
}

# Expected: Status = "SUCCESS" (90% chance)
# Expected: retryCount = 1
# Expected: Booking status updates to CONFIRMED
```

### Test Case 2: Multiple Retry Attempts

```bash
# Keep retrying until success or max retries reached
for ($i = 1; $i -le 3; $i++) {
    Write-Host "Retry attempt $i..."
    
    POST http://localhost:5000/api/payment/retry
    Authorization: Bearer <token>
    {
      "bookingId": "booking-guid"
    }
    
    # Check retryCount in response
}
```

### Test Case 3: Max Retries Reached

```bash
# After 3 failed retries, next attempt should fail
POST http://localhost:5000/api/payment/retry
Authorization: Bearer <token>
{
  "bookingId": "booking-guid"
}

# Expected: 400 Bad Request
# Expected: "Maximum retry attempts (3) reached for this payment"
```

### Test Case 4: Change Payment Method

```bash
POST http://localhost:5000/api/payment/retry
Authorization: Bearer <token>
{
  "bookingId": "booking-guid",
  "paymentMethod": "DEBIT_CARD"
}

# Expected: Payment processed with new payment method
```

### Test Case 5: No Failed Payment

```bash
# Try to retry a booking with no failed payment
POST http://localhost:5000/api/payment/retry
Authorization: Bearer <token>
{
  "bookingId": "non-existent-booking-guid"
}

# Expected: 400 Bad Request
# Expected: "No failed payment found for booking..."
```

## Monitoring

### Seq Log Queries

**All Retry Attempts:**
```
@Message like '%Retrying payment%'
```

**Successful Retries:**
```
@Message like '%Payment retry succeeded%'
```

**Failed Retries:**
```
@Message like '%Payment retry failed%'
```

**Max Retries Reached:**
```
@Message like '%Max retry attempts%'
```

**Track Specific Booking:**
```
BookingId = "your-booking-guid"
```

### RabbitMQ Monitoring

Check these queues after retry:
- `payment_succeeded` - For successful retry
- `payment_failed` - For failed retry

### MongoDB Queries

**Find payments with retries:**
```javascript
db.payments.find({ retryCount: { $gt: 0 } })
```

**Find payments at max retries:**
```javascript
db.payments.find({ retryCount: 3, status: "FAILED" })
```

**Retry statistics:**
```javascript
db.payments.aggregate([
  { $group: { 
      _id: "$status", 
      avgRetries: { $avg: "$retryCount" },
      maxRetries: { $max: "$retryCount" },
      count: { $sum: 1 }
  }}
])
```

## Business Logic

### Retry Eligibility Rules

1. **Payment Must Exist:** A failed payment for the booking must exist
2. **Status Must Be FAILED:** Only FAILED payments can be retried
3. **Retry Limit:** Maximum 3 retry attempts allowed
4. **Most Recent:** Only the most recent failed payment is retried

### Retry Success Probability

Same as initial payment: **90% success rate**

This means:
- 1st retry: 90% chance of success
- 2nd retry: 90% chance of success (if 1st failed)
- 3rd retry: 90% chance of success (if 1st & 2nd failed)

Probability of all 3 retries failing: 0.1 × 0.1 × 0.1 = **0.1% (very rare)**

### When to Retry?

**Good Scenarios:**
- Temporary network issues
- Insufficient funds (user adds money)
- Card declined (user tries different card)
- Payment gateway timeout

**Bad Scenarios (shouldn't retry):**
- Invalid card number
- Expired card
- Fraudulent transaction

*Note: In a real system, different error codes would determine if retry is appropriate*

## Architecture Benefits

1. **User Recovery:** Users can recover from failed payments without creating new booking
2. **Attempt Limiting:** Prevents infinite retry loops with max 3 attempts
3. **Audit Trail:** Complete history of retry attempts tracked
4. **Flexibility:** Optional payment method change for retries
5. **Consistent Events:** Retry success/failure triggers same events as initial payment
6. **Reliable Delivery:** Outbox pattern ensures events aren't lost

## Integration with Existing Features

### Works With:
- ✅ Payment Failed Event (publishes on retry failure)
- ✅ Payment Succeeded Event (publishes on retry success)
- ✅ Booking Cancellation (booking updates via events)
- ✅ Outbox Pattern (reliable event delivery)
- ✅ JWT Authentication (endpoint is protected)
- ✅ API Gateway (routes through gateway)
- ✅ Rate Limiting (standard payment policy applies)

## Files Modified Summary

### PaymentService (6 files):
- ✅ `Models/Payment.cs` - Added retry tracking fields
- ✅ `DTOs/RetryPaymentRequest.cs` - New request DTO (created)
- ✅ `DTOs/PaymentResponse.cs` - Added retry fields to response
- ✅ `Services/IPaymentService.cs` - Added retry method
- ✅ `Services/PaymentServiceImpl.cs` - Implemented retry logic
- ✅ `Controllers/PaymentController.cs` - Added retry endpoint

### API Gateway (0 files):
- ✅ Already configured with wildcard route

## Limitations & Future Enhancements

### Current Limitations:
1. Fixed 3 retry limit (not configurable)
2. Same payment simulation logic (no smart retry)
3. No cooldown period between retries
4. No retry cost/fee implementation

### Future Enhancements:

1. **Configurable Retry Limit:**
   - Per user tier (premium users get more retries)
   - Per payment amount (higher amounts get fewer retries)

2. **Smart Retry Logic:**
   - Different success rates based on retry count
   - Learn from error patterns
   - Skip retry for permanent failures

3. **Cooldown Period:**
   - Enforce minimum time between retries
   - Exponential backoff for automatic retries

4. **Retry Fees:**
   - Charge small fee for retries after first attempt
   - Encourage users to fix issues before retrying

5. **Partial Refunds:**
   - If original payment partially succeeded
   - Allow retry for remaining amount

6. **Multi-Payment Support:**
   - Allow splitting payment across multiple methods
   - Retry only the failed portion

## Success Criteria ✅

- ✅ Retry payment endpoint implemented
- ✅ Retry tracking fields added to Payment model
- ✅ Maximum retry limit enforced (3 attempts)
- ✅ Payment method can be changed on retry
- ✅ Events published correctly on retry success/failure
- ✅ Outbox pattern used for reliable delivery
- ✅ API Gateway routes retry endpoint correctly
- ✅ Error handling for invalid retry attempts
- ✅ Comprehensive logging for monitoring
- ✅ Build succeeds without errors

---

**Implementation Status:** Complete and Ready for Testing! 🎉

The retry payment feature is now fully functional and integrated with the existing event-driven architecture.
