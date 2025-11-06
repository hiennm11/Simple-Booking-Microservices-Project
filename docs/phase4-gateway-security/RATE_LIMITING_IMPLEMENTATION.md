# 🚦 Rate Limiting Implementation Guide

## 📋 Table of Contents
- [Overview](#overview)
- [What is Rate Limiting?](#what-is-rate-limiting)
- [Why Rate Limiting?](#why-rate-limiting)
- [Rate Limiting Algorithms](#rate-limiting-algorithms)
- [Implementation Architecture](#implementation-architecture)
- [Configuration Guide](#configuration-guide)
- [Policy Details](#policy-details)
- [Testing Rate Limits](#testing-rate-limits)
- [Monitoring and Observability](#monitoring-and-observability)
- [Best Practices](#best-practices)
- [Troubleshooting](#troubleshooting)

---

## Overview

Rate limiting has been implemented in the API Gateway to protect backend services from being overwhelmed by excessive requests. This implementation provides:

✅ **6 Different Rate Limiting Policies** for various use cases  
✅ **4 Rate Limiting Algorithms** (Fixed Window, Sliding Window, Token Bucket, Concurrency Limiter)  
✅ **Per-User and Per-IP Partitioning** for fair resource allocation  
✅ **Configurable Limits** via appsettings.json  
✅ **Comprehensive Logging** with Seq integration  
✅ **Informative Error Responses** with retry guidance  

---

## What is Rate Limiting?

**Rate limiting** is a technique used to control the rate of requests sent to or received by an API. It prevents any single user or IP address from overwhelming the system with too many requests in a given time period.

### Simple Analogy
Think of rate limiting like a **water tap with a flow regulator**:
- Water (requests) flows through the tap
- The regulator ensures water doesn't flow too fast
- If you try to open it fully, it still limits the flow
- This prevents flooding (system overload)

### Example
```
Without Rate Limiting:
User sends 10,000 requests → Server overloaded → Service crashes

With Rate Limiting (100 req/min):
User sends 10,000 requests → First 100 accepted → Rest get 429 error
→ Service stays healthy → User gets clear feedback
```

---

## Why Rate Limiting?

### 1. **Prevent Abuse & DDoS Attacks**
Malicious actors can't overwhelm your API with millions of requests.

```
❌ Without Rate Limiting:
Attacker sends 1,000,000 requests/second → API crashes

✅ With Rate Limiting:
Attacker sends 1,000,000 requests/second → Only 100/min accepted
→ System stays healthy
```

### 2. **Fair Resource Allocation**
Ensure all users get fair access to resources.

```
Scenario: 1000 users using the API
- User A (malicious bot): 10,000 requests/minute
- User B-Z (legitimate): 10 requests/minute each

Without Rate Limiting:
User A consumes 99% of resources → Other users experience slowdowns

With Rate Limiting:
User A limited to 100 requests/minute → Fair access for all users
```

### 3. **Cost Control**
Prevent unexpected infrastructure costs from API abuse.

```
Cloud hosting cost example:
- Normal usage: 1M requests/month = $50/month
- Without rate limiting: Bot sends 100M requests/month = $5,000/month
- With rate limiting: Bot blocked after quota = $50/month
```

### 4. **Service Protection**
Protect downstream services from cascading failures.

```
API Gateway (with rate limiting) → Protects → BookingService, PaymentService
        ↓
   Rejects excess traffic
        ↓
   Backend services stay healthy
```

### 5. **SLA Enforcement**
Enforce different service tiers (free vs premium).

```
Free Tier: 100 requests/minute
Premium Tier: 500 requests/minute
Enterprise Tier: Unlimited (or very high limits)
```

---

## Rate Limiting Algorithms

Our implementation uses 4 different algorithms, each suited for specific use cases.

### 1. **Fixed Window** ⏰

**How it works:**
- Divides time into fixed windows (e.g., 1:00-1:01, 1:01-1:02)
- Counts requests in each window
- Resets counter at window boundary

```
Timeline:
1:00:00 - 1:00:59 → Window 1 (100 requests allowed)
1:01:00 - 1:01:59 → Window 2 (counter resets, 100 requests allowed)
1:02:00 - 1:02:59 → Window 3 (counter resets, 100 requests allowed)

Example:
1:00:00 → Request #1   ✅ (count: 1/100)
1:00:30 → Request #50  ✅ (count: 50/100)
1:00:58 → Request #100 ✅ (count: 100/100)
1:00:59 → Request #101 ❌ (limit exceeded)
1:01:00 → Request #102 ✅ (new window, count: 1/100)
```

**Pros:**
- ✅ Simple to implement
- ✅ Low memory usage
- ✅ Fast performance

**Cons:**
- ❌ Boundary burst problem (user can send 200 requests in 2 seconds at window boundary)

**Use Cases:**
- Global API limits
- Read-only endpoints
- Non-critical operations

**Our Implementation:**
- **Global Policy**: 100 requests per minute per IP
- **Read Policy**: 200 requests per minute per user

---

### 2. **Sliding Window** 🪟

**How it works:**
- Uses a rolling time window
- Divides window into segments
- Counts requests across segments with weighted calculation

```
Window: 1 minute, divided into 6 segments (10 seconds each)

Timeline:
[Seg1][Seg2][Seg3][Seg4][Seg5][Seg6]
  ↓     ↓     ↓     ↓     ↓     ↓
 10s   10s   10s   10s   10s   10s

At 1:00:35 (middle of Seg4):
- Requests in Seg1: Weighted by 50% (partially outside window)
- Requests in Seg2-4: Counted 100%
- Requests in future segments: Not counted yet

This prevents the boundary burst problem!
```

**Example with 5 requests per minute:**
```
Time    Request  Status    Reason
1:00:00  Req #1   ✅       (count: 1/5)
1:00:10  Req #2   ✅       (count: 2/5)
1:00:20  Req #3   ✅       (count: 3/5)
1:00:30  Req #4   ✅       (count: 4/5)
1:00:40  Req #5   ✅       (count: 5/5)
1:00:50  Req #6   ❌       (limit: 5/5)
1:01:01  Req #7   ✅       (req #1 rolled out of window, count: 5/5)
```

**Pros:**
- ✅ No boundary burst problem
- ✅ More accurate rate limiting
- ✅ Smoother traffic distribution

**Cons:**
- ❌ More complex implementation
- ❌ Higher memory usage (stores segments)
- ❌ Slightly slower than fixed window

**Use Cases:**
- Authentication endpoints (prevent brute force)
- Critical operations requiring precise limits
- APIs where burst traffic is problematic

**Our Implementation:**
- **Auth Policy**: 5 requests per 5 minutes per IP
  - Prevents brute force attacks on login
  - Uses 5 segments for smooth tracking

---

### 3. **Token Bucket** 🪣

**How it works:**
- Imagine a bucket that holds tokens
- Tokens are added at a constant rate (refill)
- Each request consumes 1 token
- If bucket is empty, request is rejected
- Allows bursts up to bucket capacity

```
Bucket Setup:
- Capacity: 50 tokens
- Refill rate: 10 tokens per minute

Timeline:
0:00  → Bucket: 50 tokens (full)
0:00  → User sends 30 requests → 20 tokens remain ✅
0:00  → User sends 20 requests → 0 tokens remain ✅
0:00  → User sends 1 request → REJECTED ❌ (empty bucket)
0:01  → 10 tokens added → Bucket: 10 tokens
0:01  → User sends 10 requests → 0 tokens remain ✅
0:02  → 10 tokens added → Bucket: 10 tokens
...continues
```

**Visual Example:**
```
Time 0:00 (Bucket Full)
🪣 [████████████████████████████████████] 50/50 tokens

After 30 requests
🪣 [████████████████            ] 20/50 tokens

After 20 more requests
🪣 [                            ] 0/50 tokens ← Next request rejected

After 1 minute (refill)
🪣 [████████                    ] 10/50 tokens
```

**Pros:**
- ✅ Allows burst traffic (up to bucket capacity)
- ✅ Smooth rate limiting over time
- ✅ Flexible and predictable
- ✅ Good balance between strictness and flexibility

**Cons:**
- ❌ More complex to understand
- ❌ Requires state management (bucket level)

**Use Cases:**
- APIs with bursty traffic patterns
- Operations that naturally come in batches
- Services that can handle occasional bursts

**Our Implementation:**
- **Booking Policy**: 50 token capacity, refill 10 per minute
  - Allows users to create multiple bookings quickly (burst)
  - Then rate-limits to 10 bookings/minute for sustained usage

---

### 4. **Concurrency Limiter** 🔢

**How it works:**
- Limits **simultaneous** requests, not request rate
- When request starts → counter increases
- When request completes → counter decreases
- If max reached, new requests wait in queue or are rejected

```
Max concurrent requests: 3

Timeline:
0:00  → Request A starts    (count: 1/3) ✅ Processing...
0:01  → Request B starts    (count: 2/3) ✅ Processing...
0:02  → Request C starts    (count: 3/3) ✅ Processing...
0:03  → Request D arrives   (count: 3/3) ⏳ Queued (or rejected)
0:04  → Request A completes (count: 2/3) → Request D starts ✅
0:05  → Request E arrives   (count: 3/3) ⏳ Queued
0:06  → Request B completes (count: 2/3) → Request E starts ✅
```

**Visual Example:**
```
Processing Slots: [A] [B] [C]
Queue: [D] [E] [F]

When A completes:
Processing Slots: [D] [B] [C]
Queue: [E] [F]
```

**Pros:**
- ✅ Protects against resource exhaustion
- ✅ Prevents concurrent operations that might conflict
- ✅ Good for database-heavy operations
- ✅ Prevents long-running request pileup

**Cons:**
- ❌ Doesn't prevent rapid sequential requests
- ❌ Can lead to queueing delays

**Use Cases:**
- Payment processing (prevent double payments)
- Database write operations
- Resource-intensive operations
- Operations requiring exclusive access

**Our Implementation:**
- **Payment Policy**: Max 10 concurrent requests per user
  - Prevents accidental double payments
  - Protects payment service from overload
  - Queues up to 2 additional requests

---

## Algorithm Comparison Table

| Algorithm | Best For | Allows Bursts | Prevents Boundary Issue | Complexity | Memory Usage |
|-----------|----------|---------------|------------------------|------------|--------------|
| **Fixed Window** | General APIs, read operations | ❌ | ❌ | Low | Low |
| **Sliding Window** | Auth, critical operations | ❌ | ✅ | Medium | Medium |
| **Token Bucket** | Bursty traffic, booking APIs | ✅ | ✅ | Medium | Medium |
| **Concurrency** | Payments, DB operations | N/A | N/A | Low | Low |

---

## Implementation Architecture

### System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                        API Gateway                           │
├─────────────────────────────────────────────────────────────┤
│  Incoming Request                                            │
│       ↓                                                      │
│  ┌────────────────────────────────────┐                     │
│  │  Rate Limit Middleware              │                     │
│  │  (RateLimiter)                      │                     │
│  ├────────────────────────────────────┤                     │
│  │  1. Extract Partition Key          │                     │
│  │     - IP Address                    │                     │
│  │     - User ID                       │                     │
│  │     - API Key                       │                     │
│  │                                     │                     │
│  │  2. Select Rate Limit Policy       │                     │
│  │     - Global (100/min)             │                     │
│  │     - Auth (5/5min)                │                     │
│  │     - Booking (Token Bucket)       │                     │
│  │     - Payment (Concurrency)        │                     │
│  │     - Read (200/min)               │                     │
│  │     - Premium (500/min)            │                     │
│  │                                     │                     │
│  │  3. Check Against Limit            │                     │
│  │     - Current count/tokens         │                     │
│  │     - Time window                  │                     │
│  │     - Queue status                 │                     │
│  └────────┬─────────────────┬─────────┘                     │
│           │                 │                                │
│           ↓                 ↓                                │
│     ┌─────────┐      ┌──────────────┐                       │
│     │ ALLOWED │      │   REJECTED   │                       │
│     └────┬────┘      └──────┬───────┘                       │
│          │                  │                                │
│          ↓                  ↓                                │
│  ┌──────────────┐   ┌──────────────────────┐               │
│  │ Forward to   │   │ Return 429 Response  │               │
│  │ Downstream   │   │ + Retry-After header │               │
│  │ Service      │   │ + Rate limit info    │               │
│  └──────────────┘   └──────────────────────┘               │
└─────────────────────────────────────────────────────────────┘
```

### Request Flow

```
1. Client Request Arrives
   ↓
2. Rate Limiter Extracts Identifier
   - IP: 192.168.1.100
   - User: user123
   - Endpoint: /api/bookings
   ↓
3. Selects Appropriate Policy
   - POST /api/bookings → "booking" policy (Token Bucket)
   ↓
4. Checks Current State
   - User "user123" has 15 tokens remaining
   - Request needs 1 token
   ↓
5. Decision
   ✅ Allowed (15 - 1 = 14 tokens remaining)
   ↓
6. Update State
   - Set tokens = 14
   - Log event to Seq
   ↓
7. Forward Request to BookingService
   ↓
8. Return Response to Client
   - Add headers:
     X-RateLimit-Limit: 50
     X-RateLimit-Remaining: 14
     X-RateLimit-Policy: booking
```

### When Limit is Exceeded

```
1. Client Request Arrives (User already at limit)
   ↓
2. Rate Limiter Checks State
   - User "user123" has 0 tokens
   - Request needs 1 token
   ↓
3. Decision
   ❌ Rejected (no tokens available)
   ↓
4. Calculate Retry Time
   - Next token refill in 45 seconds
   ↓
5. Log Warning to Seq
   - "Rate limit exceeded for /api/bookings by user123"
   ↓
6. Return 429 Response
   {
     "error": "Rate limit exceeded",
     "message": "Too many requests. Please try again later.",
     "retryAfter": 45,
     "endpoint": "/api/bookings",
     "timestamp": "2025-11-06T10:30:00Z"
   }
   Headers:
     Retry-After: 45
     X-RateLimit-Limit: 50
     X-RateLimit-Remaining: 0
```

---

## Configuration Guide

### appsettings.json Structure

```json
{
  "RateLimiting": {
    "GlobalPolicy": {
      "PermitLimit": 100,
      "WindowMinutes": 1,
      "QueueLimit": 5
    },
    "AuthPolicy": {
      "PermitLimit": 5,
      "WindowMinutes": 5,
      "SegmentsPerWindow": 5
    },
    "BookingPolicy": {
      "TokenLimit": 50,
      "TokensPerPeriod": 10,
      "ReplenishmentMinutes": 1,
      "QueueLimit": 3
    },
    "PaymentPolicy": {
      "PermitLimit": 10,
      "QueueLimit": 2
    },
    "ReadPolicy": {
      "PermitLimit": 200,
      "WindowMinutes": 1
    },
    "PremiumPolicy": {
      "PermitLimit": 500,
      "WindowMinutes": 1
    }
  }
}
```

### Configuration Parameters Explained

#### Fixed Window & Sliding Window Parameters

| Parameter | Type | Description | Example |
|-----------|------|-------------|---------|
| `PermitLimit` | int | Maximum requests allowed in window | 100 |
| `WindowMinutes` | int | Time window duration in minutes | 1 |
| `QueueLimit` | int | Max queued requests when limit reached | 5 |
| `SegmentsPerWindow` | int | (Sliding only) Number of segments per window | 5 |

#### Token Bucket Parameters

| Parameter | Type | Description | Example |
|-----------|------|-------------|---------|
| `TokenLimit` | int | Maximum tokens in bucket (capacity) | 50 |
| `TokensPerPeriod` | int | Tokens added per replenishment period | 10 |
| `ReplenishmentMinutes` | int | How often tokens are added (minutes) | 1 |
| `QueueLimit` | int | Max queued requests | 3 |

#### Concurrency Limiter Parameters

| Parameter | Type | Description | Example |
|-----------|------|-------------|---------|
| `PermitLimit` | int | Max concurrent requests | 10 |
| `QueueLimit` | int | Max requests in queue | 2 |

---

## Policy Details

### 1. Global Policy (Default)

**Algorithm:** Fixed Window  
**Partition Key:** IP Address  
**Purpose:** Baseline protection for all endpoints

```json
"GlobalPolicy": {
  "PermitLimit": 100,
  "WindowMinutes": 1,
  "QueueLimit": 5
}
```

**Behavior:**
- Applies to all requests unless overridden
- Limits each IP to 100 requests per minute
- Queues up to 5 requests when limit reached
- Window resets every minute

**Use Cases:**
- General API protection
- Prevents casual abuse
- Baseline for all traffic

**Example:**
```bash
# IP 192.168.1.100 sends requests
curl http://localhost:5000/api/users/123  # ✅ Request 1/100
curl http://localhost:5000/api/users/456  # ✅ Request 2/100
# ... 98 more requests ...
curl http://localhost:5000/api/users/789  # ✅ Request 100/100
curl http://localhost:5000/api/users/999  # ❌ 429 Too Many Requests

# After 1 minute, counter resets
curl http://localhost:5000/api/users/111  # ✅ Request 1/100 (new window)
```

---

### 2. Auth Policy

**Algorithm:** Sliding Window  
**Partition Key:** IP Address  
**Purpose:** Prevent brute force attacks on authentication endpoints

```json
"AuthPolicy": {
  "PermitLimit": 5,
  "WindowMinutes": 5,
  "SegmentsPerWindow": 5
}
```

**Behavior:**
- Strict limit: 5 attempts per 5 minutes per IP
- Uses sliding window to prevent boundary attacks
- No queueing (immediate rejection)
- Window slides continuously (no sudden resets)

**Use Cases:**
- POST /api/users/login
- POST /api/users/register
- Password reset endpoints
- Any authentication operations

**Why This Policy:**
- **5 attempts** allows legitimate users to retry (typos)
- **5 minutes** lockout deters automated attacks
- **Sliding window** prevents timing attacks at boundaries
- **No queue** ensures fast rejection of attackers

**Example Scenario:**
```bash
# Attacker tries brute force from IP 10.0.0.5
10:00:00 → POST /api/users/login (wrong password) ✅ Attempt 1/5
10:00:10 → POST /api/users/login (wrong password) ✅ Attempt 2/5
10:00:20 → POST /api/users/login (wrong password) ✅ Attempt 3/5
10:00:30 → POST /api/users/login (wrong password) ✅ Attempt 4/5
10:00:40 → POST /api/users/login (wrong password) ✅ Attempt 5/5
10:00:50 → POST /api/users/login (wrong password) ❌ 429 Rate Limit
10:01:00 → POST /api/users/login (wrong password) ❌ 429 Rate Limit
# ... continues until 10:05:00 ...
10:05:01 → POST /api/users/login ✅ First attempt rolled out, 1 attempt available
```

---

### 3. Booking Policy

**Algorithm:** Token Bucket  
**Partition Key:** User ID (or IP if not authenticated)  
**Purpose:** Allow burst booking creation while preventing sustained abuse

```json
"BookingPolicy": {
  "TokenLimit": 50,
  "TokensPerPeriod": 10,
  "ReplenishmentMinutes": 1,
  "QueueLimit": 3
}
```

**Behavior:**
- Bucket starts with 50 tokens
- Each booking request consumes 1 token
- 10 tokens refilled every minute
- Queues up to 3 requests when empty

**Use Cases:**
- POST /api/bookings (create booking)
- PUT /api/bookings/{id} (update booking)
- Batch booking operations

**Why This Policy:**
- **50 tokens** allows legitimate users to make multiple bookings quickly
- **10/minute** refill = sustainable rate for normal usage
- **Token bucket** perfect for bursty booking patterns
- **Queue** handles slight bursts gracefully

**Example Scenario:**
```bash
# User books a group trip (needs 15 bookings quickly)
User "alice" starts with 50 tokens

10:00:00 → POST /api/bookings (Room 101) ✅ 49 tokens left
10:00:01 → POST /api/bookings (Room 102) ✅ 48 tokens left
10:00:02 → POST /api/bookings (Room 103) ✅ 47 tokens left
# ... 12 more bookings ...
10:00:14 → POST /api/bookings (Room 115) ✅ 35 tokens left

# User continues later
10:01:00 → Bucket refilled: 35 + 10 = 45 tokens
10:01:05 → POST /api/bookings (Room 116) ✅ 44 tokens left

# If user depletes bucket
10:05:00 → 0 tokens, but refill coming soon
10:05:05 → POST /api/bookings ⏳ Queued (3 slots available)
10:06:00 → 10 tokens refilled → Queued request processed ✅
```

---

### 4. Payment Policy

**Algorithm:** Concurrency Limiter  
**Partition Key:** User ID (or IP if not authenticated)  
**Purpose:** Prevent concurrent payment operations that could cause double charges

```json
"PaymentPolicy": {
  "PermitLimit": 10,
  "QueueLimit": 2
}
```

**Behavior:**
- Max 10 simultaneous payment requests per user
- Queues up to 2 additional requests
- When payment completes, queued request starts

**Use Cases:**
- POST /api/payment/pay (process payment)
- POST /api/payment/refund (refund payment)
- Any financial transaction

**Why This Policy:**
- **10 concurrent** sufficient for legitimate use
- **Prevents double payment** bugs from rapid clicks
- **Concurrency** better than rate for payment operations
- **Queue** handles accidental double-clicks

**Example Scenario:**
```bash
# User accidentally clicks "Pay" multiple times
10:00:00.000 → POST /api/payment/pay (BookingId: 123) ✅ Processing (slot 1/10)
10:00:00.100 → POST /api/payment/pay (BookingId: 123) ❌ REJECTED (same booking)
10:00:00.200 → POST /api/payment/pay (BookingId: 456) ✅ Processing (slot 2/10)

# Payment completes
10:00:05.000 → Payment 123 completes → Slot freed (1/10 in use)

# Multiple users paying simultaneously
User A → POST /api/payment/pay ✅ Slot 1
User B → POST /api/payment/pay ✅ Slot 2
# ... 8 more users ...
User K → POST /api/payment/pay ✅ Slot 10 (last slot)
User L → POST /api/payment/pay ⏳ Queued (position 1 in queue)
User M → POST /api/payment/pay ⏳ Queued (position 2 in queue)
User N → POST /api/payment/pay ❌ 429 Queue full

# When User A's payment completes
User A payment completes → User L starts ✅
```

---

### 5. Read Policy

**Algorithm:** Fixed Window  
**Partition Key:** User ID (or IP if not authenticated)  
**Purpose:** Higher limits for read-only operations

```json
"ReadPolicy": {
  "PermitLimit": 200,
  "WindowMinutes": 1
}
```

**Behavior:**
- 200 requests per minute per user
- Fixed window (simple and fast)
- Higher limits than write operations

**Use Cases:**
- GET /api/bookings (list bookings)
- GET /api/bookings/{id} (get booking)
- GET /api/users/{id} (get user)
- Any read-only operations

**Why This Policy:**
- **200/minute** supports active users browsing
- **Read operations** less resource-intensive
- **Fixed window** sufficient for reads
- **Per-user** ensures fair access

---

### 6. Premium Policy

**Algorithm:** Fixed Window  
**Partition Key:** User ID  
**Purpose:** Higher limits for premium tier users (future feature)

```json
"PremiumPolicy": {
  "PermitLimit": 500,
  "WindowMinutes": 1
}
```

**Behavior:**
- 500 requests per minute per premium user
- 5x the global limit
- Premium users identified by JWT claims

**Use Cases:**
- Premium subscription tier
- Enterprise API access
- High-volume legitimate users

**Future Implementation:**
```csharp
// Check if user is premium from JWT claims
var isPremium = context.User.FindFirst("isPremium")?.Value == "true";
var policy = isPremium ? "premium" : "global";
```

---

## Testing Rate Limits

### Manual Testing with cURL

#### Test Global Rate Limit

```bash
# Send 101 requests rapidly (Windows Command Prompt)
for /L %i in (1,1,101) do @curl -s -o nul -w "Request %i: %%{http_code}\n" http://localhost:5000/api/users/123

# Expected output:
# Request 1: 200
# Request 2: 200
# ...
# Request 100: 200
# Request 101: 429  ← Rate limit exceeded
```

#### Test Auth Rate Limit

```bash
# Attempt 6 logins rapidly (PowerShell)
for ($i=1; $i -le 6; $i++) {
    $response = Invoke-WebRequest -Uri "http://localhost:5000/api/users/login" `
        -Method POST `
        -Body '{"email":"test@test.com","password":"wrong"}' `
        -ContentType "application/json" `
        -SkipHttpErrorCheck
    
    Write-Host "Attempt $i : $($response.StatusCode)"
}

# Expected output:
# Attempt 1: 401 (wrong password but not rate limited)
# Attempt 2: 401
# Attempt 3: 401
# Attempt 4: 401
# Attempt 5: 401
# Attempt 6: 429  ← Rate limit exceeded
```

#### Test Token Bucket (Booking)

```bash
# Create 51 bookings rapidly (should succeed for first 50, then wait for refill)
$token = "your-jwt-token"

for ($i=1; $i -le 51; $i++) {
    $response = Invoke-WebRequest `
        -Uri "http://localhost:5000/api/bookings" `
        -Method POST `
        -Headers @{"Authorization"="Bearer $token"} `
        -Body '{"roomId":"ROOM-101","amount":5000}' `
        -ContentType "application/json" `
        -SkipHttpErrorCheck
    
    Write-Host "Booking $i : $($response.StatusCode)"
}

# Expected output:
# Booking 1-50: 201 Created
# Booking 51: 429 Too Many Requests
```

### Automated Test Script

Create `test-rate-limits.ps1`:

```powershell
# Test Rate Limiting Implementation

Write-Host "=== Rate Limiting Test Suite ===" -ForegroundColor Cyan

# Test 1: Global Rate Limit
Write-Host "`n[Test 1] Testing Global Rate Limit (100/min)..." -ForegroundColor Yellow
$success = 0
$failed = 0

for ($i=1; $i -le 105; $i++) {
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:5000/health" -SkipHttpErrorCheck
        if ($response.StatusCode -eq 200) { $success++ }
        if ($response.StatusCode -eq 429) { $failed++ }
    } catch {
        Write-Host "Error: $_"
    }
}

Write-Host "✅ Successful requests: $success" -ForegroundColor Green
Write-Host "❌ Rate limited requests: $failed" -ForegroundColor Red

if ($failed -gt 0) {
    Write-Host "✓ Global rate limit is working!" -ForegroundColor Green
} else {
    Write-Host "✗ Global rate limit may not be working" -ForegroundColor Red
}

# Test 2: Auth Rate Limit
Write-Host "`n[Test 2] Testing Auth Rate Limit (5/5min)..." -ForegroundColor Yellow
$authSuccess = 0
$authBlocked = 0

for ($i=1; $i -le 6; $i++) {
    try {
        $response = Invoke-WebRequest `
            -Uri "http://localhost:5000/api/users/login" `
            -Method POST `
            -Body '{"email":"test@test.com","password":"wrong"}' `
            -ContentType "application/json" `
            -SkipHttpErrorCheck
        
        if ($response.StatusCode -eq 401) { $authSuccess++ }
        if ($response.StatusCode -eq 429) { 
            $authBlocked++ 
            $content = $response.Content | ConvertFrom-Json
            Write-Host "Blocked! Retry after: $($content.retryAfter) seconds"
        }
    } catch {
        Write-Host "Error: $_"
    }
}

Write-Host "✅ Auth attempts before limit: $authSuccess" -ForegroundColor Green
Write-Host "❌ Auth attempts blocked: $authBlocked" -ForegroundColor Red

if ($authBlocked -gt 0) {
    Write-Host "✓ Auth rate limit is working!" -ForegroundColor Green
} else {
    Write-Host "✗ Auth rate limit may not be working" -ForegroundColor Red
}

# Test 3: Check Response Headers
Write-Host "`n[Test 3] Checking Rate Limit Headers..." -ForegroundColor Yellow
$response = Invoke-WebRequest -Uri "http://localhost:5000/health"

$headers = @(
    "X-RateLimit-Limit",
    "X-RateLimit-Remaining",
    "X-RateLimit-Policy"
)

foreach ($header in $headers) {
    $value = $response.Headers[$header]
    if ($value) {
        Write-Host "✓ $header : $value" -ForegroundColor Green
    } else {
        Write-Host "✗ $header : Not found" -ForegroundColor Red
    }
}

Write-Host "`n=== Test Suite Complete ===" -ForegroundColor Cyan
```

Run the test:

```bash
powershell -ExecutionPolicy Bypass -File test-rate-limits.ps1
```

---

## Monitoring and Observability

### Seq Queries for Rate Limiting

#### 1. Rate Limit Violations (Last Hour)

```sql
select count(*) as ViolationCount, Endpoint, UserId, IpAddress
from stream
where @Message like '%Rate limit exceeded%'
  and @Timestamp > Now() - 1h
group by Endpoint, UserId, IpAddress
order by ViolationCount desc
```

**What it shows:** Top offenders hitting rate limits

#### 2. Rate Limit Violations Timeline

```sql
select @Timestamp, Endpoint, UserId, IpAddress, Limit
from stream
where @Message like '%Rate limit exceeded%'
order by @Timestamp desc
limit 100
```

**What it shows:** Recent rate limit violations with details

#### 3. Top Endpoints Being Rate Limited

```sql
select count(*) as Count, Endpoint
from stream
where @Message like '%Rate limit exceeded%'
  and @Timestamp > Now() - 24h
group by Endpoint
order by Count desc
```

**What it shows:** Which endpoints are getting rate limited most

#### 4. Rate Limit Effectiveness (Blocked vs Allowed)

```sql
-- This query shows the ratio of blocked requests
select 
  count(case when @Message like '%Rate limit exceeded%' then 1 end) as Blocked,
  count(case when StatusCode = 200 then 1 end) as Allowed,
  count(*) as Total
from stream
where @Timestamp > Now() - 1h
```

#### 5. User Behavior Analysis

```sql
select UserId, 
       count(*) as TotalRequests,
       count(case when @Message like '%Rate limit exceeded%' then 1 end) as RateLimited,
       count(*) - count(case when @Message like '%Rate limit exceeded%' then 1 end) as Successful
from stream
where @Timestamp > Now() - 1h
  and UserId is not null
group by UserId
order by RateLimited desc
```

### Creating Seq Alerts

#### Alert: High Rate Limit Violations

```sql
-- Alert when more than 100 rate limit violations in 5 minutes
select count(*) as ViolationCount
from stream
where @Message like '%Rate limit exceeded%'
  and @Timestamp > Now() - 5m
having count(*) > 100
```

**Alert Configuration:**
- **Title:** High Rate Limit Violations
- **Priority:** Warning
- **Notification:** Email/Slack
- **Action:** Investigate potential DDoS or misconfiguration

#### Alert: Specific User Hitting Limits

```sql
-- Alert when a user hits rate limit 10 times in 5 minutes
select UserId, count(*) as Count
from stream
where @Message like '%Rate limit exceeded%'
  and @Timestamp > Now() - 5m
  and UserId is not null
group by UserId
having count(*) > 10
```

### Grafana Dashboard (Future)

If you add Prometheus + Grafana, track these metrics:

```
1. Rate Limit Hits (Counter)
   - Labels: endpoint, policy, user_id
   - Query: rate(rate_limit_hits_total[5m])

2. Rate Limit Rejections (Counter)
   - Labels: endpoint, policy, reason
   - Query: rate(rate_limit_rejections_total[5m])

3. Rate Limit Remaining (Gauge)
   - Labels: user_id, policy
   - Shows current available quota

4. Queue Size (Gauge)
   - Labels: policy
   - Shows number of queued requests
```

---

## Best Practices

### 1. **Choose the Right Algorithm**

```
✅ DO:
- Use Fixed Window for general API protection (fast, simple)
- Use Sliding Window for authentication (prevents boundary attacks)
- Use Token Bucket for bursty operations (bookings, uploads)
- Use Concurrency Limiter for resource-intensive operations (payments)

❌ DON'T:
- Use Sliding Window everywhere (unnecessary overhead)
- Use Token Bucket for constant-rate operations
- Use Concurrency Limiter for stateless operations
```

### 2. **Set Appropriate Limits**

```
✅ DO:
- Base limits on actual usage patterns
- Set higher limits for read operations
- Set lower limits for write operations
- Set very low limits for auth endpoints

Example:
- GET /api/bookings: 200/min (read-heavy)
- POST /api/bookings: 50/min (write operation)
- POST /api/users/login: 5/5min (security-critical)

❌ DON'T:
- Set limits too low (frustrate legitimate users)
- Set limits too high (defeat the purpose)
- Use same limits for all endpoints
```

### 3. **Partition Keys Matter**

```
✅ DO:
- Use IP address for unauthenticated endpoints
- Use User ID for authenticated endpoints
- Combine both if needed: $"{userId}_{ipAddress}"

Example:
public string GetPartitionKey(HttpContext context)
{
    var userId = context.User.FindFirst("userId")?.Value;
    var ipAddress = context.Connection.RemoteIpAddress?.ToString();
    
    // Authenticated user
    if (!string.IsNullOrEmpty(userId))
        return $"user_{userId}";
    
    // Anonymous user
    return $"ip_{ipAddress}";
}

❌ DON'T:
- Use only IP (users behind same NAT get same limit)
- Use only User ID (attacker can create many accounts)
- Use constant key (all users share same limit)
```

### 4. **Informative Error Responses**

```
✅ DO:
{
  "error": "Rate limit exceeded",
  "message": "Too many requests. Please try again later.",
  "retryAfter": 60,
  "limit": 100,
  "remaining": 0,
  "resetAt": "2025-11-06T10:35:00Z",
  "policy": "booking",
  "documentation": "https://api.example.com/docs/rate-limiting"
}

❌ DON'T:
{
  "error": "Too many requests"
}
```

### 5. **Add Response Headers**

```
✅ DO:
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 45
X-RateLimit-Reset: 1699281600
Retry-After: 60
X-RateLimit-Policy: booking

❌ DON'T:
- Omit headers (client can't know their status)
- Use inconsistent header names
```

### 6. **Monitor and Alert**

```
✅ DO:
- Log all rate limit violations to Seq
- Set up alerts for unusual patterns
- Track metrics per endpoint and user
- Review and adjust limits based on data

❌ DON'T:
- Set rate limits and forget them
- Ignore rate limit violations
- Assume limits are correct without monitoring
```

### 7. **Handle Queue Properly**

```
✅ DO:
- Use small queue sizes (3-5)
- Set queue timeout
- Log queued requests
- Monitor queue depth

❌ DON'T:
- Use large queues (leads to long waits)
- Queue indefinitely (memory leak risk)
- Ignore queue size in monitoring
```

### 8. **Test Rate Limits**

```
✅ DO:
- Test during development
- Test in staging environment
- Load test to verify limits
- Test edge cases (boundary, bursts)

❌ DON'T:
- Deploy untested rate limits to production
- Assume limits work without testing
- Test only happy path
```

---

## Troubleshooting

### Issue 1: Legitimate Users Getting Rate Limited

**Symptoms:**
- Users complain about 429 errors
- High rate of rate limit violations in logs
- Many different users affected

**Diagnosis:**
```sql
-- Check if limits are too strict
select UserId, count(*) as RequestCount
from stream
where @Timestamp > Now() - 1h
  and StatusCode = 429
group by UserId
having count(*) > 10
```

**Solutions:**

1. **Increase limits:**
```json
"ReadPolicy": {
  "PermitLimit": 300,  // Increased from 200
  "WindowMinutes": 1
}
```

2. **Add Premium tier:**
```json
"PremiumPolicy": {
  "PermitLimit": 1000,
  "WindowMinutes": 1
}
```

3. **Whitelist IP addresses:**
```csharp
options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
{
    var ipAddress = context.Connection.RemoteIpAddress?.ToString();
    
    // Whitelist specific IPs
    if (ipAddress == "10.0.0.1" || ipAddress == "10.0.0.2")
    {
        return RateLimitPartition.GetNoLimiter("whitelist");
    }
    
    // Regular rate limiting
    return RateLimitPartition.GetFixedWindowLimiter(...);
});
```

---

### Issue 2: Rate Limits Not Working

**Symptoms:**
- No 429 responses even with excessive requests
- Rate limit headers missing
- No rate limit logs in Seq

**Diagnosis:**

1. Check if middleware is registered:
```csharp
// In Program.cs - must be in this order
app.UseRouting();
app.UseRateLimiter();  // ← Check this exists
app.UseAuthentication();
app.UseAuthorization();
```

2. Check if service is configured:
```csharp
// In Program.cs - must be before app.Build()
builder.Services.AddCustomRateLimiting(builder.Configuration);
```

3. Check configuration:
```bash
# Verify config is loaded
curl http://localhost:5000/health
# Check headers
```

**Solutions:**

1. Ensure middleware order:
```csharp
// Correct order
app.UseRouting();
app.UseRateLimiter();       // After routing
app.UseAuthentication();    // After rate limiting
app.UseAuthorization();
app.MapReverseProxy();      // Last
```

2. Verify configuration loaded:
```csharp
var config = builder.Configuration.GetSection("RateLimiting");
if (!config.Exists())
{
    throw new Exception("RateLimiting configuration not found!");
}
```

---

### Issue 3: Rate Limit Resets Too Quickly/Slowly

**Symptoms:**
- Users can make more requests than expected
- Or users wait longer than expected

**Diagnosis:**
```csharp
// Log window resets
Log.Information("Rate limit window reset for {PartitionKey} at {ResetTime}", 
    partitionKey, DateTime.UtcNow);
```

**Solutions:**

1. Check window calculation:
```json
{
  "PermitLimit": 100,
  "WindowMinutes": 1  // ← Verify this is correct
}
```

2. Verify clock synchronization:
```bash
# Check system time
powershell -Command "Get-Date"

# Check if time is synchronized
w32tm /query /status
```

---

### Issue 4: Different Results from Different IPs

**Symptoms:**
- Same user gets different rate limit behavior from different locations
- Inconsistent rate limiting

**Diagnosis:**
```sql
-- Check partition keys being used
select distinct Endpoint, UserId, IpAddress, Limit
from stream
where @Message like '%Rate limit%'
  and @Timestamp > Now() - 1h
```

**Solutions:**

1. Use consistent partition key:
```csharp
// Bad: Inconsistent
var key = userId ?? ipAddress;

// Good: Consistent fallback
var key = !string.IsNullOrEmpty(userId) 
    ? $"user_{userId}" 
    : $"ip_{ipAddress}";
```

2. Consider using both:
```csharp
// Rate limit by both user AND IP
var key = $"{userId}_{ipAddress}";
```

---

### Issue 5: Queue Filling Up

**Symptoms:**
- Many requests queued
- Increased latency
- Timeouts

**Diagnosis:**
```csharp
// Add logging in middleware
Log.Warning("Rate limit queue size for {Policy}: {QueueSize}", 
    policyName, queueSize);
```

**Solutions:**

1. Reduce queue size:
```json
{
  "QueueLimit": 2  // Reduced from 5
}
```

2. Add queue timeout:
```csharp
options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
options.QueueLimit = 3;
// Add timeout (requires custom implementation)
```

3. Use concurrency limiter instead:
```json
// Switch from fixed window to concurrency
"PaymentPolicy": {
  "PermitLimit": 10,  // Max concurrent
  "QueueLimit": 2
}
```

---

## Common Patterns

### Pattern 1: Different Limits for Different HTTP Methods

```csharp
options.AddPolicy("method-based", context =>
{
    var method = context.Request.Method;
    var userId = context.User.FindFirst("userId")?.Value ?? "anonymous";
    
    // GET requests: higher limit
    if (method == "GET")
    {
        return RateLimitPartition.GetFixedWindowLimiter(
            $"read_{userId}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 200,
                Window = TimeSpan.FromMinutes(1)
            });
    }
    
    // POST/PUT/DELETE: lower limit
    return RateLimitPartition.GetFixedWindowLimiter(
        $"write_{userId}",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 50,
            Window = TimeSpan.FromMinutes(1)
        });
});
```

### Pattern 2: Time-of-Day Rate Limiting

```csharp
options.AddPolicy("time-based", context =>
{
    var hour = DateTime.UtcNow.Hour;
    var userId = context.User.FindFirst("userId")?.Value ?? "anonymous";
    
    // Peak hours (9 AM - 5 PM): stricter limits
    int limit = (hour >= 9 && hour <= 17) ? 50 : 100;
    
    return RateLimitPartition.GetFixedWindowLimiter(
        $"time_{userId}",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit,
            Window = TimeSpan.FromMinutes(1)
        });
});
```

### Pattern 3: Tiered Rate Limiting

```csharp
options.AddPolicy("tiered", context =>
{
    var tier = context.User.FindFirst("tier")?.Value ?? "free";
    var userId = context.User.FindFirst("userId")?.Value ?? "anonymous";
    
    int limit = tier switch
    {
        "free" => 100,
        "basic" => 200,
        "premium" => 500,
        "enterprise" => 2000,
        _ => 50
    };
    
    return RateLimitPartition.GetFixedWindowLimiter(
        $"tier_{tier}_{userId}",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit,
            Window = TimeSpan.FromMinutes(1)
        });
});
```

### Pattern 4: Cost-Based Rate Limiting

```csharp
// Different operations consume different amounts of "credits"
options.AddPolicy("cost-based", context =>
{
    var userId = context.User.FindFirst("userId")?.Value ?? "anonymous";
    
    return RateLimitPartition.GetTokenBucketLimiter(
        $"cost_{userId}",
        _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 1000,  // 1000 credits
            TokensPerPeriod = 100,  // Refill 100 credits/min
            ReplenishmentPeriod = TimeSpan.FromMinutes(1)
        });
});

// In your endpoint:
// - Simple GET: 1 credit
// - Complex search: 10 credits
// - Report generation: 100 credits
```

---

## Summary

Rate limiting is now fully implemented in your API Gateway with:

✅ **6 Policies** covering all use cases  
✅ **4 Algorithms** for different scenarios  
✅ **Flexible Configuration** via appsettings.json  
✅ **Comprehensive Logging** with Seq integration  
✅ **Informative Responses** with retry guidance  
✅ **Production Ready** with monitoring and alerting  

### Quick Reference

| Endpoint | Policy | Limit | Algorithm |
|----------|--------|-------|-----------|
| All endpoints | Global | 100/min per IP | Fixed Window |
| POST /api/users/login | Auth | 5/5min per IP | Sliding Window |
| POST /api/bookings | Booking | 50 tokens, +10/min | Token Bucket |
| POST /api/payment/pay | Payment | 10 concurrent | Concurrency |
| GET endpoints | Read | 200/min per user | Fixed Window |
| Premium users | Premium | 500/min per user | Fixed Window |

### Next Steps

1. **Deploy and Monitor**
   - Watch Seq for rate limit violations
   - Adjust limits based on real usage

2. **Add Alerts**
   - Set up Seq alerts for excessive violations
   - Monitor for DDoS patterns

3. **Implement Premium Tier**
   - Add `isPremium` claim to JWT
   - Apply premium policy to premium users

4. **Consider Redis**
   - For distributed deployments
   - Share rate limit state across gateway instances

---

**For questions or issues, check:**
- API Gateway logs in Seq
- This documentation
- Rate limiting middleware code in `Middleware/RateLimitMiddleware.cs`
