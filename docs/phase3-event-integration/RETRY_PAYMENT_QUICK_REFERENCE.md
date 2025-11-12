# Retry Payment - Quick Reference

## What Was Implemented

✅ **Payment Retry Endpoint** - `POST /api/payment/retry`  
✅ **Retry Tracking** - RetryCount and LastRetryAt fields  
✅ **Max Retry Limit** - 3 attempts per payment  
✅ **Payment Method Change** - Optional on retry  
✅ **Event Publishing** - PaymentSucceeded/PaymentFailed on retry result  
✅ **API Gateway Routing** - Automatic through wildcard route  

## Quick Test

### 1. Create Booking & Process Payment (wait for failure)

```bash
# Create booking
POST http://localhost:5000/api/bookings
Authorization: Bearer <token>
{
  "userId": "<guid>",
  "roomId": "ROOM-101",
  "amount": 500000
}

# Process payment (10% chance of failure)
POST http://localhost:5000/api/payment/pay
Authorization: Bearer <token>
{
  "bookingId": "<booking-guid>",
  "amount": 500000
}
```

### 2. Retry Failed Payment

```bash
POST http://localhost:5000/api/payment/retry
Authorization: Bearer <token>
{
  "bookingId": "<booking-guid>",
  "paymentMethod": "CREDIT_CARD"  # Optional
}
```

### 3. Check Results

**Success (90% probability):**
```json
{
  "status": "SUCCESS",
  "transactionId": "TXN-...",
  "retryCount": 1,
  "lastRetryAt": "2025-11-11T..."
}
```

**Failure (10% probability):**
```json
{
  "status": "FAILED",
  "errorMessage": "Payment retry failed",
  "retryCount": 1,
  "lastRetryAt": "2025-11-11T..."
}
```

**Max Retries Reached (after 3 attempts):**
```json
{
  "message": "Maximum retry attempts (3) reached for this payment"
}
```

## API Endpoint

**URL:** `POST /api/payment/retry`  
**Auth:** Required (JWT Bearer)  
**Gateway:** `http://localhost:5000/api/payment/retry`

**Request:**
```json
{
  "bookingId": "guid",
  "paymentMethod": "CREDIT_CARD"  // Optional
}
```

## Key Rules

| Rule | Description |
|------|-------------|
| **Max Retries** | 3 attempts maximum |
| **Only Failed** | Can only retry FAILED payments |
| **Most Recent** | Retries the latest failed payment for booking |
| **Same Events** | Publishes PaymentSucceeded or PaymentFailed |
| **Method Change** | Can optionally change payment method |

## Event Flow

### Successful Retry
```
1. POST /api/payment/retry
2. Find failed payment for booking
3. Validate retry count < 3
4. Update retry tracking (count++, timestamp)
5. Process payment → SUCCESS (90%)
6. Publish PaymentSucceeded event
7. BookingService updates booking to CONFIRMED
```

### Failed Retry
```
1. POST /api/payment/retry
2. Find failed payment for booking
3. Validate retry count < 3
4. Update retry tracking (count++, timestamp)
5. Process payment → FAILED (10%)
6. Publish PaymentFailed event
7. Booking remains CANCELLED
```

## Monitoring

### Seq Logs
```
# All retries
@Message like '%Retrying payment%'

# Successful retries
@Message like '%Payment retry succeeded%'

# Failed retries
@Message like '%Payment retry failed%'

# Max retries reached
@Message like '%Max retry attempts%'
```

### MongoDB Queries
```javascript
// Payments with retries
db.payments.find({ retryCount: { $gt: 0 } })

// Payments at max retries
db.payments.find({ retryCount: 3, status: "FAILED" })

// Retry statistics
db.payments.aggregate([
  { $group: { 
      _id: "$status", 
      avgRetries: { $avg: "$retryCount" },
      count: { $sum: 1 }
  }}
])
```

## Files Changed

✅ `PaymentService/Models/Payment.cs` - Added retry fields  
✅ `PaymentService/DTOs/RetryPaymentRequest.cs` - New DTO  
✅ `PaymentService/DTOs/PaymentResponse.cs` - Added retry fields  
✅ `PaymentService/Services/IPaymentService.cs` - Added method  
✅ `PaymentService/Services/PaymentServiceImpl.cs` - Implemented logic  
✅ `PaymentService/Controllers/PaymentController.cs` - Added endpoint  

## Success Probability

**Single Retry:** 90% success rate  
**All 3 Retries Fail:** 0.1% probability (very rare)

This means if initial payment fails (10%), user has 3 chances to succeed with 90% probability each.

## Common Scenarios

| Scenario | Action |
|----------|--------|
| Payment failed due to network | Retry immediately |
| Insufficient funds | User adds money, then retry |
| Wrong card | Change payment method, then retry |
| 3 retries failed | Contact support or create new booking |
| Booking already confirmed | Cannot retry (payment already succeeded) |

---

**Status:** Complete! 🎉

Users can now recover from payment failures by retrying up to 3 times, with the option to change payment methods.
