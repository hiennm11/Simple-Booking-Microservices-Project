# 🧪 End-to-End Testing Guide

This guide covers how to test the Booking Microservices system end-to-end, including health checks, load testing, and monitoring service behavior under stress.

## Table of Contents
- [Quick Health Check](#quick-health-check)
- [Manual End-to-End Testing](#manual-end-to-end-testing)
- [Load Testing with Multiple Requests](#load-testing-with-multiple-requests)
- [Monitoring Under Load](#monitoring-under-load)
- [Performance Testing Scenarios](#performance-testing-scenarios)
- [Troubleshooting](#troubleshooting)

---

## Quick Health Check

### 1. Check All Services Health
```bash
# Windows (CMD)
test-health.bat

# Or manually check each service
curl http://localhost:5000/health  # API Gateway
curl http://localhost:5001/health  # UserService
curl http://localhost:5002/health  # BookingService
curl http://localhost:5003/health  # PaymentService
```

### 2. Check Container Status
```bash
docker-compose ps

# Expected output: All services should show "Up" and "healthy"
```

### 3. Check RabbitMQ
- Open http://localhost:15672
- Login: `guest` / `guest`
- Verify queues exist: `booking_created`, `payment_succeeded`
- Check connections: Should see 2-3 connections (BookingService, PaymentService)

### 4. Check Seq Logs
- Open http://localhost:5341
- Login: `admin` / `Admin@2025!SeqPass`
- Verify logs are flowing from all services

---

## Manual End-to-End Testing

### Complete Booking Flow

#### Step 1: Register a User
```powershell
# PowerShell
$registerBody = @{
    username = "testuser"
    email = "testuser@example.com"
    password = "Test@2025!Pass"
    firstName = "Test"
    lastName = "User"
} | ConvertTo-Json

$user = Invoke-RestMethod -Uri "http://localhost:5000/api/users/register" `
    -Method Post `
    -ContentType "application/json" `
    -Body $registerBody

# Save the user ID
$userId = $user.id
Write-Host "User ID: $userId"
```

```bash
# Linux/Mac (curl)
curl -X POST http://localhost:5000/api/users/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "testuser",
    "email": "testuser@example.com",
    "password": "Test@2025!Pass",
    "firstName": "Test",
    "lastName": "User"
  }'
```

#### Step 2: Login and Get Token
```powershell
# PowerShell
$loginBody = @{
    username = "testuser"
    password = "Test@2025!Pass"
} | ConvertTo-Json

$loginResponse = Invoke-RestMethod -Uri "http://localhost:5000/api/users/login" `
    -Method Post `
    -ContentType "application/json" `
    -Body $loginBody

$token = $loginResponse.token
Write-Host "Token: $token"
```

#### Step 3: Create a Booking
```powershell
# PowerShell
$bookingBody = @{
    userId = $userId
    roomId = "ROOM-101"
    amount = 500000
} | ConvertTo-Json

$booking = Invoke-RestMethod -Uri "http://localhost:5002/api/bookings" `
    -Method Post `
    -ContentType "application/json" `
    -Body $bookingBody

$bookingId = $booking.id
Write-Host "Booking ID: $bookingId"
Write-Host "Booking Status: $($booking.status)"  # Should be PENDING
```

#### Step 4: Process Payment
```powershell
# PowerShell
$paymentBody = @{
    bookingId = $bookingId
    amount = 500000
} | ConvertTo-Json

$payment = Invoke-RestMethod -Uri "http://localhost:5003/api/payment/pay" `
    -Method Post `
    -ContentType "application/json" `
    -Body $paymentBody

Write-Host "Payment ID: $($payment.id)"
Write-Host "Payment Status: $($payment.status)"
```

#### Step 5: Verify Booking Updated
```powershell
# Wait a few seconds for event processing
Start-Sleep -Seconds 3

# Check booking status
$updatedBooking = Invoke-RestMethod -Uri "http://localhost:5002/api/bookings/$bookingId" `
    -Method Get

Write-Host "Updated Booking Status: $($updatedBooking.status)"  # Should be CONFIRMED
```

---

## Load Testing with Multiple Requests

### Option 1: PowerShell Load Test Script

Create `test-load.ps1`:

```powershell
param(
    [int]$NumberOfRequests = 100,
    [int]$ConcurrentThreads = 10,
    [string]$BaseUrl = "http://localhost:5000"
)

Write-Host "Starting Load Test"
Write-Host "==================="
Write-Host "Requests: $NumberOfRequests"
Write-Host "Concurrent: $ConcurrentThreads"
Write-Host "Target: $BaseUrl"
Write-Host ""

$results = @{
    Success = 0
    Failed = 0
    TotalTime = 0
    MinTime = [double]::MaxValue
    MaxTime = 0
}

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

# Create thread-safe collections
$successCount = [ref]0
$failCount = [ref]0
$times = [System.Collections.Concurrent.ConcurrentBag[double]]::new()

# Create jobs
$jobs = 1..$NumberOfRequests | ForEach-Object -Parallel {
    $requestId = $_
    $url = $using:BaseUrl
    
    try {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        
        # Create booking request
        $body = @{
            userId = [guid]::NewGuid().ToString()
            roomId = "ROOM-$(Get-Random -Minimum 100 -Maximum 999)"
            amount = Get-Random -Minimum 100000 -Maximum 1000000
        } | ConvertTo-Json
        
        $response = Invoke-RestMethod -Uri "$url/api/bookings" `
            -Method Post `
            -ContentType "application/json" `
            -Body $body `
            -TimeoutSec 30
        
        $sw.Stop()
        $elapsed = $sw.Elapsed.TotalMilliseconds
        
        Write-Host "✓ Request $requestId completed in $($elapsed)ms"
        
        # Thread-safe increment
        [System.Threading.Interlocked]::Increment($using:successCount)
        ($using:times).Add($elapsed)
        
    } catch {
        Write-Host "✗ Request $requestId failed: $($_.Exception.Message)"
        [System.Threading.Interlocked]::Increment($using:failCount)
    }
} -ThrottleLimit $ConcurrentThreads

$stopwatch.Stop()

# Calculate statistics
$timeArray = $times.ToArray()
$avgTime = if ($timeArray.Count -gt 0) { ($timeArray | Measure-Object -Average).Average } else { 0 }
$minTime = if ($timeArray.Count -gt 0) { ($timeArray | Measure-Object -Minimum).Minimum } else { 0 }
$maxTime = if ($timeArray.Count -gt 0) { ($timeArray | Measure-Object -Maximum).Maximum } else { 0 }

Write-Host ""
Write-Host "Load Test Results"
Write-Host "==================="
Write-Host "Total Requests: $NumberOfRequests"
Write-Host "Successful: $successCount"
Write-Host "Failed: $failCount"
Write-Host "Success Rate: $([math]::Round(($successCount / $NumberOfRequests) * 100, 2))%"
Write-Host ""
Write-Host "Performance Metrics"
Write-Host "==================="
Write-Host "Total Time: $($stopwatch.Elapsed.TotalSeconds) seconds"
Write-Host "Requests/sec: $([math]::Round($NumberOfRequests / $stopwatch.Elapsed.TotalSeconds, 2))"
Write-Host "Avg Response Time: $([math]::Round($avgTime, 2))ms"
Write-Host "Min Response Time: $([math]::Round($minTime, 2))ms"
Write-Host "Max Response Time: $([math]::Round($maxTime, 2))ms"
```

**Run the load test:**
```powershell
# Test with 100 requests, 10 concurrent
.\test-load.ps1 -NumberOfRequests 100 -ConcurrentThreads 10

# Test with 500 requests, 20 concurrent
.\test-load.ps1 -NumberOfRequests 500 -ConcurrentThreads 20

# Stress test with 1000 requests, 50 concurrent
.\test-load.ps1 -NumberOfRequests 1000 -ConcurrentThreads 50
```

### Option 2: Complete E2E Flow Load Test

Create `test-e2e-load.ps1`:

```powershell
param(
    [int]$NumberOfFlows = 50,
    [int]$ConcurrentFlows = 5
)

Write-Host "Starting End-to-End Flow Load Test"
Write-Host "====================================="
Write-Host "Total Flows: $NumberOfFlows"
Write-Host "Concurrent: $ConcurrentFlows"
Write-Host ""

$successCount = [ref]0
$failCount = [ref]0

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

1..$NumberOfFlows | ForEach-Object -Parallel {
    $flowId = $_
    
    try {
        # Step 1: Register user
        $username = "user$flowId-$(Get-Random)"
        $registerBody = @{
            username = $username
            email = "$username@example.com"
            password = "Test@2025!Pass"
            firstName = "Test"
            lastName = "User"
        } | ConvertTo-Json
        
        $user = Invoke-RestMethod -Uri "http://localhost:5000/api/users/register" `
            -Method Post `
            -ContentType "application/json" `
            -Body $registerBody `
            -TimeoutSec 30
        
        $userId = $user.id
        
        # Step 2: Create booking
        $bookingBody = @{
            userId = $userId
            roomId = "ROOM-$(Get-Random -Minimum 100 -Maximum 999)"
            amount = Get-Random -Minimum 100000 -Maximum 1000000
        } | ConvertTo-Json
        
        $booking = Invoke-RestMethod -Uri "http://localhost:5002/api/bookings" `
            -Method Post `
            -ContentType "application/json" `
            -Body $bookingBody `
            -TimeoutSec 30
        
        $bookingId = $booking.id
        
        # Step 3: Process payment
        $paymentBody = @{
            bookingId = $bookingId
            amount = $booking.amount
        } | ConvertTo-Json
        
        $payment = Invoke-RestMethod -Uri "http://localhost:5003/api/payment/pay" `
            -Method Post `
            -ContentType "application/json" `
            -Body $paymentBody `
            -TimeoutSec 30
        
        # Step 4: Wait and verify booking status
        Start-Sleep -Seconds 2
        
        $updatedBooking = Invoke-RestMethod -Uri "http://localhost:5002/api/bookings/$bookingId" `
            -Method Get `
            -TimeoutSec 30
        
        if ($updatedBooking.status -eq "CONFIRMED") {
            Write-Host "✓ Flow $flowId completed successfully (User: $userId, Booking: $bookingId)"
            [System.Threading.Interlocked]::Increment($using:successCount)
        } else {
            Write-Host "⚠ Flow $flowId: Booking not confirmed (Status: $($updatedBooking.status))"
            [System.Threading.Interlocked]::Increment($using:failCount)
        }
        
    } catch {
        Write-Host "✗ Flow $flowId failed: $($_.Exception.Message)"
        [System.Threading.Interlocked]::Increment($using:failCount)
    }
} -ThrottleLimit $ConcurrentFlows

$stopwatch.Stop()

Write-Host ""
Write-Host "E2E Flow Test Results"
Write-Host "====================="
Write-Host "Total Flows: $NumberOfFlows"
Write-Host "Successful: $successCount"
Write-Host "Failed: $failCount"
Write-Host "Success Rate: $([math]::Round(($successCount / $NumberOfFlows) * 100, 2))%"
Write-Host "Total Time: $($stopwatch.Elapsed.TotalSeconds) seconds"
Write-Host "Flows/sec: $([math]::Round($NumberOfFlows / $stopwatch.Elapsed.TotalSeconds, 2))"
```

**Run the E2E flow test:**
```powershell
# Test with 50 complete flows, 5 concurrent
.\test-e2e-load.ps1 -NumberOfFlows 50 -ConcurrentFlows 5

# Stress test with 100 flows, 10 concurrent
.\test-e2e-load.ps1 -NumberOfFlows 100 -ConcurrentFlows 10
```

### Option 3: Using Apache Bench (ab)

```bash
# Install Apache Bench (if not already installed)
# Windows: Download from https://www.apachelounge.com/download/
# Linux: sudo apt-get install apache2-utils
# Mac: brew install httpd (ab is included)

# Test booking creation endpoint
ab -n 1000 -c 10 -p booking.json -T "application/json" http://localhost:5002/api/bookings

# Create booking.json file:
# {
#   "userId": "a3bb189e-8bf9-3888-9912-ace4e6543002",
#   "roomId": "ROOM-101",
#   "amount": 500000
# }
```

### Option 4: Using k6 (Recommended for Advanced Load Testing)

Install k6: https://k6.io/docs/getting-started/installation/

Create `load-test.js`:

```javascript
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '30s', target: 10 },  // Ramp up to 10 users
    { duration: '1m', target: 10 },   // Stay at 10 users
    { duration: '30s', target: 50 },  // Ramp up to 50 users
    { duration: '2m', target: 50 },   // Stay at 50 users
    { duration: '30s', target: 0 },   // Ramp down to 0 users
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'], // 95% of requests should be below 500ms
    http_req_failed: ['rate<0.1'],    // Less than 10% of requests should fail
  },
};

export default function () {
  // Create booking
  const bookingPayload = JSON.stringify({
    userId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
    roomId: `ROOM-${Math.floor(Math.random() * 900) + 100}`,
    amount: Math.floor(Math.random() * 900000) + 100000,
  });

  const bookingRes = http.post('http://localhost:5002/api/bookings', bookingPayload, {
    headers: { 'Content-Type': 'application/json' },
  });

  check(bookingRes, {
    'booking created': (r) => r.status === 201,
    'booking has id': (r) => JSON.parse(r.body).id !== undefined,
  });

  if (bookingRes.status === 201) {
    const booking = JSON.parse(bookingRes.body);
    
    // Process payment
    const paymentPayload = JSON.stringify({
      bookingId: booking.id,
      amount: booking.amount,
    });

    const paymentRes = http.post('http://localhost:5003/api/payment/pay', paymentPayload, {
      headers: { 'Content-Type': 'application/json' },
    });

    check(paymentRes, {
      'payment processed': (r) => r.status === 200,
    });
  }

  sleep(1);
}
```

**Run k6 test:**
```bash
k6 run load-test.js

# Or with custom parameters
k6 run --vus 50 --duration 2m load-test.js
```

---

## Monitoring Under Load

### 1. Watch Docker Container Stats
```bash
# Monitor CPU, Memory, and Network usage
docker stats

# Watch specific containers
docker stats userservice bookingservice paymentservice rabbitmq
```

### 2. Monitor RabbitMQ
Open http://localhost:15672 during load test:
- **Overview tab**: Check message rates
- **Queues tab**: Monitor queue lengths and message rates
- **Connections tab**: Check number of active connections

Key metrics to watch:
- Message rate (msg/s)
- Queue depth (should not grow indefinitely)
- Consumer utilization
- Connection count

### 3. Monitor Logs in Seq
Open http://localhost:5341 during load test:

**Useful queries:**
```sql
-- Count requests per second
select count(*) as RequestCount, DateTime(Truncate(@Timestamp, 1000)) as Second
group by Second

-- Find slow requests
select RequestPath, ResponseTime
where ResponseTime > 1000
order by ResponseTime desc

-- Error rate
select count(*) as ErrorCount
where @Level = 'Error'
group by DateTime(Truncate(@Timestamp, 60000))

-- Event processing time
select EventName, ProcessingTimeMs
where EventName in ['BookingCreated', 'PaymentSucceeded']
order by ProcessingTimeMs desc
```

### 4. Check Database Connections

**PostgreSQL:**
```bash
# Check active connections on UserService DB
docker exec -it userdb psql -U userservice -d userdb -c "SELECT count(*) FROM pg_stat_activity WHERE datname='userdb';"

# Check active connections on BookingService DB
docker exec -it bookingdb psql -U bookingservice -d bookingdb -c "SELECT count(*) FROM pg_stat_activity WHERE datname='bookingdb';"
```

**MongoDB:**
```bash
# Check current operations
docker exec -it paymentdb mongosh -u paymentservice -p PaymentSvc@2025SecurePass --eval "db.currentOp()"
```

### 5. Application Health Checks
Create `monitor-health.ps1`:

```powershell
while ($true) {
    Clear-Host
    Write-Host "Health Status at $(Get-Date -Format 'HH:mm:ss')"
    Write-Host "=" * 60
    
    $services = @(
        @{Name="UserService"; Url="http://localhost:5001/health"},
        @{Name="BookingService"; Url="http://localhost:5002/health"},
        @{Name="PaymentService"; Url="http://localhost:5003/health"},
        @{Name="ApiGateway"; Url="http://localhost:5000/health"}
    )
    
    foreach ($service in $services) {
        try {
            $response = Invoke-RestMethod -Uri $service.Url -TimeoutSec 5
            Write-Host "✓ $($service.Name): $($response.status)" -ForegroundColor Green
        } catch {
            Write-Host "✗ $($service.Name): UNHEALTHY" -ForegroundColor Red
        }
    }
    
    Start-Sleep -Seconds 5
}
```

**Run during load test:**
```powershell
.\monitor-health.ps1
```

---

## Performance Testing Scenarios

### Scenario 1: Baseline Performance Test
**Goal:** Establish baseline metrics with normal load

```powershell
# 100 requests, 10 concurrent users
.\test-load.ps1 -NumberOfRequests 100 -ConcurrentThreads 10
```

**Expected Results:**
- Success Rate: > 99%
- Avg Response Time: < 200ms
- Max Response Time: < 500ms

### Scenario 2: Sustained Load Test
**Goal:** Test system stability under continuous load

```powershell
# Run for 5 minutes with 20 concurrent users
.\test-load.ps1 -NumberOfRequests 6000 -ConcurrentThreads 20
```

**Monitor:**
- Memory usage should remain stable
- No increasing error rates over time
- Database connection pool stays healthy

### Scenario 3: Spike Test
**Goal:** Test system behavior during sudden traffic spikes

```powershell
# Start with normal load
.\test-load.ps1 -NumberOfRequests 100 -ConcurrentThreads 10

# Wait 10 seconds
Start-Sleep -Seconds 10

# Sudden spike
.\test-load.ps1 -NumberOfRequests 500 -ConcurrentThreads 50
```

**Expected:**
- System should handle spike gracefully
- Response times may increase but should recover
- No crashes or cascading failures

### Scenario 4: Event Processing Under Load
**Goal:** Test async event processing with many messages

```powershell
# Create many bookings quickly
.\test-load.ps1 -NumberOfRequests 500 -ConcurrentThreads 50
```

**Monitor RabbitMQ:**
- Check that messages are consumed steadily
- No messages stuck in queue
- Consumer prefetch working correctly

### Scenario 5: Database Stress Test
**Goal:** Test database performance under load

```powershell
# Create many bookings for same user (tests database contention)
$userId = "a3bb189e-8bf9-3888-9912-ace4e6543002"

1..200 | ForEach-Object -Parallel {
    $body = @{
        userId = $using:userId
        roomId = "ROOM-$(Get-Random -Minimum 100 -Maximum 999)"
        amount = 500000
    } | ConvertTo-Json
    
    Invoke-RestMethod -Uri "http://localhost:5002/api/bookings" `
        -Method Post `
        -ContentType "application/json" `
        -Body $body
} -ThrottleLimit 20
```

---

## Troubleshooting

### High Response Times
**Symptoms:** Requests taking > 1 second

**Check:**
1. Database connection pool size
2. RabbitMQ message queue depth
3. Container CPU/memory limits
4. Network latency between containers

**Solutions:**
```bash
# Increase database connection pool (in appsettings.json)
"ConnectionStrings": {
  "DefaultConnection": "...;Maximum Pool Size=100;"
}

# Scale services
docker-compose up -d --scale bookingservice=3
```

### Request Failures
**Symptoms:** 500 errors, timeouts

**Check:**
1. Service logs in Seq
2. Container health status
3. Database connectivity
4. RabbitMQ connection status

**Debug:**
```bash
# Check service logs
docker logs bookingservice --tail 100

# Check if service is running
docker ps | grep bookingservice

# Restart specific service
docker-compose restart bookingservice
```

### Memory Leaks
**Symptoms:** Increasing memory usage over time

**Check:**
```bash
# Monitor memory usage
docker stats --no-stream

# Check for memory leaks in .NET
docker exec bookingservice dotnet-dump collect -p 1
```

### Event Processing Delays
**Symptoms:** Booking status not updating quickly

**Check:**
1. RabbitMQ queue depth
2. Consumer prefetch settings
3. Event processing logs in Seq

**Query in Seq:**
```sql
select EventName, 
       avg(ProcessingTimeMs) as AvgProcessingTime,
       max(ProcessingTimeMs) as MaxProcessingTime
where EventName is not null
group by EventName
```

### Database Connection Exhaustion
**Symptoms:** "Cannot create more connections" errors

**Check:**
```bash
# PostgreSQL max connections
docker exec userdb psql -U userservice -d userdb -c "SHOW max_connections;"

# Current connections
docker exec userdb psql -U userservice -d userdb -c "SELECT count(*) FROM pg_stat_activity;"
```

**Solution:**
Increase max_connections in docker-compose.yml:
```yaml
userdb:
  command: postgres -c max_connections=200
```

---

## Performance Benchmarks

### Target Performance Metrics

| Metric | Target | Good | Acceptable | Poor |
|--------|--------|------|------------|------|
| API Response Time (p95) | < 200ms | < 500ms | < 1000ms | > 1000ms |
| Event Processing Time | < 100ms | < 500ms | < 2000ms | > 2000ms |
| Throughput (req/sec) | > 100 | > 50 | > 20 | < 20 |
| Error Rate | < 0.1% | < 1% | < 5% | > 5% |
| Database Query Time | < 50ms | < 100ms | < 500ms | > 500ms |
| Memory Usage (per service) | < 200MB | < 500MB | < 1GB | > 1GB |
| CPU Usage (per service) | < 50% | < 70% | < 90% | > 90% |

### Capacity Planning

**Current Setup (Single Instance):**
- Expected Capacity: 50-100 req/sec
- Max Concurrent Users: 100
- Database Connections: 50 per service

**Scaled Setup (3 Instances):**
- Expected Capacity: 150-300 req/sec
- Max Concurrent Users: 300
- Database Connections: 150 per service

---

## Next Steps

1. **Implement Distributed Tracing**: Add OpenTelemetry for request tracing
2. **Add Metrics Dashboard**: Integrate Prometheus + Grafana
3. **Circuit Breaker**: Implement Polly circuit breaker patterns
4. **Auto-Scaling**: Configure Docker Swarm or Kubernetes for auto-scaling
5. **Caching**: Add Redis for frequently accessed data
6. **Rate Limiting**: Implement API rate limiting in Gateway

---

## Summary

To test your system end-to-end under load:

1. **Start with health checks** to ensure all services are running
2. **Run manual E2E test** to verify the complete flow works
3. **Execute load tests** starting with small numbers and gradually increase
4. **Monitor everything**: Docker stats, RabbitMQ, Seq logs, database connections
5. **Analyze results** and identify bottlenecks
6. **Optimize and repeat** until you meet your performance targets

**Quick Start Command:**
```powershell
# 1. Check health
.\test-health.bat

# 2. Run E2E flow test with 50 flows
.\test-e2e-load.ps1 -NumberOfFlows 50 -ConcurrentFlows 5

# 3. Monitor while running
docker stats

# 4. Check results in Seq
# Open http://localhost:5341
```
