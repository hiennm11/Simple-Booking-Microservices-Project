# Rate Limiting - Quick Reference

## 🎯 Quick Policy Overview

| Policy | Limit | Window | Algorithm | Use Case |
|--------|-------|--------|-----------|----------|
| **Global** | 100 req/min | 1 min | Fixed Window | All endpoints (default) |
| **Auth** | 5 attempts | 5 min | Sliding Window | Login/register (brute force prevention) |
| **Booking** | 50 tokens, +10/min | Continuous | Token Bucket | Create/update bookings (allows bursts) |
| **Payment** | 10 concurrent | N/A | Concurrency | Payment processing (prevents double charge) |
| **Read** | 200 req/min | 1 min | Fixed Window | GET endpoints (higher limit) |
| **Premium** | 500 req/min | 1 min | Fixed Window | Premium users (future) |

## 📊 Algorithm Comparison

```
Fixed Window:      [100 req]|[100 req]|[100 req]  ← Resets at boundaries
                   ━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                   1:00      1:01      1:02

Sliding Window:    [═════════100 req═════════]     ← Rolls continuously
                   ━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                   Prevents boundary bursts

Token Bucket:      🪣 [50 tokens] → Refill +10/min ← Allows bursts
                   Requests consume tokens
                   
Concurrency:       [Slot1][Slot2]...[Slot10]      ← Max simultaneous
                   Queue: [Req11][Req12]
```

## 🚦 Response Codes

| Code | Meaning | Action |
|------|---------|--------|
| 200 | Success | Continue normal operation |
| 429 | Rate Limited | Wait for `Retry-After` seconds |
| Headers | X-RateLimit-* | Check remaining quota |

## 📋 Response Headers

```http
X-RateLimit-Limit: 100           # Total allowed in window
X-RateLimit-Remaining: 45        # Remaining requests
X-RateLimit-Reset: 1699281600    # Unix timestamp of reset
X-RateLimit-Policy: booking      # Which policy was applied
Retry-After: 60                  # Seconds until retry (on 429)
```

## 🔍 Common Scenarios

### Scenario 1: User Makes Too Many Login Attempts

```
Request 1-5: ✅ 401 Unauthorized (wrong password, but not rate limited)
Request 6+:  ❌ 429 Too Many Requests
Wait 5 minutes → Try again ✅
```

### Scenario 2: User Creates Multiple Bookings

```
Bookings 1-50:  ✅ Created (consumes 50 tokens)
Booking 51:     ❌ 429 Too Many Requests (no tokens)
Wait 1 minute → +10 tokens → Bookings 51-60 ✅
```

### Scenario 3: Concurrent Payment Processing

```
Payments 1-10:  ✅ Processing (10 slots occupied)
Payment 11-12:  ⏳ Queued (2 queue slots)
Payment 13+:    ❌ 429 Too Many Requests (queue full)
Payment 1 completes → Payment 11 starts ✅
```

## ⚙️ Configuration Examples

### Increase Global Limit

```json
// appsettings.json
"RateLimiting": {
  "GlobalPolicy": {
    "PermitLimit": 200,  // Increased from 100
    "WindowMinutes": 1
  }
}
```

### Make Auth More Strict

```json
"AuthPolicy": {
  "PermitLimit": 3,      // Reduced from 5
  "WindowMinutes": 10    // Increased from 5
}
```

### Allow More Booking Burst

```json
"BookingPolicy": {
  "TokenLimit": 100,     // Increased from 50
  "TokensPerPeriod": 20, // Increased from 10
  "ReplenishmentMinutes": 1
}
```

## 🧪 Quick Tests

### Test Global Limit (CMD)

```cmd
REM Send 101 requests
for /L %i in (1,1,101) do @curl -s http://localhost:5000/health
```

### Test Auth Limit (PowerShell)

```powershell
# Send 6 login attempts
1..6 | ForEach-Object {
    Invoke-WebRequest -Uri "http://localhost:5000/api/users/login" `
        -Method POST `
        -Body '{"email":"test@test.com","password":"wrong"}' `
        -ContentType "application/json" `
        -SkipHttpErrorCheck
}
```

### Check Current Limits

```powershell
# Make a request and check headers
$response = Invoke-WebRequest http://localhost:5000/health
$response.Headers["X-RateLimit-Limit"]
$response.Headers["X-RateLimit-Remaining"]
```

## 📊 Seq Queries

### Rate Limit Violations (Last Hour)

```sql
select count(*) as Count, Endpoint, UserId
from stream
where @Message like '%Rate limit exceeded%'
  and @Timestamp > Now() - 1h
group by Endpoint, UserId
order by Count desc
```

### Top Offenders

```sql
select UserId, IpAddress, count(*) as Violations
from stream
where @Message like '%Rate limit exceeded%'
  and @Timestamp > Now() - 24h
group by UserId, IpAddress
order by Violations desc
limit 10
```

### Rate Limit Effectiveness

```sql
select 
  count(case when StatusCode = 429 then 1 end) as Blocked,
  count(case when StatusCode < 400 then 1 end) as Allowed
from stream
where @Timestamp > Now() - 1h
```

## 🛠️ Troubleshooting

### Issue: Users Complaining About 429

**Check:**
1. Are limits too strict?
2. Is one user making too many requests?
3. Are multiple users behind same IP (NAT)?

**Fix:**
```json
// Increase limit or change partition key
"GlobalPolicy": {
  "PermitLimit": 200  // Increased
}
```

### Issue: Rate Limiting Not Working

**Check:**
1. Is middleware registered? (`app.UseRateLimiter()`)
2. Is it in correct order? (after routing, before auth)
3. Is config loaded? (check appsettings.json)

**Verify:**
```powershell
# Test with curl
curl -i http://localhost:5000/health
# Look for X-RateLimit-* headers
```

### Issue: Some Requests Bypass Rate Limit

**Check:**
1. Partition key consistency
2. Multiple gateway instances without shared state
3. Clock synchronization

**Fix:**
```csharp
// Ensure consistent partition key
var key = !string.IsNullOrEmpty(userId) 
    ? $"user_{userId}" 
    : $"ip_{ipAddress}";
```

## 📚 More Information

- **Full Documentation:** [RATE_LIMITING_IMPLEMENTATION.md](RATE_LIMITING_IMPLEMENTATION.md)
- **Configuration:** `src/ApiGateway/appsettings.json`
- **Middleware Code:** `src/ApiGateway/Middleware/RateLimitMiddleware.cs`
- **Main README:** [README.md](../../README.md)

## 💡 Best Practices

✅ **DO:**
- Monitor rate limit violations in Seq
- Set alerts for unusual patterns
- Adjust limits based on real usage
- Use different policies for different endpoints
- Provide clear error messages with retry guidance

❌ **DON'T:**
- Set limits too low (frustrate users)
- Set limits too high (defeat purpose)
- Use same limits for all endpoints
- Ignore rate limit violations
- Deploy without testing
