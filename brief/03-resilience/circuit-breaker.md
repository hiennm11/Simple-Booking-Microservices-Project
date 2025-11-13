# Circuit Breaker Pattern

## 📚 What is Circuit Breaker?

**Circuit Breaker** is a resilience pattern that prevents cascading failures by automatically stopping requests to a failing service, allowing it time to recover before retrying.

Named after electrical circuit breakers that protect electrical systems from damage during faults.

### The Problem: Cascading Failures

Without circuit breaker:

```
Service A → Service B (failing) → Wait for timeout (30s) → Retry → Wait again
                                                              ↓
                                   Resources exhausted (thread pool, connections)
                                                              ↓
                                   Service A also becomes slow/fails
                                                              ↓
                                   Entire system degrades ❌
```

**Impact**:
- Thread pool exhaustion
- Connection pool saturation
- Slow response times cascade upstream
- Complete system outage

---

## ✅ Solution: Fail Fast and Recover

With circuit breaker:

```
Service A → Service B (failing) → Circuit detects failures
                                                ↓
                                   Open circuit (fail fast)
                                                ↓
                        Service A immediately returns error (no waiting)
                                                ↓
                        After 30s, test if Service B recovered
                                                ↓
                        Service B healthy → Close circuit → Resume normal operation ✅
```

**Benefits**:
- ✅ Prevents resource exhaustion
- ✅ Fails fast instead of waiting
- ✅ Gives failing service time to recover
- ✅ Automatic recovery testing
- ✅ Protects entire system

---

## 🔄 Circuit Breaker States

```
           ┌─────────────┐
    ┌──────│   CLOSED    │◄─────┐
    │      │  (Normal)   │      │
    │      └──────┬──────┘      │
    │             │             │
    │    Failure threshold      │
    │    reached (e.g., 50%)    │
    │             │             │
    │      ┌──────▼──────┐      │
    │      │    OPEN     │      │
    │      │ (Blocking)  │      │
    │      └──────┬──────┘      │
    │             │             │
    │    After break duration   │
    │    (e.g., 30 seconds)     │
    │             │             │
    │      ┌──────▼──────┐      │
    │      │ HALF-OPEN   │      │
    │      │ (Testing)   │──────┘
    │      └──────┬──────┘
    │             │
    │    Success: Close circuit
    │    Failure: Reopen circuit
    │             │
    └─────────────┘
```

### 1. CLOSED (Normal Operation)

- **Behavior**: All requests pass through
- **Monitoring**: Tracks success/failure rate
- **Transition**: If failure ratio exceeds threshold → OPEN

**Example**: 10 requests, 5 failures = 50% failure rate → Circuit opens

---

### 2. OPEN (Failing Fast)

- **Behavior**: All requests rejected immediately (no call to failing service)
- **Response**: Returns error immediately (e.g., "Service unavailable")
- **Monitoring**: Wait for break duration
- **Transition**: After break duration → HALF-OPEN

**Example**: For 30 seconds, all requests fail fast without calling Service B

---

### 3. HALF-OPEN (Testing Recovery)

- **Behavior**: Allow limited requests through to test recovery
- **Monitoring**: Track success of test requests
- **Transition**:
  - Success → CLOSED (service recovered)
  - Failure → OPEN (service still failing)

**Example**: Allow 1-3 test requests. If successful, resume normal operation.

---

## 🔧 Implementation with Polly

### Basic Circuit Breaker

```csharp
using Polly;
using Polly.CircuitBreaker;

var circuitBreaker = new ResiliencePipelineBuilder()
    .AddCircuitBreaker(new CircuitBreakerStrategyOptions
    {
        // Open circuit if 50% of requests fail
        FailureRatio = 0.5,
        
        // Need at least 10 requests before evaluating
        MinimumThroughput = 10,
        
        // Stay open for 30 seconds
        BreakDuration = TimeSpan.FromSeconds(30),
        
        // Number of test requests in half-open state
        SamplingDuration = TimeSpan.FromSeconds(10)
    })
    .Build();

// Use circuit breaker
await circuitBreaker.ExecuteAsync(async ct =>
{
    await CallExternalServiceAsync();
}, cancellationToken);
```

---

### Advanced Configuration with Logging

```csharp
var circuitBreaker = new ResiliencePipelineBuilder()
    .AddCircuitBreaker(new CircuitBreakerStrategyOptions
    {
        FailureRatio = 0.5,
        MinimumThroughput = 10,
        BreakDuration = TimeSpan.FromSeconds(30),
        
        // ✅ Log when circuit opens
        OnOpened = args =>
        {
            _logger.LogError(
                "Circuit breaker OPENED for {ServiceName}. " +
                "Failure rate: {FailureRate:P}. " +
                "Will retest after {BreakDuration}s",
                serviceName,
                args.FailureRate,
                args.BreakDuration.TotalSeconds);
            
            return ValueTask.CompletedTask;
        },
        
        // ✅ Log when circuit closes (recovery)
        OnClosed = args =>
        {
            _logger.LogInformation(
                "Circuit breaker CLOSED for {ServiceName}. Service recovered.",
                serviceName);
            
            return ValueTask.CompletedTask;
        },
        
        // ✅ Log when entering half-open state
        OnHalfOpened = args =>
        {
            _logger.LogWarning(
                "Circuit breaker HALF-OPEN for {ServiceName}. Testing recovery...",
                serviceName);
            
            return ValueTask.CompletedTask;
        }
    })
    .Build();
```

---

### Combining Retry + Circuit Breaker

**Best Practice**: Use retry for transient errors, circuit breaker for sustained failures

```csharp
var pipeline = new ResiliencePipelineBuilder()
    // Step 1: Retry transient failures
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(2),
        BackoffType = DelayBackoffType.Exponential
    })
    
    // Step 2: Open circuit if many retries fail
    .AddCircuitBreaker(new CircuitBreakerStrategyOptions
    {
        FailureRatio = 0.5,
        MinimumThroughput = 10,
        BreakDuration = TimeSpan.FromSeconds(30)
    })
    
    // Step 3: Overall timeout
    .AddTimeout(TimeSpan.FromSeconds(10))
    .Build();
```

**Flow**:
```
Request 1: Try → Fail → Retry 3 times → Still fail (counts as 1 failure)
Request 2: Try → Fail → Retry 3 times → Still fail (counts as 2 failures)
...
Request 6: Try → Fail → Retry 3 times → Still fail (counts as 6 failures)

6 out of 10 requests failed (60% > 50% threshold)
→ Circuit OPENS
→ Next requests fail immediately without retry (saving resources)
→ After 30 seconds, test if service recovered
```

---

## 💡 Real-World Scenario

### Without Circuit Breaker ❌

```
Time: 10:00 - RabbitMQ overloaded
10:00:01 - Service tries to publish event → Timeout after 10s
10:00:11 - Service tries again → Timeout after 10s
10:00:21 - Service tries again → Timeout after 10s
10:00:31 - All threads blocked waiting for RabbitMQ
10:00:40 - Service runs out of threads → Entire service down

Impact: 40 seconds of complete service outage
```

### With Circuit Breaker ✅

```
Time: 10:00 - RabbitMQ overloaded
10:00:01 - Service tries to publish → Timeout after 10s (failure 1)
10:00:11 - Service tries again → Timeout after 10s (failure 2)
10:00:21 - Service tries again → Timeout after 10s (failure 3)
10:00:31 - Circuit OPENS (3 failures detected)
10:00:32 - New request → Fail fast immediately (no waiting)
10:00:33 - New request → Fail fast immediately
...
10:01:01 - Circuit HALF-OPEN → Test request succeeds
10:01:02 - Circuit CLOSED → Resume normal operation

Impact: Only first 30 seconds affected, service remains responsive
```

---

## 📊 Configuration Guidelines

### Failure Ratio Thresholds

| Service Criticality | Failure Ratio | Rationale |
|---------------------|---------------|-----------|
| **Critical** (Payment) | 0.3 (30%) | Open quickly to protect |
| **Important** (Booking) | 0.5 (50%) | Balanced protection |
| **Non-Critical** (Logging) | 0.7 (70%) | More tolerant |

---

### Minimum Throughput

```csharp
// ❌ Too low - opens on single failure
MinimumThroughput = 1

// ✅ Good - statistical significance
MinimumThroughput = 10

// ⚠️ High - may delay opening
MinimumThroughput = 100
```

**Rule of Thumb**: Use 10 for most services, 20-50 for high-traffic services.

---

### Break Duration

```csharp
// ❌ Too short - service hasn't recovered
BreakDuration = TimeSpan.FromSeconds(5)

// ✅ Good - typical recovery time
BreakDuration = TimeSpan.FromSeconds(30)

// ⚠️ Long - slow to retry
BreakDuration = TimeSpan.FromMinutes(5)
```

**Guidelines**:
- Fast-recovering services: 10-30 seconds
- Database/Cache: 30-60 seconds
- External APIs: 60-120 seconds

---

## 🎯 Use Cases

### 1. Event Publishing to RabbitMQ

**Scenario**: RabbitMQ becomes overloaded during traffic spike

```csharp
public class EventPublishingService
{
    private readonly ResiliencePipeline _pipeline;

    public EventPublishingService()
    {
        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2)
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 10,
                BreakDuration = TimeSpan.FromSeconds(30),
                OnOpened = args =>
                {
                    _logger.LogError("Event publishing circuit opened - RabbitMQ unavailable");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public async Task PublishEventAsync<T>(T @event)
    {
        try
        {
            await _pipeline.ExecuteAsync(async ct =>
            {
                await _eventBus.PublishAsync(@event);
            });
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning("Circuit open - storing event in outbox for later");
            await StoreInOutboxAsync(@event);
        }
    }
}
```

**Benefit**: Service continues operating even when RabbitMQ fails. Events stored in outbox for later delivery.

---

### 2. Database Queries

**Scenario**: Database experiencing high load

```csharp
var dbPipeline = new ResiliencePipelineBuilder()
    .AddCircuitBreaker(new CircuitBreakerStrategyOptions
    {
        FailureRatio = 0.4,  // Open at 40% failure
        MinimumThroughput = 20,
        BreakDuration = TimeSpan.FromMinutes(1)
    })
    .Build();

public async Task<Booking?> GetBookingAsync(Guid id)
{
    try
    {
        return await _dbPipeline.ExecuteAsync(async ct =>
        {
            return await _dbContext.Bookings
                .FirstOrDefaultAsync(b => b.Id == id, ct);
        });
    }
    catch (BrokenCircuitException)
    {
        _logger.LogWarning("Database circuit open - returning cached data");
        return await _cache.GetAsync<Booking>(id);
    }
}
```

**Benefit**: Graceful degradation - serve cached data when database fails.

---

### 3. External API Calls (Future)

```csharp
builder.Services.AddHttpClient("PaymentGatewayClient")
    .AddResilienceHandler("payment-gateway-resilience", builder =>
    {
        builder
            .AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1)
            })
            .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 10,
                BreakDuration = TimeSpan.FromMinutes(2)
            });
    });
```

**Benefit**: Protect payment service from failing external payment gateway.

---

## 📊 Monitoring Circuit Breaker

### Metrics to Track

1. **Circuit State Changes**:
   - When circuit opens (indicates sustained failure)
   - When circuit closes (indicates recovery)
   - Time spent in each state

2. **Rejection Rate**:
   - Number of requests rejected due to open circuit
   - Percentage of total requests

3. **Recovery Time**:
   - Time from circuit open to circuit close
   - Average recovery time over time

---

### Seq Queries

**Circuit State Changes**:
```sql
select @Timestamp, Service, @Message, CircuitState
from stream
where @Message like '%Circuit breaker%'
order by @Timestamp desc
```

**Failed Requests During Open Circuit**:
```sql
select count(*) as RejectedRequests
from stream
where Exception like '%BrokenCircuitException%'
  and @Timestamp > Now() - 1h
```

**Recovery Success Rate**:
```sql
select 
  Service,
  count(*) as CircuitOpenings,
  sum(case when NextState = 'Closed' then 1 else 0 end) as SuccessfulRecoveries
from stream
where @Message like '%Circuit breaker OPENED%'
group by Service
```

---

### Health Check Integration

```csharp
public class CircuitBreakerHealthCheck : IHealthCheck
{
    private readonly ResiliencePipeline _pipeline;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context)
    {
        // Check circuit state
        var circuitState = GetCircuitState(_pipeline);
        
        return circuitState switch
        {
            "Closed" => Task.FromResult(HealthCheckResult.Healthy("Circuit closed")),
            "HalfOpen" => Task.FromResult(HealthCheckResult.Degraded("Circuit half-open - testing")),
            "Open" => Task.FromResult(HealthCheckResult.Unhealthy("Circuit open - service unavailable")),
            _ => Task.FromResult(HealthCheckResult.Healthy())
        };
    }
}

builder.Services.AddHealthChecks()
    .AddCheck<CircuitBreakerHealthCheck>("circuit-breaker");
```

---

## ⚠️ Common Pitfalls

### 1. No Minimum Throughput

❌ **Problem**:
```csharp
// Opens on first failure (0% success with 1 request)
MinimumThroughput = 1
```

✅ **Solution**:
```csharp
// Wait for statistical significance
MinimumThroughput = 10
```

---

### 2. Break Duration Too Short

❌ **Problem**:
```csharp
// 5 seconds not enough for service to recover
BreakDuration = TimeSpan.FromSeconds(5)
```

✅ **Solution**:
```csharp
// Give service time to recover
BreakDuration = TimeSpan.FromSeconds(30)
```

---

### 3. Not Handling BrokenCircuitException

❌ **Problem**:
```csharp
await _pipeline.ExecuteAsync(async ct =>
{
    await CallServiceAsync();
});
// Exception propagates to caller
```

✅ **Solution**:
```csharp
try
{
    await _pipeline.ExecuteAsync(async ct =>
    {
        await CallServiceAsync();
    });
}
catch (BrokenCircuitException)
{
    // Provide fallback or graceful degradation
    return CachedData();
}
```

---

### 4. Circuit Breaker Before Retry

❌ **Wrong Order**:
```csharp
builder
    .AddCircuitBreaker(/* ... */)
    .AddRetry(/* ... */)  // Retry never happens!
```

✅ **Correct Order**:
```csharp
builder
    .AddRetry(/* ... */)  // Try retry first
    .AddCircuitBreaker(/* ... */)  // Then check for sustained failures
```

---

## 🎓 Key Takeaways

1. **Circuit breaker prevents cascading failures** by failing fast
2. **Three states**: Closed (normal), Open (blocking), Half-Open (testing)
3. **Use with retry**: Retry for transient errors, circuit breaker for sustained failures
4. **Configure failure ratio** based on service criticality (30-70%)
5. **Set appropriate break duration** to allow service recovery (30-60s typical)
6. **Handle BrokenCircuitException** for graceful degradation
7. **Monitor circuit state changes** to detect systemic issues
8. **Use minimum throughput** to avoid opening on insufficient data

---

## 📚 Further Reading

- [Martin Fowler: Circuit Breaker](https://martinfowler.com/bliki/CircuitBreaker.html)
- [Microsoft: Circuit Breaker Pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/circuit-breaker)
- [Polly Circuit Breaker Documentation](https://www.pollydocs.org/strategies/circuit-breaker.html)
- [Release It! by Michael Nygard](https://pragprog.com/titles/mnee2/release-it-second-edition/) (Chapter on Circuit Breaker)

---

## 🔗 Related Documentation

- [Retry Patterns with Polly](./retry-patterns-polly.md)
- [Connection Resilience](./connection-resilience.md)
- [Dead Letter Queue](./dead-letter-queue.md)

---

**Implementation Status**: 📋 Planned (Future Enhancement)  
**Priority**: Medium (Implement after core resilience patterns)  
**Estimated Effort**: 2-3 hours  
**Last Updated**: November 12, 2025

---

## 💡 Quick Reference

```csharp
// Basic circuit breaker configuration
var pipeline = new ResiliencePipelineBuilder()
    .AddCircuitBreaker(new CircuitBreakerStrategyOptions
    {
        FailureRatio = 0.5,              // Open at 50% failure
        MinimumThroughput = 10,          // After 10 requests
        BreakDuration = TimeSpan.FromSeconds(30),  // Stay open 30s
        SamplingDuration = TimeSpan.FromSeconds(10) // Sample window
    })
    .Build();

// Combine with retry
var combinedPipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 3 })
    .AddCircuitBreaker(new CircuitBreakerStrategyOptions { /* ... */ })
    .AddTimeout(TimeSpan.FromSeconds(10))
    .Build();

// Handle broken circuit
try
{
    await pipeline.ExecuteAsync(async ct => await OperationAsync());
}
catch (BrokenCircuitException)
{
    // Fallback logic
    return CachedValue();
}
```
