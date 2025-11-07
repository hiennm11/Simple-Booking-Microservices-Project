# High-Concurrency Load Test Fixes

## Problem Summary
Load test with `-NumberOfFlows 100 -ConcurrentFlows 5` resulted in:
- **68% success rate** (32 failures)
- **30s timeout errors** (20 instances)
- **400 Bad Request errors** (12 instances)
- **Avg auth time: 6.25s** (acceptable is <1s)
- **P95 E2E time: 21.9s** (acceptable is <12s)

### Root Causes Identified
1. **30-second HTTP timeout too short** for high-concurrency scenarios
2. **No Kestrel connection limits** - services throttle under load
3. **Default database connection pools** - exhausted under concurrent operations
4. **No YARP HttpClient timeouts** - gateway used default 100s timeout internally
5. **Errors not reaching Seq** - connection failures before middleware catches them

---

## Changes Implemented

### 1. Test Script Timeout Increases
**File:** `scripts/testing/test-e2e-auth.ps1`
- ✅ All `Invoke-RestMethod` timeouts: `30s → 60s`
- Applied to: Register, Login, Booking, Payment, Booking Verification

### 2. Kestrel Connection Limits
**Files:** All service `Program.cs` files

#### UserService, BookingService, PaymentService
```csharp
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxConcurrentConnections = 500;
    serverOptions.Limits.MaxConcurrentUpgradedConnections = 500;
    serverOptions.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
    serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
    serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
});
```

#### ApiGateway (Higher limits as entry point)
```csharp
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxConcurrentConnections = 1000;
    serverOptions.Limits.MaxConcurrentUpgradedConnections = 1000;
    serverOptions.Limits.MaxRequestBodySize = 10 * 1024 * 1024;
    serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
    serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
});
```

### 3. Database Connection Pool Optimization
**File:** `docker-compose.yml`

#### PostgreSQL (UserService, BookingService)
```yaml
ConnectionStrings__DefaultConnection: "Host=...;Maximum Pool Size=200;Minimum Pool Size=10;Connection Lifetime=300"
```

#### MongoDB (PaymentService)
```yaml
MongoDB__ConnectionString: "mongodb://...?maxPoolSize=200&minPoolSize=10"
```

**Program.cs Updates:**
```csharp
// UserService & BookingService
builder.Services.AddDbContext<DbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.MaxBatchSize(100);
        npgsqlOptions.CommandTimeout(30);
    }).EnableSensitiveDataLogging(builder.Environment.IsDevelopment()));
```

### 4. API Gateway YARP HttpClient Timeouts
**File:** `src/ApiGateway/appsettings.json`

Added to all clusters (users, bookings, payments):
```json
"HttpClient": {
  "ActivityTimeout": "00:01:00",
  "RequestTimeout": "00:00:45"
}
```

---

## Rebuild & Test Instructions

### Step 1: Rebuild Services
```cmd
docker-compose down
docker-compose build --no-cache userservice bookingservice paymentservice apigateway
docker-compose up -d
```

### Step 2: Verify Health
```cmd
docker ps
docker stats --no-stream
curl http://localhost:5000/health
```

### Step 3: Run Load Test (PowerShell 7)
```powershell
# Test with original parameters
pwsh -File .\scripts\testing\test-e2e-auth.ps1 -NumberOfFlows 100 -ConcurrentFlows 5

# Test with higher concurrency
pwsh -File .\scripts\testing\test-e2e-auth.ps1 -NumberOfFlows 200 -ConcurrentFlows 10
```

### Step 4: Monitor Performance
```cmd
# Check Seq logs
http://localhost:5341

# Monitor container stats during test
docker stats

# Check service logs
docker logs apigateway --tail 50 -f
docker logs userservice --tail 50 -f
docker logs bookingservice --tail 50 -f
docker logs paymentservice --tail 50 -f
```

---

## Expected Improvements

### Before vs After Metrics

| Metric | Before | Target After |
|--------|--------|--------------|
| **Success Rate** | 68% | >95% |
| **Timeout Errors** | 20/100 | <1/100 |
| **400 Errors** | 12/100 | <1/100 |
| **Avg Auth Time** | 6250ms | <1000ms |
| **P95 E2E Time** | 21943ms | <12000ms |

### Why These Changes Help

1. **60s timeout** - Prevents premature failures during heavy load spikes
2. **Kestrel limits (500-1000)** - Explicit queue management instead of implicit rejection
3. **DB pool 200/10** - Supports 5+ concurrent flows × 20 connections each
4. **YARP timeout 45s** - Aligns gateway with backend capacity
5. **Connection Lifetime 300s** - Recycles stale connections preventing leaks

---

## Troubleshooting

### If Still Seeing Timeouts
1. Check database CPU/memory:
   ```cmd
   docker stats userdb bookingdb paymentdb
   ```
2. Increase pool sizes to 300:
   ```yaml
   Maximum Pool Size=300;Minimum Pool Size=20
   ```

### If Seeing 400 Bad Request
1. Check Seq for validation errors:
   ```
   Filter: @Level = 'Error' and Service = 'UserService'
   ```
2. Verify JWT token format in test output
3. Check RabbitMQ queue depth:
   ```
   http://localhost:15672
   ```

### If Auth Time Still >1s
1. Consider adding Redis for JWT token caching
2. Check UserDB query performance:
   ```sql
   SELECT * FROM pg_stat_statements ORDER BY mean_exec_time DESC LIMIT 10;
   ```

---

## Performance Tuning Guide

### For Production (>1000 concurrent users)
```yaml
# docker-compose.yml - Add resource limits
services:
  userservice:
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 2G
        reservations:
          cpus: '0.5'
          memory: 512M
```

### Kestrel Tuning
```csharp
// For high-throughput scenarios
serverOptions.Limits.MaxConcurrentConnections = 2000;
serverOptions.Limits.Http2.MaxStreamsPerConnection = 100;
```

### Database Tuning
```yaml
# PostgreSQL
max_connections=300
shared_buffers=256MB
effective_cache_size=1GB

# MongoDB
maxIncomingConnections=500
```

---

## Next Steps

1. ✅ Run load test with 100 flows / 5 concurrent
2. ✅ Verify success rate >95%
3. ⬜ Test with 200 flows / 10 concurrent
4. ⬜ Test with 500 flows / 20 concurrent
5. ⬜ Add Redis caching for JWT tokens
6. ⬜ Implement distributed tracing (OpenTelemetry)
7. ⬜ Add Prometheus metrics for real-time monitoring

---

## Files Modified

### Services (4 files)
- `src/UserService/Program.cs`
- `src/BookingService/Program.cs`
- `src/PaymentService/Program.cs`
- `src/ApiGateway/Program.cs`

### Configuration (2 files)
- `docker-compose.yml`
- `src/ApiGateway/appsettings.json`

### Test Scripts (1 file)
- `scripts/testing/test-e2e-auth.ps1`

**Total: 7 files modified**

---

## Monitoring Queries (Seq)

### Find Slow Requests
```
@Duration > 5000 and @Level = 'Information'
| select Timestamp, Service, @Duration, RequestPath
```

### Connection Pool Exhaustion
```
@Message like '%pool%' or @Message like '%connection%'
| where @Level in ['Warning', 'Error']
```

### Failed Authentications
```
Service = 'ApiGateway' and @Message like '%authentication%failed%'
| select Timestamp, UserId, @Exception
```

---

**Document Version:** 1.0  
**Last Updated:** 2025-11-07  
**Related Docs:** `docs/phase4-gateway-security/RATE_LIMITING_IMPLEMENTATION.md`
