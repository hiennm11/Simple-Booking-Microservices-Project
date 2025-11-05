# Testing Scripts - Quick Reference

## Fixed Issues ✅

The PowerShell testing scripts have been fixed to work correctly on Windows with both PowerShell 5.1 and PowerShell 7+.

**Issues that were fixed:**
- ❌ Unicode symbols (✓, ✗, ⚠) causing parsing errors
- ❌ Thread-safe variables causing type errors
- ❌ PowerShell 7+ only syntax in original scripts

**Solutions:**
- ✅ Replaced Unicode symbols with ASCII text: `[OK]`, `[FAIL]`, `[WARN]`
- ✅ Added PowerShell version detection
- ✅ Created fallback for PowerShell 5.1 users
- ✅ Created simplified version for sequential testing

---

## Available Testing Scripts

### 1. `test-load.ps1` - Main Load Testing Script

**Requirements:** PowerShell 7+ (for parallel execution)

**Usage:**
```powershell
# Basic test with 100 requests, 10 concurrent
.\test-load.ps1 -NumberOfRequests 100 -ConcurrentThreads 10

# Custom configuration
.\test-load.ps1 -NumberOfRequests 500 -ConcurrentThreads 20 -BaseUrl "http://localhost:5002"
```

**Features:**
- Parallel execution (requires PowerShell 7+)
- Configurable concurrent threads
- Performance metrics (average, min, max, p95)
- Success rate tracking
- Performance assessment

**Falls back to sequential mode** if PowerShell 5.1 is detected.

---

### 2. `test-load-simple.ps1` - Simple Load Testing (No PS7 Required)

**Requirements:** PowerShell 5.1+ (any version)

**Usage:**
```powershell
# Test with 20 requests (sequential)
.\test-load-simple.ps1 -NumberOfRequests 20

# Custom URL
.\test-load-simple.ps1 -NumberOfRequests 50 -BaseUrl "http://localhost:5002"
```

**Features:**
- Works with PowerShell 5.1+
- Sequential execution (one request at a time)
- Same metrics as main script
- Simpler, more compatible

**Use this if:**
- You don't have PowerShell 7+
- You want quick testing without setup
- You're testing basic functionality

---

### 3. `test-e2e-load.ps1` - End-to-End Flow Testing

**Requirements:** PowerShell 7+ (for parallel execution)

**Usage:**
```powershell
# Test 50 complete flows with 5 concurrent
.\test-e2e-load.ps1 -NumberOfFlows 50 -ConcurrentFlows 5

# Custom URLs
.\test-e2e-load.ps1 -NumberOfFlows 20 -ConcurrentFlows 3 `
    -GatewayUrl "http://localhost:5000" `
    -BookingUrl "http://localhost:5002" `
    -PaymentUrl "http://localhost:5003"
```

**What it tests:**
1. User registration (via API Gateway)
2. Booking creation (BookingService)
3. Payment processing (PaymentService)
4. Event publishing (RabbitMQ)
5. Event consumption (BookingService)
6. Status update (PENDING → CONFIRMED)

---

### 4. `monitor-health.ps1` - Real-time Health Monitoring

**Requirements:** PowerShell 5.1+

**Usage:**
```powershell
# Monitor with 5 second refresh
.\monitor-health.ps1

# Custom refresh interval
.\monitor-health.ps1 -RefreshIntervalSeconds 10
```

**Monitors:**
- All 4 microservices health endpoints
- RabbitMQ status and message counts
- Seq logging availability
- PostgreSQL databases (userdb, bookingdb)
- MongoDB (paymentdb)

Press `Ctrl+C` to stop.

---

### 5. `test-health.bat` - Quick Health Check

**Requirements:** curl (built-in on Windows 10+)

**Usage:**
```cmd
test-health.bat
```

**Checks:**
- UserService health
- BookingService health
- PaymentService health
- ApiGateway health
- Docker container status

---

### 6. `test-system.bat` - Complete System Test

**Requirements:** Docker, PowerShell

**Usage:**
```cmd
test-system.bat
```

**Performs:**
1. Docker status check
2. Services running check
3. Health check all services
4. Basic API functionality test
5. Small load test (20 requests)

---

## PowerShell Version Check

Check your PowerShell version:
```powershell
$PSVersionTable.PSVersion
```

**Output example:**
```
Major  Minor  Build  Revision
-----  -----  -----  --------
5      1      19041  4648      # PowerShell 5.1 (Windows built-in)
7      4      0      0         # PowerShell 7.4 (download required)
```

**Download PowerShell 7+:**
- Windows: https://aka.ms/powershell
- Or use Windows Package Manager: `winget install Microsoft.PowerShell`

---

## Quick Start

### For PowerShell 7+ Users:
```powershell
# 1. Health check
.\test-health.bat

# 2. Load test with concurrency
.\test-load.ps1 -NumberOfRequests 100 -ConcurrentThreads 10

# 3. E2E flow test
.\test-e2e-load.ps1 -NumberOfFlows 50 -ConcurrentFlows 5

# 4. Monitor while testing (separate terminal)
.\monitor-health.ps1
```

### For PowerShell 5.1 Users:
```powershell
# 1. Health check
.\test-health.bat

# 2. Simple load test (sequential)
.\test-load-simple.ps1 -NumberOfRequests 20

# 3. Complete system test
.\test-system.bat

# 4. Monitor health
.\monitor-health.ps1
```

---

## Example Output

### Successful Test:
```
========================================
   Load Test Results
========================================
Total Requests:    100
Successful:        100
Failed:            0
Success Rate:      100%

========================================
   Performance Metrics
========================================
Total Time:        5.23 seconds
Requests/sec:      19.12

Response Times:
  Average:         235ms
  Minimum:         8ms
  Maximum:         1024ms
  95th Percentile: 512ms

========================================
   Performance Assessment
========================================
[GOOD] Response time: p95 < 1000ms
[EXCELLENT] Error rate: <1%
[POOR] Throughput: <20 req/s
```

---

## Troubleshooting

### Error: "ForEach-Object : Parameter set cannot be resolved"
**Cause:** Using PowerShell 5.1 with `-Parallel` parameter  
**Solution:** Use `test-load-simple.ps1` or upgrade to PowerShell 7+

### Error: "Execution policy" or "scripts are disabled"
**Cause:** PowerShell execution policy  
**Solution:** 
```powershell
powershell -ExecutionPolicy Bypass -File test-load.ps1
```

### Error: Connection refused or timeout
**Cause:** Services not running  
**Solution:**
```cmd
docker-compose up -d
.\test-health.bat
```

### Script shows "Running in sequential mode"
**Cause:** PowerShell 5.1 detected  
**Solution:** This is normal, tests will run slower but still work. Upgrade to PS7+ for better performance.

---

## Performance Targets

| Metric | Excellent | Good | Acceptable | Poor |
|--------|-----------|------|------------|------|
| Response Time (p95) | < 200ms | < 500ms | < 1000ms | > 1000ms |
| Error Rate | < 1% | < 5% | < 10% | > 10% |
| Throughput | > 100 req/s | > 50 req/s | > 20 req/s | < 20 req/s |
| E2E Flow Time | < 3s | < 5s | < 10s | > 10s |

---

## Additional Resources

- **Complete Testing Guide**: `docs/E2E_TESTING_GUIDE.md`
- **Quick Start**: `TESTING_QUICK_START.md`
- **Project Documentation**: `README.md`

---

**Need Help?**
1. Check service health: `.\test-health.bat`
2. View logs: http://localhost:5341
3. Check RabbitMQ: http://localhost:15672
4. Monitor containers: `docker stats`
