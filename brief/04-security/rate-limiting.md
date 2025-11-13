# Rate Limiting - Deep Dive

## Table of Contents

- [What is Rate Limiting?](#what-is-rate-limiting)
- [Why Rate Limiting Matters](#why-rate-limiting-matters)
- [Rate Limiting Algorithms](#rate-limiting-algorithms)
- [Token Bucket Algorithm](#token-bucket-algorithm)
- [Multi-Policy Configuration](#multi-policy-configuration)
- [Implementation in API Gateway](#implementation-in-api-gateway)
- [Client Identification Strategies](#client-identification-strategies)
- [Response Handling](#response-handling)
- [Testing Rate Limits](#testing-rate-limits)
- [Common Pitfalls](#common-pitfalls)
- [Best Practices](#best-practices)
- [Interview Questions](#interview-questions)

---

## What is Rate Limiting?

**Rate limiting** controls the number of requests a client can make to an API within a specific time period.

### Simple Analogy

Think of a **water fountain** at a park:

```
Without rate limiting:
- Everyone rushes at once
- Water pressure drops
- Fountain breaks
- Nobody gets water

With rate limiting:
- Maximum 10 people per minute
- Everyone gets fair access
- Fountain stays functional
- System stays healthy
```

### Technical Definition

```
Rate Limiting = Request Count ÷ Time Window

Examples:
- 100 requests per minute per IP address
- 5 login attempts per 5 minutes per user
- 50 booking requests per minute per user
```

### Why It's Essential

```
Scenario: E-commerce site during Black Friday sale

Without Rate Limiting:
- Bot sends 100,000 requests/second
- Server CPU: 100%
- Database connections exhausted
- Site crashes for everyone
- Legitimate customers can't buy anything

With Rate Limiting:
- Bot limited to 100 requests/minute
- Server CPU: 30%
- Database healthy
- Legitimate customers shop normally
- Sales continue successfully
```

---

## Why Rate Limiting Matters

### 1. Prevent Abuse & DDoS Attacks

**Distributed Denial of Service (DDoS):**

```
Attack scenario:
- Botnet with 10,000 compromised computers
- Each sends 100 requests/second
- Total: 1,000,000 requests/second
- Server overwhelmed and crashes

With rate limiting:
- Limit each IP to 100 requests/minute
- 10,000 IPs × 100 req/min = 1,000,000 req/min (16,666 req/sec)
- Server capacity: 50,000 req/sec
- ✅ Attack mitigated!
```

### 2. Fair Resource Allocation

**Noisy neighbor problem:**

```
Scenario: 1000 users sharing API

Without rate limiting:
User A (bot):        900,000 requests/hour (90%)
Users B-Z (humans):  100,000 requests/hour (10% total)

Result: Humans experience slowness

With rate limiting (100 req/min per user):
User A:   6,000 requests/hour (max)
User B-Z: 6,000 requests/hour each
Result: Fair access for everyone
```

### 3. Cost Control

**Cloud hosting costs:**

```
Pricing: $0.10 per 1,000 API requests

Without rate limiting:
- Bot attack: 100M requests/day
- Cost: $10,000/day
- Monthly: $300,000

With rate limiting (100 req/min per IP):
- Normal traffic: 1M requests/day
- Cost: $100/day
- Monthly: $3,000

Savings: $297,000/month!
```

### 4. Protect Downstream Services

**Cascading failures:**

```
API Gateway (no rate limiting)
    ↓
BookingService (receives 10,000 req/sec)
    ↓
Database (max 1,000 connections)
    ↓
Connection pool exhausted
    ↓
BookingService crashes
    ↓
PaymentService can't reach BookingService
    ↓
Entire system down!

With rate limiting at Gateway:
✅ Gateway blocks excessive requests
✅ BookingService receives manageable load
✅ Database stays healthy
✅ System remains stable
```

### 5. Enforce SLA Tiers

**Monetization strategy:**

```
Free Tier:    100 requests/hour   → Rate limit: 100 req/hr
Basic Tier:   1,000 requests/hour → Rate limit: 1,000 req/hr ($10/mo)
Premium Tier: 10,000 requests/hour → Rate limit: 10,000 req/hr ($100/mo)
Enterprise:   Unlimited           → No rate limit ($1,000/mo)
```

---

## Rate Limiting Algorithms

### 1. Fixed Window

**How it works:**

```
Time divided into fixed windows (e.g., 1:00-1:01, 1:01-1:02)

Window 1 (1:00:00 - 1:00:59):
- Limit: 100 requests
- Count: 0 → 1 → 2 → ... → 100
- Request 101: REJECTED

Window 2 (1:01:00 - 1:01:59):
- Counter resets to 0
- Limit: 100 requests again
```

**Example:**

```
Timeline:
1:00:00 → Request #1   ✅ (count: 1/100)
1:00:30 → Request #50  ✅ (count: 50/100)
1:00:58 → Request #100 ✅ (count: 100/100)
1:00:59 → Request #101 ❌ (RATE LIMIT EXCEEDED)
1:01:00 → Request #102 ✅ (new window, count: 1/100)
```

**Pros:**
- ✅ Simple to implement
- ✅ Low memory usage (one counter per client)
- ✅ Fast performance

**Cons:**
- ❌ **Boundary burst problem:**
  ```
  1:00:50 → 100 requests ✅ (end of window 1)
  1:01:00 → 100 requests ✅ (start of window 2)
  
  Result: 200 requests in 10 seconds!
  ```

**Use cases:**
- Global API limits
- Read-only endpoints
- Non-critical operations

### 2. Sliding Window

**How it works:**

```
Rolling time window moves with each request

Window: 60 seconds rolling
At 1:00:35:
- Count requests from 0:59:35 to 1:00:35
- Requests outside this window are dropped

At 1:00:36:
- Count requests from 0:59:36 to 1:00:36
- Window "slides" forward by 1 second
```

**Example:**

```
Timeline (5 req/min limit):
1:00:00 → Req #1 ✅ (window: 0:59:00-1:00:00, count: 1/5)
1:00:10 → Req #2 ✅ (window: 0:59:10-1:00:10, count: 2/5)
1:00:20 → Req #3 ✅ (count: 3/5)
1:00:30 → Req #4 ✅ (count: 4/5)
1:00:40 → Req #5 ✅ (count: 5/5)
1:00:50 → Req #6 ❌ (RATE LIMIT: 5/5)
1:01:01 → Req #7 ✅ (Req #1 rolled out of window, count: 5/5)
```

**Pros:**
- ✅ No boundary burst problem
- ✅ More accurate rate limiting
- ✅ Smoother traffic distribution

**Cons:**
- ❌ More complex implementation
- ❌ Higher memory usage (stores request timestamps)
- ❌ Slower (must scan timestamps)

**Use cases:**
- Authentication endpoints (prevent brute force)
- Critical operations
- APIs requiring precise rate limiting

### 3. Token Bucket

**How it works:**

```
Imagine a bucket that holds tokens:
- Bucket capacity: 50 tokens
- Refill rate: +10 tokens per minute
- Each request consumes 1 token

Timeline:
Start:  50 tokens (bucket full)
        ↓
Request: 49 tokens (consumed 1)
        ↓
Wait 6 seconds: 50 tokens (refilled 1)
        ↓
50 requests in 1 second: 0 tokens (all consumed)
        ↓
Next request: REJECTED (no tokens)
        ↓
Wait 60 seconds: 10 tokens (refilled 10)
        ↓
10 requests: ✅ Allowed
```

**Visual representation:**

```
Bucket capacity: 50 tokens
Refill: +10 tokens/min (1 token every 6 seconds)

Time   Tokens  Action
---------------------------------
0:00   50      Start (full)
0:01   49      Request (consume 1)
0:02   48      Request
0:06   49      (refilled 1 token)
0:10   50      (refilled to max)
0:10   45      5 requests (burst)
0:15   45      (refill +1, but under max)
0:60   50      (refilled to max over time)
```

**Pros:**
- ✅ Allows bursts (use accumulated tokens)
- ✅ Smooth traffic over time
- ✅ Flexible (capacity + refill rate)
- ✅ Better user experience

**Cons:**
- ❌ More complex than fixed window
- ❌ Harder to explain to users

**Use cases:**
- Booking/reservation systems (burst of bookings, then idle)
- Media upload (burst of uploads)
- APIs with variable request patterns

### 4. Concurrency Limiter

**How it works:**

```
Limits concurrent (simultaneous) requests

Max concurrency: 10

Scenario:
- 10 requests in progress → New request REJECTED
- 1 request completes → Slot available
- New request → ACCEPTED (now 10 in progress again)
```

**Example:**

```
Timeline:
10 requests start (all long-running) → Concurrency: 10/10
Request #11 arrives → REJECTED (at limit)
Request #3 completes → Concurrency: 9/10
Request #11 retries → ACCEPTED (now 10/10)
```

**Pros:**
- ✅ Protects against slow clients
- ✅ Prevents resource exhaustion
- ✅ Good for long-running operations

**Cons:**
- ❌ Doesn't limit request rate (only simultaneous)
- ❌ Can starve clients with fast requests

**Use cases:**
- Payment processing (slow external API calls)
- File uploads (long-running)
- Database queries (concurrent connection limit)

### Comparison Table

| Algorithm | Burst Handling | Accuracy | Complexity | Memory | Use Case |
|-----------|----------------|----------|------------|--------|----------|
| Fixed Window | ❌ Boundary burst | Medium | Low | Low | Global limits |
| Sliding Window | ✅ Good | High | High | High | Auth endpoints |
| Token Bucket | ✅ Best | High | Medium | Medium | Booking systems |
| Concurrency | N/A | N/A | Low | Low | Long operations |

---

## Token Bucket Algorithm

Deep dive into our primary algorithm.

### Core Concepts

**Token Bucket = Capacity + Replenishment Rate**

```
Bucket:
┌─────────────────────┐
│  Token capacity: 50 │ ← Maximum tokens bucket can hold
│  Current tokens: 35 │ ← Tokens available now
│  Refill rate: 10/min│ ← Tokens added per minute
└─────────────────────┘

Request arrives:
- Check: tokens > 0?
  - Yes: Consume 1 token, allow request
  - No: Reject request (429 Too Many Requests)

Background process:
- Every minute: tokens += 10 (up to max 50)
```

### Mathematical Model

```
Parameters:
- C = Bucket capacity (50 tokens)
- R = Refill rate (10 tokens/minute)
- T = Current tokens available

Request handling:
if T > 0:
    T = T - 1
    return ALLOW
else:
    return REJECT (429)

Refill (every minute):
T = min(T + R, C)
```

### Burst Behavior

**Scenario 1: Burst after idle period**

```
User idle for 10 minutes:
- Bucket refills: 10 tokens/min × 10 min = 100 tokens
- But capacity = 50, so bucket = 50 (capped)
- User sends 50 requests instantly → All accepted!
- Next request → Rejected (tokens = 0)
- User must wait for refill
```

**Scenario 2: Sustained high load**

```
User sends requests constantly:
- Rate: 15 requests/minute
- Refill: 10 tokens/minute
- Net: -5 tokens/minute
- Eventually: tokens = 0
- User limited to 10 req/min (refill rate)

Result: Allows bursts, smooths to sustained rate
```

### Why Token Bucket for Bookings?

```
Booking system characteristics:
1. Bursty traffic:
   - Event announcement → Flood of booking requests
   - 100 requests in first minute
   - Then quiet for hours

2. Token bucket fits perfectly:
   - Capacity: 50 tokens
   - Refill: 10 tokens/minute
   - Burst: User can book 50 events quickly
   - Sustained: Limited to 10 bookings/minute
   - Fair: Tokens accumulate during idle time
```

---

## Multi-Policy Configuration

Our implementation uses **6 different policies** for different use cases.

### Policy 1: Global (Fixed Window)

**Purpose:** Protect entire API from excessive traffic per IP

```csharp
options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
{
    var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    
    return RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: $"global_{ipAddress}",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100,                      // 100 requests
            Window = TimeSpan.FromMinutes(1),        // per minute
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 5                           // Queue 5 requests when at limit
        });
});
```

**Characteristics:**
- Partition by: IP address
- Algorithm: Fixed window
- Limit: 100 requests/minute
- Queue: 5 requests

**Use case:** General API protection

### Policy 2: Auth (Sliding Window)

**Purpose:** Prevent brute force login attacks

```csharp
options.AddPolicy("auth", context =>
{
    var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    
    return RateLimitPartition.GetSlidingWindowLimiter(
        partitionKey: $"auth_{ipAddress}",
        factory: _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 5,                        // 5 login attempts
            Window = TimeSpan.FromMinutes(5),        // per 5 minutes
            SegmentsPerWindow = 5,                   // 5 segments (1 min each)
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0                           // No queueing (immediate reject)
        });
});
```

**Characteristics:**
- Partition by: IP address
- Algorithm: Sliding window
- Limit: 5 attempts/5 minutes
- Queue: 0 (strict)

**Use case:** Login/registration endpoints

**Why sliding window?** Prevents boundary burst:

```
Fixed window attack:
1:04:55 → 5 attempts ✅ (end of window)
1:05:00 → 5 attempts ✅ (new window)
Result: 10 attempts in 5 seconds!

Sliding window:
1:04:55 → 5 attempts ✅
1:05:00 → Next attempt ❌ (still 5 attempts in last 5 min)
Must wait until 1:09:55 for attempts to roll out
```

### Policy 3: Booking (Token Bucket)

**Purpose:** Allow burst bookings, smooth over time

```csharp
options.AddPolicy("booking", context =>
{
    var userId = context.User.Identity?.Name 
                 ?? context.Connection.RemoteIpAddress?.ToString() 
                 ?? "anonymous";
    
    return RateLimitPartition.GetTokenBucketLimiter(
        partitionKey: $"booking_{userId}",
        factory: _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 50,                         // Bucket capacity: 50 tokens
            TokensPerPeriod = 10,                    // Refill 10 tokens
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),  // per minute
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 3                           // Queue 3 requests
        });
});
```

**Characteristics:**
- Partition by: User ID (or IP if unauthenticated)
- Algorithm: Token bucket
- Capacity: 50 tokens
- Refill: 10 tokens/minute
- Queue: 3 requests

**Use case:** Booking creation/modification

**Burst example:**

```
User creates 40 bookings in 10 seconds:
- Tokens before: 50
- Tokens after: 10
- All 40 requests: ✅ Accepted

User tries 15 more bookings immediately:
- First 10: ✅ Accepted (tokens: 10 → 0)
- Next 5: ❌ Rejected (no tokens)

User waits 1 minute:
- Tokens refilled: 0 → 10
- Next 10 requests: ✅ Accepted
```

### Policy 4: Payment (Concurrency Limiter)

**Purpose:** Limit concurrent payment processing

```csharp
options.AddPolicy("payment", context =>
{
    var userId = context.User.Identity?.Name 
                 ?? context.Connection.RemoteIpAddress?.ToString() 
                 ?? "anonymous";
    
    return RateLimitPartition.GetConcurrencyLimiter(
        partitionKey: $"payment_{userId}",
        factory: _ => new ConcurrencyLimiterOptions
        {
            PermitLimit = 10,                        // Max 10 concurrent payments
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 2                           // Queue 2 requests
        });
});
```

**Characteristics:**
- Partition by: User ID
- Algorithm: Concurrency limiter
- Limit: 10 concurrent requests
- Queue: 2 requests

**Use case:** Payment processing (calls external payment gateway)

**Why concurrency limiter?**

```
Payment processing is slow (external API):
- Average payment: 3 seconds
- Without limit: User submits 50 payments simultaneously
- Result: 50 concurrent HTTP calls to payment gateway
- Payment gateway rate limits us → Failures

With concurrency limit (10):
- First 10 payments: Processing
- Next 2 payments: Queued
- Remaining 38: Rejected (429)
- As payments complete, queue processes
- Result: Controlled load on payment gateway
```

### Policy 5: Read (Fixed Window)

**Purpose:** Higher limits for read-only operations

```csharp
options.AddPolicy("read", context =>
{
    var userId = context.User.Identity?.Name 
                 ?? context.Connection.RemoteIpAddress?.ToString() 
                 ?? "anonymous";
    
    return RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: $"read_{userId}",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 200,                       // 200 requests
            Window = TimeSpan.FromMinutes(1),        // per minute
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 10                          // Queue 10 requests
        });
});
```

**Characteristics:**
- Partition by: User ID
- Algorithm: Fixed window
- Limit: 200 requests/minute
- Queue: 10 requests

**Use case:** GET endpoints (bookings list, payments history)

**Rationale:** Reads are less expensive than writes, allow more requests

### Policy 6: Premium (Fixed Window)

**Purpose:** Higher limits for premium users (future)

```csharp
options.AddPolicy("premium", context =>
{
    var userId = context.User.Identity?.Name ?? "anonymous";
    
    return RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: $"premium_{userId}",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 500,                       // 500 requests
            Window = TimeSpan.FromMinutes(1),        // per minute
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 20                          // Queue 20 requests
        });
});
```

**Characteristics:**
- Partition by: User ID
- Algorithm: Fixed window
- Limit: 500 requests/minute (5x normal)
- Queue: 20 requests

**Use case:** Premium subscription tier (not yet implemented)

### Policy Comparison

| Policy | Algorithm | Partition | Limit | Window | Queue | Use Case |
|--------|-----------|-----------|-------|--------|-------|----------|
| Global | Fixed | IP | 100/min | 1 min | 5 | API protection |
| Auth | Sliding | IP | 5/5min | 5 min | 0 | Brute force prevention |
| Booking | Token Bucket | User | 50 cap, +10/min | 1 min | 3 | Burst bookings |
| Payment | Concurrency | User | 10 concurrent | N/A | 2 | Payment processing |
| Read | Fixed | User | 200/min | 1 min | 10 | GET requests |
| Premium | Fixed | User | 500/min | 1 min | 20 | Premium tier |

---

## Implementation in API Gateway

### Complete Implementation

**Location:** `src/ApiGateway/Middleware/RateLimitMiddleware.cs`

```csharp
public static IServiceCollection AddCustomRateLimiting(
    this IServiceCollection services, 
    IConfiguration configuration)
{
    var rateLimitConfig = configuration.GetSection("RateLimiting");
    
    services.AddRateLimiter(options =>
    {
        // Global rejection handler
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        
        options.OnRejected = async (context, cancellationToken) =>
        {
            var endpoint = context.HttpContext.Request.Path;
            var ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userId = context.HttpContext.User.Identity?.Name ?? "anonymous";
            
            Log.Warning(
                "Rate limit exceeded for {Endpoint} by {UserId} from {IpAddress}",
                endpoint, userId, ipAddress);
            
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            
            // Add Retry-After header
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                context.HttpContext.Response.Headers.RetryAfter = 
                    ((int)retryAfter.TotalSeconds).ToString();
            }
            
            // Add rate limit headers
            context.HttpContext.Response.Headers["X-RateLimit-Remaining"] = "0";
            
            var retryAfterSeconds = retryAfter != default ? (int)retryAfter.TotalSeconds : 60;
            
            await context.HttpContext.Response.WriteAsJsonAsync(new
            {
                error = "Rate limit exceeded",
                message = "Too many requests. Please try again later.",
                retryAfter = retryAfterSeconds,
                endpoint = endpoint.ToString(),
                timestamp = DateTime.UtcNow
            }, cancellationToken);
        };
        
        // Configure policies (shown above)
        // ...
    });
    
    return services;
}
```

### Configuration File

**Location:** `src/ApiGateway/appsettings.json`

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
      "SegmentsPerWindow": 5,
      "QueueLimit": 0
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
      "WindowMinutes": 1,
      "QueueLimit": 10
    },
    "PremiumPolicy": {
      "PermitLimit": 500,
      "WindowMinutes": 1,
      "QueueLimit": 20
    }
  }
}
```

### Middleware Registration

**Location:** `src/ApiGateway/Program.cs`

```csharp
// Add rate limiting
builder.Services.AddCustomRateLimiting(builder.Configuration);

// Use rate limiting (before YARP)
app.UseRateLimiter();
app.MapReverseProxy();
```

### Applying Policies to Routes

```csharp
// Apply policy via endpoint metadata
app.MapGet("/api/bookings", () => "...")
    .RequireRateLimiting("booking");

app.MapPost("/api/users/login", () => "...")
    .RequireRateLimiting("auth");

app.MapGet("/api/bookings/{id}", () => "...")
    .RequireRateLimiting("read");
```

---

## Client Identification Strategies

### Strategy 1: IP Address

**Use:** Anonymous users, DDoS protection

```csharp
var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
partitionKey: $"global_{ipAddress}"
```

**Pros:**
- ✅ Works for unauthenticated users
- ✅ Simple to implement

**Cons:**
- ❌ NAT/proxy issues (multiple users share IP)
- ❌ Can't track individual users
- ❌ Spoofable (VPN/proxy)

### Strategy 2: User ID

**Use:** Authenticated users, per-user limits

```csharp
var userId = context.User.Identity?.Name 
             ?? context.User.FindFirst("sub")?.Value 
             ?? "anonymous";
partitionKey: $"booking_{userId}"
```

**Pros:**
- ✅ Accurate user tracking
- ✅ Fair allocation per user
- ✅ Works across devices/IPs

**Cons:**
- ❌ Requires authentication
- ❌ Doesn't work for public endpoints

### Strategy 3: Hybrid (User ID + IP Fallback)

**Use:** Most endpoints (our implementation)

```csharp
var identifier = context.User.Identity?.Name 
                 ?? context.Connection.RemoteIpAddress?.ToString() 
                 ?? "anonymous";
partitionKey: $"booking_{identifier}"
```

**Benefits:**
- ✅ Authenticated users: Per-user limits
- ✅ Anonymous users: Per-IP limits
- ✅ Best of both worlds

### Strategy 4: API Key

**Use:** External API clients

```csharp
var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault() ?? "unknown";
partitionKey: $"api_{apiKey}"
```

**Benefits:**
- ✅ Track third-party clients
- ✅ Different limits per client
- ✅ Billing integration

### Strategy 5: Session ID

**Use:** Track across multiple requests in session

```csharp
var sessionId = context.Session.Id;
partitionKey: $"session_{sessionId}"
```

**Benefits:**
- ✅ Tracks user journey
- ✅ Works before authentication

---

## Response Handling

### 429 Too Many Requests

**Response structure:**

```json
{
  "error": "Rate limit exceeded",
  "message": "Too many requests. Please try again later.",
  "retryAfter": 45,
  "endpoint": "/api/bookings",
  "timestamp": "2024-11-12T14:30:00Z"
}
```

**HTTP headers:**

```
HTTP/1.1 429 Too Many Requests
Retry-After: 45
X-RateLimit-Limit: 50
X-RateLimit-Remaining: 0
X-RateLimit-Reset: 1699804800
Content-Type: application/json
```

### Retry-After Header

**Tells client when to retry:**

```
Retry-After: 60  (seconds)

Client logic:
if (response.status == 429) {
    const retryAfter = response.headers['Retry-After'];
    await sleep(retryAfter * 1000);
    retry();
}
```

### Rate Limit Headers

**Informational headers for successful requests:**

```
X-RateLimit-Limit: 50         (max requests allowed)
X-RateLimit-Remaining: 23     (requests left in window)
X-RateLimit-Reset: 1699804800 (Unix timestamp when limit resets)
```

**Client can proactively slow down:**

```javascript
const remaining = response.headers['X-RateLimit-Remaining'];
if (remaining < 5) {
    console.warn('Approaching rate limit, slowing down...');
    await sleep(1000);  // Add delay between requests
}
```

---

## Testing Rate Limits

### Test 1: Hit Global Limit

```bash
# Send 105 requests quickly (global limit: 100/min)
for i in {1..105}; do
    curl -s http://localhost:5000/api/health
done

# Expected:
# Requests 1-100: 200 OK
# Requests 101-105: 429 Too Many Requests
```

### Test 2: Auth Brute Force Protection

```bash
# Try 10 login attempts (auth limit: 5/5min)
for i in {1..10}; do
    curl -X POST http://localhost:5001/api/users/login \
        -H "Content-Type: application/json" \
        -d '{"username":"test","password":"wrong"}'
done

# Expected:
# Attempts 1-5: 401 Unauthorized (wrong password)
# Attempts 6-10: 429 Too Many Requests (rate limited)
```

### Test 3: Token Bucket Burst

```bash
# Get auth token
TOKEN=$(curl -s -X POST http://localhost:5001/api/users/login \
    -d '{"username":"john_doe","password":"pass123"}' | jq -r '.token')

# Send 60 booking requests rapidly (limit: 50 capacity, +10/min)
for i in {1..60}; do
    curl -s -X POST http://localhost:5000/api/bookings \
        -H "Authorization: Bearer $TOKEN" \
        -d '{"eventName":"Concert","eventDate":"2024-12-01"}' &
done
wait

# Expected:
# Requests 1-50: 201 Created (burst)
# Requests 51-60: 429 Too Many Requests (bucket empty)

# Wait 1 minute, try 15 more
sleep 60
for i in {1..15}; do
    curl -s -X POST http://localhost:5000/api/bookings \
        -H "Authorization: Bearer $TOKEN" \
        -d '{"eventName":"Concert2","eventDate":"2024-12-02"}' &
done
wait

# Expected:
# Requests 1-10: 201 Created (bucket refilled 10 tokens)
# Requests 11-15: 429 Too Many Requests
```

### Test 4: Verify Retry-After

```bash
# Trigger rate limit and check Retry-After header
for i in {1..110}; do
    curl -s -i http://localhost:5000/api/health | grep -i retry-after
done

# Expected output:
# (first 100 requests: no header)
# Retry-After: 60
# Retry-After: 58
# Retry-After: 56
# (decrements as time passes)
```

### Test 5: Concurrent Payment Limit

```bash
# Start 15 payment requests simultaneously (limit: 10 concurrent)
for i in {1..15}; do
    curl -s -X POST http://localhost:5000/api/payments \
        -H "Authorization: Bearer $TOKEN" \
        -d '{"bookingId":"abc123","amount":100}' &
done

# Expected:
# First 10: Processing (concurrent)
# Next 2: Queued
# Last 3: 429 Too Many Requests
```

---

## Common Pitfalls

### 1. Shared IP Address (NAT/Proxy)

**Problem:**

```
Office network:
- 100 employees
- Single public IP: 203.0.113.5
- Rate limit: 100 req/min per IP

Result:
- All 100 employees share 100 req/min limit
- Each gets ~1 req/min (not fair!)
```

**Solution:**

```csharp
// Use user ID instead of IP for authenticated users
var identifier = context.User.Identity?.Name  // Prefer user ID
                 ?? context.Connection.RemoteIpAddress?.ToString();  // Fallback to IP
```

### 2. Forgetting to Handle 429

**Bad client code:**

```javascript
// ❌ No retry logic
const response = await fetch('/api/bookings');
if (response.ok) {
    return response.json();
}
// User sees error, doesn't know to retry
```

**Good client code:**

```javascript
// ✅ Automatic retry with backoff
async function fetchWithRetry(url, maxRetries = 3) {
    for (let i = 0; i < maxRetries; i++) {
        const response = await fetch(url);
        
        if (response.status === 429) {
            const retryAfter = response.headers.get('Retry-After') || 60;
            console.log(`Rate limited, retrying after ${retryAfter}s...`);
            await sleep(retryAfter * 1000);
            continue;  // Retry
        }
        
        return response;
    }
    throw new Error('Max retries exceeded');
}
```

### 3. Rate Limiting After Authentication

**Wrong order:**

```csharp
app.UseRateLimiter();      // Rate limit BEFORE authentication
app.UseAuthentication();
app.UseAuthorization();
```

**Problem:** Attacker can exhaust rate limit for an IP, blocking legitimate users behind same IP.

**Better approach:**

```csharp
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();      // Rate limit AFTER authentication (per-user)
```

**Best approach:** Use both!

```csharp
// Global rate limit (IP-based, prevent DDoS)
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Per-user rate limits (applied at controller/route level)
```

### 4. Rate Limiting Expensive Operations Only

**Mistake:**

```
Rate limit: POST /api/bookings (expensive)
No limit: GET /api/bookings (cheap)

Attack:
- Attacker floods GET requests
- Database still overwhelmed from read queries
- System crashes
```

**Solution:** Rate limit ALL endpoints (different limits for read vs write).

### 5. Not Monitoring Rate Limit Hits

**Problem:** Rate limits hit frequently, but no alerts or monitoring.

**Solution:**

```csharp
options.OnRejected = async (context, cancellationToken) =>
{
    // Log to structured logging
    Log.Warning("Rate limit exceeded for {Endpoint}", endpoint);
    
    // Increment metrics counter
    RateLimitHits.Inc();
    
    // Alert if threshold exceeded
    if (RateLimitHits.Value > 1000)
    {
        await _alertService.SendAlert("High rate limit hits detected");
    }
};
```

---

## Best Practices

### 1. Use Different Policies for Different Endpoints

```
Authentication endpoints: Strict (5/5min sliding window)
Read endpoints: Permissive (200/min fixed window)
Write endpoints: Moderate (50 token bucket)
Payment endpoints: Concurrency limit (10 concurrent)
```

### 2. Provide Clear Error Messages

```json
// ❌ Bad
{ "error": "Too many requests" }

// ✅ Good
{
  "error": "Rate limit exceeded",
  "message": "You've exceeded the limit of 100 requests per minute. Please wait 45 seconds before trying again.",
  "retryAfter": 45,
  "limit": 100,
  "window": "1 minute",
  "documentation": "https://docs.example.com/rate-limits"
}
```

### 3. Log Rate Limit Events

```csharp
Log.Warning(
    "Rate limit exceeded: {UserId} on {Endpoint} from {IpAddress}",
    userId, endpoint, ipAddress);

// Enables:
// - Detect abuse patterns
// - Identify legitimate users hitting limits
// - Adjust limits based on usage
```

### 4. Use Appropriate Algorithms

```
Fixed Window: Global API protection (simple, fast)
Sliding Window: Authentication (no burst attacks)
Token Bucket: Bursty workloads (bookings, uploads)
Concurrency: External API calls (slow operations)
```

### 5. Implement Graceful Degradation

```csharp
// If rate limit service fails, don't block all traffic
try
{
    await rateLimiter.CheckRateLimitAsync(userId);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Rate limiter failed, allowing request");
    // Fail open: allow request
}
```

### 6. Consider Premium Tiers

```
Free:     100 requests/hour
Basic:    1,000 requests/hour ($10/mo)
Premium:  10,000 requests/hour ($100/mo)
Enterprise: Unlimited ($Custom)
```

### 7. Monitor and Alert

```
Metrics to track:
- Rate limit hits per policy
- Top users hitting limits
- 429 response rate
- Retry-After values

Alert thresholds:
- >10% of requests rate limited → Investigate
- Single user >1000 429s/day → Potential abuse
```

### 8. Document Rate Limits

```markdown
# API Rate Limits

## Global Limits
- 100 requests per minute per IP address

## Authentication
- 5 login attempts per 5 minutes per IP
- Use Retry-After header value

## Bookings
- 50 requests burst capacity
- Refills at 10 requests per minute
- Per authenticated user

## Payments
- Maximum 10 concurrent payment requests per user

## Premium Users
- 5x higher limits on all endpoints
- Contact sales for custom limits
```

---

## Interview Questions

### Q1: Explain the difference between fixed window and token bucket algorithms.

**Answer:**

**Fixed Window:**
- Time divided into fixed windows (e.g., 1:00-1:01)
- Counter resets at boundary
- Simple but has boundary burst problem (can send 2x limit at window boundary)

**Token Bucket:**
- Bucket holds tokens, refills continuously
- Allows bursts (use accumulated tokens)
- Smooths traffic over time
- Better for variable workloads

**Example:**
Fixed: 100 req/min, can send 100 at 1:00:59 and 100 at 1:01:00 (200 in 2 sec)
Token Bucket: 50 capacity + 10/min refill, can burst 50 immediately, then limited to refill rate

---

### Q2: Why use sliding window for authentication endpoints?

**Answer:**

Authentication endpoints are vulnerable to brute force attacks. Sliding window prevents boundary exploitation:

**Attack with fixed window:**
```
1:04:55 → 5 attempts (end of window)
1:05:00 → 5 attempts (new window)
Result: 10 attempts in 5 seconds
```

**Protection with sliding window:**
```
1:04:55 → 5 attempts
1:05:00 → Attempt #6 rejected (still 5 in last 5 minutes)
Must wait until 1:09:55
```

Sliding window tracks actual time elapsed, not window boundaries, providing better security.

---

### Q3: How do you handle rate limiting for users behind NAT/proxy?

**Answer:**

Use hybrid approach:
1. **Authenticated users:** Rate limit by user ID (accurate, fair)
2. **Anonymous users:** Rate limit by IP (best available option)

```csharp
var identifier = context.User.Identity?.Name  // User ID if authenticated
                 ?? context.Connection.RemoteIpAddress?.ToString();  // IP fallback
```

**Additional strategies:**
- Check `X-Forwarded-For` header (but can be spoofed)
- Require authentication for high-volume operations
- Use fingerprinting (browser, device info)
- Implement CAPTCHA after multiple 429s

---

### Q4: What's the purpose of QueueLimit in rate limiting?

**Answer:**

`QueueLimit` allows requests to wait when rate limit is reached, instead of immediate rejection:

```
Without queue (QueueLimit = 0):
- 101st request → 429 immediately

With queue (QueueLimit = 5):
- 101st-105th requests → Queued
- As tokens refill, queued requests processed
- 106th request → 429
```

**Benefits:**
- Better user experience (automatic retry)
- Reduces client retry logic complexity
- Smooths traffic spikes

**When to use:**
- QueueLimit = 0: Strict endpoints (auth, payments)
- QueueLimit > 0: User-facing APIs (better UX)

---

### Q5: Design a rate limiting system for an e-commerce site during a flash sale.

**Answer:**

**Challenge:** 10,000 users trying to buy 100 items simultaneously

**Strategy:**

```
Layer 1: Global Protection (IP-based)
- Fixed window: 50 requests/min per IP
- Prevents DDoS

Layer 2: Authenticated Users (User-based)
- Token bucket: 20 capacity, +5/min refill
- Allows burst, then throttles

Layer 3: Critical Endpoints (Purchase)
- Concurrency limiter: 2 concurrent purchases per user
- Prevents duplicate orders
- Sliding window: 5 purchases/5 minutes
- Prevents inventory hoarding

Layer 4: Inventory Reservation
- Reserve item for 10 minutes on "Add to Cart"
- Must complete purchase within window
- Auto-release if abandoned

Special handling:
- Premium members: 2x higher limits
- Use queue system for fairness (FIFO)
- Implement waiting room if demand > capacity
```

**Result:** Fair access, prevent bots, protect inventory, maintain system stability

---

### Q6: Implement exponential backoff for rate-limited requests.

**Answer:**

```javascript
async function fetchWithExponentialBackoff(url, maxRetries = 5) {
    let retries = 0;
    let delay = 1000; // Start with 1 second
    
    while (retries < maxRetries) {
        const response = await fetch(url);
        
        if (response.ok) {
            return response.json();
        }
        
        if (response.status === 429) {
            // Check Retry-After header
            const retryAfter = response.headers.get('Retry-After');
            
            if (retryAfter) {
                // Use server-provided delay
                delay = parseInt(retryAfter) * 1000;
            } else {
                // Exponential backoff: 1s, 2s, 4s, 8s, 16s
                delay = Math.min(delay * 2, 30000); // Max 30 seconds
            }
            
            console.log(`Rate limited, retrying in ${delay/1000}s...`);
            await sleep(delay);
            retries++;
            continue;
        }
        
        throw new Error(`Request failed: ${response.status}`);
    }
    
    throw new Error('Max retries exceeded');
}

function sleep(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}
```

---

### Q7: How do you test rate limiting in automated tests?

**Answer:**

```csharp
[Fact]
public async Task GlobalRateLimiter_RejectsAfterLimit()
{
    // Arrange
    var client = _factory.CreateClient();
    
    // Act: Send 105 requests (limit: 100)
    var responses = new List<HttpResponseMessage>();
    for (int i = 0; i < 105; i++)
    {
        responses.Add(await client.GetAsync("/api/health"));
    }
    
    // Assert
    var successCount = responses.Count(r => r.IsSuccessStatusCode);
    var rateLimitedCount = responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests);
    
    Assert.Equal(100, successCount);
    Assert.Equal(5, rateLimitedCount);
    
    // Verify Retry-After header
    var rateLimitedResponse = responses.First(r => r.StatusCode == HttpStatusCode.TooManyRequests);
    Assert.True(rateLimitedResponse.Headers.Contains("Retry-After"));
}

[Fact]
public async Task TokenBucket_AllowsBurstThenThrottles()
{
    // Arrange: Token bucket with 50 capacity, +10/min refill
    var client = await GetAuthenticatedClient();
    
    // Act: Send 60 requests rapidly
    var tasks = Enumerable.Range(1, 60)
        .Select(_ => client.PostAsync("/api/bookings", CreateBookingContent()))
        .ToList();
    
    var responses = await Task.WhenAll(tasks);
    
    // Assert: First 50 succeed (burst), next 10 fail
    var successCount = responses.Count(r => r.IsSuccessStatusCode);
    Assert.InRange(successCount, 50, 53); // Allow for race conditions
    
    // Wait for refill (1 minute = 10 tokens)
    await Task.Delay(TimeSpan.FromMinutes(1));
    
    // Act: Send 15 more requests
    var responses2 = await SendRequests(client, 15);
    
    // Assert: 10 succeed (refilled), 5 fail
    var successCount2 = responses2.Count(r => r.IsSuccessStatusCode);
    Assert.InRange(successCount2, 10, 12);
}
```

---

## Summary

Rate limiting is essential for API protection and resource management:

**Key Algorithms:**
- **Fixed Window:** Simple, fast, boundary burst issue → Global protection
- **Sliding Window:** Accurate, no burst issue → Authentication
- **Token Bucket:** Allows bursts, smooth over time → Variable workloads
- **Concurrency:** Limits simultaneous requests → Slow operations

**Our Multi-Policy Approach:**
1. **Global (100/min):** IP-based, protects entire API
2. **Auth (5/5min):** IP-based sliding, prevents brute force
3. **Booking (50 cap, +10/min):** User-based token bucket, allows bursts
4. **Payment (10 concurrent):** User-based concurrency, protects external API
5. **Read (200/min):** User-based fixed, higher limits for reads
6. **Premium (500/min):** User-based fixed, monetization tier

**Best Practices:**
1. Use appropriate algorithm for each use case
2. Partition by user ID when possible, IP as fallback
3. Provide clear 429 responses with Retry-After
4. Log rate limit events for monitoring
5. Implement client-side retry with exponential backoff
6. Document limits clearly for API consumers
7. Monitor metrics and adjust limits as needed

Rate limiting protects your system from abuse while ensuring fair access for legitimate users!
