# Retry Patterns with Polly

## 📚 What is Retry Pattern?

**Retry pattern** is a resilience strategy that automatically retries failed operations, helping systems recover from **transient failures** - temporary errors that are likely to succeed if attempted again.

### Why Retry Matters in Microservices

```
❌ Without Retry:
Client → Service A → Network Glitch → Service B
                                       ↓
                                   Request Lost
                                   
✅ With Retry:
Client → Service A → Network Glitch → Service B (Attempt 1) ❌
                  ↓
                Wait 2s
                  ↓
                → Service B (Attempt 2) ✅ Success!
```

**Common Transient Failures**:
- Network packet loss
- Database connection timeouts
- Service restarts
- Message broker temporary unavailability
- Rate limiting (HTTP 429)
- Temporary resource exhaustion

---

## 🔧 What is Polly?

**Polly** is a .NET resilience and transient-fault-handling library that provides:
- ✅ Retry policies
- ✅ Circuit breaker patterns
- ✅ Timeout policies
- ✅ Bulkhead isolation
- ✅ Fallback mechanisms
- ✅ Policy composition

**Official Site**: https://www.pollydocs.org/

### Polly v8 (Modern API)

This project uses **Polly v8** with the new Resilience Pipeline API:

```csharp
// Install
dotnet add package Polly --version 8.6.4

// Configure
var pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(2),
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true
    })
    .Build();

// Execute
await pipeline.ExecuteAsync(async ct =>
{
    await DoSomethingAsync();
}, cancellationToken);
```

---

## 📖 Retry Strategies

### 1. Simple Retry (Immediate)

Retries immediately without delay.

```csharp
var pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.Zero
    })
    .Build();
```

**Timeline**:
```
Attempt 1 → Fail (0ms) → Attempt 2 → Fail (0ms) → Attempt 3 → Success ✅
```

**Use Case**: Fast operations where delay doesn't help (e.g., reading from cache).

**Pros**: Very fast recovery
**Cons**: Can overwhelm already-struggling services

---

### 2. Fixed Delay Retry

Waits a fixed amount of time between retries.

```csharp
var pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(2)
    })
    .Build();
```

**Timeline**:
```
Attempt 1 → Fail → Wait 2s → Attempt 2 → Fail → Wait 2s → Attempt 3 ✅
```

**Use Case**: Operations with predictable recovery time.

**Pros**: Simple, predictable
**Cons**: Doesn't adapt to service load

---

### 3. Exponential Backoff ⭐ (Recommended)

Increases delay exponentially after each retry.

```csharp
var pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 5,
        Delay = TimeSpan.FromSeconds(2),
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        MaxDelay = TimeSpan.FromSeconds(60)
    })
    .Build();
```

**Timeline**:
```
Attempt 1 → Fail → Wait 2s →
Attempt 2 → Fail → Wait 4s →
Attempt 3 → Fail → Wait 8s →
Attempt 4 → Fail → Wait 16s →
Attempt 5 → Success ✅

Total wait time: 30 seconds
```

**Why Exponential Backoff?**
- ✅ Gives services time to recover
- ✅ Reduces load on struggling services
- ✅ Prevents "thundering herd" problem
- ✅ Industry best practice (AWS, Google, Microsoft)

**Pros**: Adapts to service load, reduces cascading failures
**Cons**: Longer recovery time

---

### 4. Jitter Strategy

Adds randomness to delays to avoid synchronized retries.

```csharp
var pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(2),
        UseJitter = true // ✅ Randomizes delay ±25%
    })
    .Build();
```

**Problem Without Jitter**:
```
Service restarts → 100 clients all retry at exactly 2s, 4s, 8s
                → Traffic spikes → Service overwhelmed again
```

**Solution With Jitter**:
```
Service restarts → Clients retry at 1.8s, 2.3s, 1.9s, 2.1s, etc.
                → Smooth traffic distribution → Service recovers
```

**AWS Research**: Jitter can reduce retry collisions by 95%

---

## 💡 Implementation in This Project

### Event Publishing Resilience

**File**: `PaymentService/Services/ResiliencePipelineService.cs`

```csharp
private ResiliencePipeline CreateEventPublishingPipeline()
{
    return new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(2),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            OnRetry = args =>
            {
                _logger.LogWarning(
                    "Event publishing retry {Attempt}/{MaxAttempts} after {Delay}ms",
                    args.AttemptNumber,
                    3,
                    args.RetryDelay.TotalMilliseconds);
                return ValueTask.CompletedTask;
            }
        })
        .AddTimeout(TimeSpan.FromSeconds(10))
        .Build();
}
```

**Usage in PaymentServiceImpl**:
```csharp
private async Task PublishPaymentSucceededEventAsync(Payment payment)
{
    var paymentEvent = CreateEvent(payment);
    
    try
    {
        // ✅ Execute with retry policy
        await _resiliencePipeline.ExecuteAsync(async ct =>
        {
            await _eventBus.PublishAsync(paymentEvent, queueName, ct);
        }, CancellationToken.None);
        
        _logger.LogInformation("Event published successfully");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to publish event after 3 retries");
        // Store in dead-letter queue for manual investigation
        throw;
    }
}
```

**Retry Timeline Example**:
```
Publish attempt 1 → RabbitMQ down → Fail
Wait 2 seconds
Publish attempt 2 → RabbitMQ still down → Fail
Wait 4 seconds (exponential backoff)
Publish attempt 3 → RabbitMQ back online → Success ✅

Total time: ~6 seconds
```

---

### Database Operations Resilience

```csharp
private ResiliencePipeline CreateDatabasePipeline()
{
    return new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 5,
            Delay = TimeSpan.FromSeconds(1),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = new PredicateBuilder()
                .Handle<TimeoutException>()
                .Handle<MongoDB.Driver.MongoConnectionException>()
        })
        .AddTimeout(TimeSpan.FromSeconds(30))
        .Build();
}
```

**Handled Exceptions**:
- `TimeoutException` - Database query timeout
- `MongoConnectionException` - MongoDB connection lost
- `MongoExecutionTimeoutException` - Operation timeout

---

### RabbitMQ Connection Retry

**File**: `PaymentService/EventBus/RabbitMQEventBus.cs`

```csharp
private ResiliencePipeline CreateConnectionResiliencePipeline()
{
    return new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 10,
            Delay = TimeSpan.FromSeconds(5),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            MaxDelay = TimeSpan.FromSeconds(60),
            ShouldHandle = new PredicateBuilder()
                .Handle<BrokerUnreachableException>()
                .Handle<SocketException>()
                .Handle<TimeoutException>()
        })
        .Build();
}
```

**Why 10 Attempts?**
- Allows ~8 minutes for RabbitMQ to fully start
- Handles orchestration delays in Docker/Kubernetes
- Prevents service crash during infrastructure startup

**Retry Timeline**:
```
Attempt 1: 0s   → Fail
Attempt 2: 5s   → Fail
Attempt 3: 15s  → Fail
Attempt 4: 35s  → Fail
Attempt 5: 75s  → Fail (1m 15s)
Attempt 6: 135s → Success ✅ (2m 15s)

RabbitMQ had 2 minutes to start up
```

---

## 🎯 Best Practices

### 1. Choose Appropriate Max Attempts

```csharp
// ❌ Too few - gives up too quickly
MaxRetryAttempts = 1

// ✅ Good for most scenarios
MaxRetryAttempts = 3

// ⚠️ Use carefully - can delay failure detection
MaxRetryAttempts = 10
```

**Recommended by Operation Type**:

| Operation | Max Attempts | Rationale |
|-----------|--------------|-----------|
| Event Publishing | 3 | Fast feedback loop |
| Database Queries | 5 | Connection pools need time |
| Connection Establishment | 10 | Infrastructure startup |
| Cache Operations | 2 | Fast recovery expected |

---

### 2. Always Use Timeouts

```csharp
var pipeline = new ResiliencePipelineBuilder()
    .AddRetry(/* ... */)
    .AddTimeout(TimeSpan.FromSeconds(30)) // ✅ Prevent hanging
    .Build();
```

**Why?** Without timeout, retry can hang indefinitely if operation never completes.

---

### 3. Log Retry Attempts

```csharp
OnRetry = args =>
{
    _logger.LogWarning(
        "Retry {Attempt}/{Max} after {Delay}ms. Error: {Error}",
        args.AttemptNumber,
        maxRetries,
        args.RetryDelay.TotalMilliseconds,
        args.Outcome.Exception?.Message);
    
    return ValueTask.CompletedTask;
}
```

**Benefits**:
- ✅ Debugging transient issues
- ✅ Identifying patterns in failures
- ✅ Monitoring retry rates

---

### 4. Don't Retry Everything

Some failures should **not** be retried:

```csharp
ShouldHandle = new PredicateBuilder()
    .Handle<HttpRequestException>()      // ✅ Retry network errors
    .Handle<TimeoutException>()          // ✅ Retry timeouts
    .HandleResult(r => r.StatusCode == HttpStatusCode.TooManyRequests) // ✅ Retry rate limits
    // ❌ Don't retry validation errors (400, 422)
    // ❌ Don't retry authorization errors (401, 403)
    // ❌ Don't retry not found errors (404)
```

**Rule of Thumb**: Only retry **transient** failures (temporary errors that might succeed later).

---

### 5. Combine with Idempotency

Ensure operations can be safely retried:

```csharp
// ✅ Idempotent - safe to retry
var existingPayment = await _dbContext.Payments
    .FirstOrDefaultAsync(p => p.BookingId == bookingId);

if (existingPayment != null)
{
    _logger.LogWarning("Payment already exists");
    return existingPayment; // Don't create duplicate
}

// Now safe to create
var payment = new Payment { /* ... */ };
await _dbContext.Payments.InsertOneAsync(payment);
```

**Without idempotency check**: Retry creates duplicate payments ❌

---

### 6. Use Correlation IDs

Track retries across the system:

```csharp
var correlationId = Guid.NewGuid();

_logger.LogInformation(
    "Request {CorrelationId} - Attempt {Attempt}",
    correlationId,
    attemptNumber);
```

**In Seq Query**:
```sql
select * from stream
where CorrelationId = 'abc-123-def-456'
order by @Timestamp
```

See entire retry flow across all services.

---

## 🧪 Testing Retry Logic

### Unit Test Example

```csharp
[Fact]
public async Task EventPublishing_RetriesOnFailure_ThenSucceeds()
{
    // Arrange
    var attemptCount = 0;
    var pipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 3 })
        .Build();

    // Act
    await pipeline.ExecuteAsync(async ct =>
    {
        attemptCount++;
        if (attemptCount < 3)
            throw new Exception("Simulated failure");
        
        await Task.CompletedTask; // Success on 3rd attempt
    });

    // Assert
    Assert.Equal(3, attemptCount);
}
```

### Chaos Engineering Test

```csharp
public class ChaosEventBus : IEventBus
{
    private int _callCount = 0;

    public async Task PublishAsync<T>(T @event, string queueName)
    {
        _callCount++;
        
        // Fail first 2 attempts
        if (_callCount <= 2)
            throw new Exception("Chaos failure!");
        
        // Succeed on 3rd attempt
        await _innerEventBus.PublishAsync(@event, queueName);
    }
}
```

---

## 📊 Monitoring Retry Patterns

### Seq Dashboard Queries

**Total Retry Attempts (Last Hour)**:
```sql
select count(*) as RetryCount
from stream
where @Message like '%retry%'
  and @Timestamp > Now() - 1h
```

**Failed After Exhausting Retries**:
```sql
select Service, OperationName, ErrorMessage, count(*) as FailureCount
from stream
where @Message like '%failed after retries%'
group by Service, OperationName, ErrorMessage
order by FailureCount desc
```

**Average Retry Success Rate**:
```sql
select 
  count(*) as TotalRetries,
  sum(case when @Message like '%succeeded after retry%' then 1 else 0 end) as SuccessAfterRetry,
  (sum(case when @Message like '%succeeded after retry%' then 1 else 0 end) * 100.0 / count(*)) as SuccessRate
from stream
where @Message like '%retry%'
```

### Metrics to Track

1. **Retry Rate**: Attempts per minute
2. **Success Rate**: % of operations that succeed after retry
3. **Exhaustion Rate**: % of operations that fail after max retries
4. **Average Retry Count**: Average attempts before success
5. **Retry Duration**: Time spent in retry loops

---

## ⚠️ Common Pitfalls

### 1. Retrying Non-Idempotent Operations

❌ **Problem**:
```csharp
await _resiliencePipeline.ExecuteAsync(async ct =>
{
    var payment = new Payment { Amount = 100 };
    await _dbContext.Payments.InsertOneAsync(payment); // Creates duplicate on retry!
});
```

✅ **Solution**:
```csharp
// Check for duplicates first
var existing = await _dbContext.Payments
    .FirstOrDefaultAsync(p => p.BookingId == bookingId);
if (existing != null) return existing;

await _resiliencePipeline.ExecuteAsync(async ct =>
{
    var payment = new Payment { Amount = 100 };
    await _dbContext.Payments.InsertOneAsync(payment);
});
```

---

### 2. Retrying Validation Errors

❌ **Problem**:
```csharp
await _resiliencePipeline.ExecuteAsync(async ct =>
{
    // Will retry forever - amount will never be valid!
    if (request.Amount <= 0)
        throw new ArgumentException("Invalid amount");
});
```

✅ **Solution**:
```csharp
// Validate BEFORE retry logic
if (request.Amount <= 0)
    throw new ArgumentException("Invalid amount");

await _resiliencePipeline.ExecuteAsync(async ct =>
{
    // Only transient operations here
});
```

---

### 3. No Jitter - Thundering Herd

❌ **Problem**:
```csharp
// All 1000 clients retry at exactly the same time
Delay = TimeSpan.FromSeconds(5)
```

✅ **Solution**:
```csharp
// Randomize delays to spread load
Delay = TimeSpan.FromSeconds(5),
UseJitter = true // Adds ±25% randomness
```

---

## 🎓 Key Takeaways

1. **Retry logic is essential** in distributed systems to handle transient failures
2. **Exponential backoff with jitter** is the industry standard retry strategy
3. **Always set max retry attempts** to prevent infinite loops
4. **Combine with timeouts** to prevent hanging operations
5. **Only retry transient errors** - validation errors should fail immediately
6. **Ensure idempotency** before applying retry logic
7. **Log retry attempts** for monitoring and debugging
8. **Track correlation IDs** to trace requests across retries

---

## 📚 Further Reading

- [Polly Official Documentation](https://www.pollydocs.org/)
- [AWS: Exponential Backoff and Jitter](https://aws.amazon.com/blogs/architecture/exponential-backoff-and-jitter/)
- [Microsoft: Implement resilient applications](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/)
- [Martin Fowler: Transient Fault Handling](https://martinfowler.com/bliki/TransientFault.html)

---

## 🔗 Related Documentation

- [Connection Resilience](./connection-resilience.md) - RabbitMQ connection retry
- [Dead Letter Queue](./dead-letter-queue.md) - Handling exhausted retries
- [Circuit Breaker](./circuit-breaker.md) - Preventing cascading failures
- [Complete Retry Documentation](/docs/phase3-event-integration/RETRY_LOGIC_AND_POLLY.md)

---

**Implementation Status**: ✅ Production Ready  
**Last Updated**: November 12, 2025
