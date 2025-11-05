# 🚀 Quick Testing Guide

Simple commands to test your microservices system end-to-end and under load.

## Prerequisites

1. **Start all services**:
   ```cmd
   docker-compose up -d
   ```

2. **Verify services are running**:
   ```cmd
   docker-compose ps
   ```

3. **PowerShell Version** (for load testing):
   - **Recommended**: PowerShell 7+ for parallel execution
   - **Alternative**: PowerShell 5.1 (runs tests sequentially, slower)
   - Check version: `$PSVersionTable.PSVersion`
   - Download PS7: https://aka.ms/powershell

## Quick Health Check (30 seconds)

```cmd
REM Check all service health endpoints
test-health.bat
```

Or check manually:
```cmd
curl http://localhost:5000/health
curl http://localhost:5001/health
curl http://localhost:5002/health
curl http://localhost:5003/health
```

## Manual E2E Test (2 minutes)

Test the complete flow: Register → Create Booking → Process Payment → Verify

### PowerShell:
```powershell
# 1. Register user
$user = Invoke-RestMethod -Uri "http://localhost:5000/api/users/register" `
    -Method Post -ContentType "application/json" `
    -Body '{"username":"testuser","email":"test@example.com","password":"Test@2025!Pass","firstName":"Test","lastName":"User"}'

# 2. Create booking
$booking = Invoke-RestMethod -Uri "http://localhost:5002/api/bookings" `
    -Method Post -ContentType "application/json" `
    -Body "{`"userId`":`"$($user.id)`",`"roomId`":`"ROOM-101`",`"amount`":500000}"

Write-Host "Booking Status: $($booking.status)"  # Should be PENDING

# 3. Process payment
$payment = Invoke-RestMethod -Uri "http://localhost:5003/api/payment/pay" `
    -Method Post -ContentType "application/json" `
    -Body "{`"bookingId`":`"$($booking.id)`",`"amount`":500000}"

# 4. Wait for event processing
Start-Sleep -Seconds 3

# 5. Check booking status updated
$updated = Invoke-RestMethod -Uri "http://localhost:5002/api/bookings/$($booking.id)"
Write-Host "Updated Status: $($updated.status)"  # Should be CONFIRMED
```

### Using VS Code REST Client:

Open `src/ApiGateway/ApiGateway.http` and click "Send Request" on each endpoint.

## Load Testing

### Basic Load Test (100 requests)
```powershell
.\test-load.ps1 -NumberOfRequests 100 -ConcurrentThreads 10
```

**Output:**
- Success rate
- Average/Min/Max response times
- Requests per second
- Performance assessment

### Medium Load Test (500 requests)
```powershell
.\test-load.ps1 -NumberOfRequests 500 -ConcurrentThreads 20
```

### Stress Test (1000 requests)
```powershell
.\test-load.ps1 -NumberOfRequests 1000 -ConcurrentThreads 50
```

## End-to-End Flow Load Test

Tests the complete flow (Register → Booking → Payment) with multiple users:

```powershell
# 50 complete flows, 5 concurrent
.\test-e2e-load.ps1 -NumberOfFlows 50 -ConcurrentFlows 5

# Stress test: 100 flows, 10 concurrent
.\test-e2e-load.ps1 -NumberOfFlows 100 -ConcurrentFlows 10
```

**This tests:**
- User registration through API Gateway
- Booking creation
- Payment processing
- Event-driven booking status updates
- Full system integration

## Continuous Health Monitoring

Monitor all services in real-time while load testing:

```powershell
.\monitor-health.ps1
```

**Shows:**
- Service health status
- Response times
- RabbitMQ messages and connections
- Seq logging status
- Database connectivity

Press `Ctrl+C` to stop monitoring.

## Monitor During Load Test

**Terminal 1**: Run load test
```powershell
.\test-e2e-load.ps1 -NumberOfFlows 100 -ConcurrentFlows 10
```

**Terminal 2**: Monitor health
```powershell
.\monitor-health.ps1
```

**Terminal 3**: Watch containers
```cmd
docker stats
```

**Browser**: Open monitoring dashboards
- RabbitMQ: http://localhost:15672 (guest/guest)
- Seq Logs: http://localhost:5341 (admin/Admin@2025!SeqPass)

## Recommended Testing Scenarios

### 1. Smoke Test (Quick validation)
```powershell
.\test-health.bat
.\test-load.ps1 -NumberOfRequests 10 -ConcurrentThreads 2
```

### 2. Performance Baseline
```powershell
.\test-load.ps1 -NumberOfRequests 100 -ConcurrentThreads 10
.\test-e2e-load.ps1 -NumberOfFlows 20 -ConcurrentFlows 3
```

### 3. Sustained Load Test
```powershell
# Run for ~5 minutes
.\test-load.ps1 -NumberOfRequests 6000 -ConcurrentThreads 20
```

### 4. Spike Test
```powershell
# Normal load
.\test-load.ps1 -NumberOfRequests 100 -ConcurrentThreads 10
Start-Sleep -Seconds 10
# Sudden spike
.\test-load.ps1 -NumberOfRequests 500 -ConcurrentThreads 50
```

### 5. Event Processing Test
```powershell
# Create many bookings quickly to test RabbitMQ
.\test-load.ps1 -NumberOfRequests 200 -ConcurrentThreads 40

# Watch RabbitMQ process messages
# Open: http://localhost:15672
```

## Troubleshooting During Tests

### High Response Times
```powershell
# Check container resources
docker stats

# Check service logs
docker logs bookingservice --tail 50
docker logs paymentservice --tail 50

# Check database connections
docker exec userdb psql -U userservice -d userdb -c "SELECT count(*) FROM pg_stat_activity;"
```

### Request Failures
```powershell
# Check Seq for errors
# Open: http://localhost:5341
# Search: @Level = 'Error'

# Restart problematic service
docker-compose restart bookingservice
```

### Event Processing Delays
```powershell
# Check RabbitMQ queue depth
# Open: http://localhost:15672
# Go to Queues tab

# Check consumer logs
docker logs bookingservice --tail 100 | Select-String "PaymentSucceeded"
```

## Performance Targets

| Metric | Target | Good | Acceptable |
|--------|--------|------|------------|
| Response Time (p95) | < 200ms | < 500ms | < 1000ms |
| Success Rate | > 99% | > 95% | > 90% |
| Throughput | > 100 req/s | > 50 req/s | > 20 req/s |
| E2E Flow Time | < 3s | < 5s | < 10s |
| Event Processing | < 100ms | < 500ms | < 2000ms |

## Quick Commands Reference

```powershell
# Health check
.\test-health.bat

# Basic load test
.\test-load.ps1

# E2E flow test
.\test-e2e-load.ps1

# Continuous monitoring
.\monitor-health.ps1

# Docker stats
docker stats

# Service logs
docker logs bookingservice -f

# Restart services
docker-compose restart

# Stop everything
docker-compose down

# Clean restart
docker-compose down -v && docker-compose up -d
```

## Next Steps

After testing, analyze results:

1. **Check Seq logs**: http://localhost:5341
   - Search for errors: `@Level = 'Error'`
   - View slow requests: `ResponseTime > 1000`
   - Track events: `EventName is not null`

2. **Review RabbitMQ**: http://localhost:15672
   - Check message rates
   - Verify queues are being processed
   - Look for connection issues

3. **Optimize bottlenecks**:
   - Scale services: `docker-compose up -d --scale bookingservice=3`
   - Increase database connection pools
   - Add caching (Redis)
   - Implement circuit breakers

## Full Documentation

For detailed testing guide, see: `docs/E2E_TESTING_GUIDE.md`

---

**Quick Start Testing:**
```powershell
# 1. Start services
docker-compose up -d

# 2. Check health
.\test-health.bat

# 3. Run E2E test
.\test-e2e-load.ps1 -NumberOfFlows 20 -ConcurrentFlows 3

# 4. View results in Seq
start http://localhost:5341
```
