# Retry Logic and Resilience with Polly

## Table of Contents
- [Overview](#overview)
- [Why Retry Logic Matters](#why-retry-logic-matters)
- [Introduction to Polly](#introduction-to-polly)
- [Current System Analysis](#current-system-analysis)
- [Retry Strategies](#retry-strategies)
- [Implementation Guide](#implementation-guide)
- [Polly in Action: Use Cases](#polly-in-action-use-cases)
- [Best Practices](#best-practices)
- [Testing Retry Logic](#testing-retry-logic)
- [Monitoring and Observability](#monitoring-and-observability)
- [Common Pitfalls](#common-pitfalls)

---

## Overview

**Retry logic** is a resilience pattern that automatically retries failed operations, helping systems recover from transient failures. In distributed microservices architectures, temporary failures are inevitable due to:

- Network glitches
- Service restarts
- Database connection timeouts
- Message broker unavailability
- Rate limiting
- Temporary resource exhaustion

**Polly** is a .NET resilience and transient-fault-handling library that provides:
- Retry policies
- Circuit breaker patterns
- Timeout policies
- Bulkhead isolation
- Fallback mechanisms
- Policy wrapping (combining multiple strategies)

---

## Why Retry Logic Matters

### The Problem: Transient Failures

In a microservices system, services communicate over networks, which are inherently unreliable:

```
❌ Without Retry Logic:
Client → BookingService → [Network Glitch] → RabbitMQ
                                                  ↓
                                           Event Lost ❌
                                                  ↓
                                        Payment Never Processed
                                                  ↓
                                           Booking Stuck in PENDING
```

```
✅ With Retry Logic:
Client → BookingService → [Network Glitch] → RabbitMQ (Attempt 1) ❌
                        ↓
                   Wait 2s
                        ↓
                → RabbitMQ (Attempt 2) ❌
                        ↓
                   Wait 4s
                        ↓
                → RabbitMQ (Attempt 3) ✅
                        ↓
                  Event Published Successfully
```

### Business Impact

| Scenario | Without Retry | With Retry | Business Value |
|----------|---------------|------------|----------------|
| RabbitMQ temporary unavailable | Booking fails | Booking succeeds after retry | Customer satisfaction |
| Database connection timeout | Payment lost | Payment processed | Revenue protected |
| Network packet loss | Event lost | Event delivered | Data consistency |
| Service restart | Request fails | Request succeeds | System availability |

---

## Introduction to Polly

### What is Polly?

Polly is a .NET library that helps you build resilient applications by defining **policies** for handling failures.

**Official Documentation**: https://github.com/App-vNext/Polly

### Core Concepts

#### 1. **Policy**
A rule that defines how to handle failures (e.g., "retry 3 times with 2-second delays").

#### 2. **Resilience Pipeline**
Polly v8 introduces a modern way to compose multiple resilience strategies.

#### 3. **Transient Faults**
Temporary failures that are likely to succeed if retried (network errors, timeouts, service unavailable).

### Polly v8 vs Earlier Versions

This guide focuses on **Polly v8** (modern API):

```csharp
// Polly v8 (Modern - Used in PaymentService)
var pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(2)
    })
    .Build();

await pipeline.ExecuteAsync(async ct => 
{
    await DoSomethingAsync();
}, cancellationToken);
```

```csharp
// Polly v7 (Legacy - For reference)
var policy = Policy
    .Handle<HttpRequestException>()
    .WaitAndRetryAsync(3, retryAttempt => 
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

await policy.ExecuteAsync(async () => 
{
    await DoSomethingAsync();
});
```

---

## Current System Analysis

### Retry Capabilities in Current Implementation

#### 1. **RabbitMQ Consumer Retry** ✅ (Basic)

**Location**: `BookingService/Consumers/PaymentSucceededConsumer.cs`

```csharp
private async Task HandleMessageAsync(BasicDeliverEventArgs ea)
{
    try
    {
        // Process message
        await ProcessPaymentSucceededAsync(paymentEvent);
        
        // ✅ Success - acknowledge
        _channel!.BasicAck(ea.DeliveryTag, false);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error processing event");
        
        // ❌ Failure - requeue for retry
        _channel!.BasicNack(ea.DeliveryTag, false, requeue: true);
    }
}
```

**Current Behavior**:
- **Pros**: Failed messages are automatically requeued
- **Cons**: 
  - No exponential backoff (immediate retry)
  - No max retry limit (infinite retries possible)
  - Can cause "poison message" scenarios

#### 2. **Event Publishing** ❌ (No Retry)

**Location**: `PaymentService/Services/PaymentServiceImpl.cs`

```csharp
private async Task PublishPaymentSucceededEventAsync(Payment payment)
{
    try
    {
        await _eventBus.PublishAsync(paymentEvent, queueName);
        _logger.LogInformation("Event published");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to publish event");
        // ⚠️ Event is lost - no retry mechanism
        // Note: In production, you might want to implement retry logic...
    }
}
```

**Current Behavior**:
- **Pros**: Failure is logged
- **Cons**: Event is permanently lost if publish fails

#### 3. **HTTP Calls** ⚠️ (Not Applicable)

Currently, services don't make HTTP calls to each other (event-driven architecture), but if they did, there would be no retry logic.

### Gaps and Opportunities

| Component | Current State | Recommended Enhancement |
|-----------|---------------|-------------------------|
| Event Publishing | No retry | Add Polly retry with exponential backoff |
| Event Consumption | Basic requeue | Add Polly with max retry attempts |
| Database Operations | No retry | Add Polly for transient DB errors |
| RabbitMQ Connection | Manual reconnect | Add Polly for connection establishment |
| Future HTTP Calls | N/A | Pre-configure Polly for resilience |

---

## Retry Strategies

### 1. **Simple Retry**

Retries immediately without delay.

```csharp
var pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.Zero // Immediate retry
    })
    .Build();
```

**Use Case**: Fast operations where delay doesn't help (e.g., reading from local cache).

**Timeline**:
```
Attempt 1 → Fail (0ms) → Attempt 2 → Fail (0ms) → Attempt 3 → Success ✅
```

### 2. **Fixed Delay Retry**

Waits a fixed amount of time between retries.

```csharp
var pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(2) // Fixed 2s delay
    })
    .Build();
```

**Use Case**: Network operations with predictable recovery time.

**Timeline**:
```
Attempt 1 → Fail → Wait 2s → Attempt 2 → Fail → Wait 2s → Attempt 3 → Success ✅
```

### 3. **Exponential Backoff** ⭐ (Recommended)

Increases delay exponentially after each retry.

```csharp
var pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 5,
        Delay = TimeSpan.FromSeconds(2),
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true // Add randomness to prevent thundering herd
    })
    .Build();
```

**Use Case**: Most production scenarios, especially for external services.

**Timeline**:
```
Attempt 1 → Fail → Wait 2s → 
Attempt 2 → Fail → Wait 4s → 
Attempt 3 → Fail → Wait 8s → 
Attempt 4 → Fail → Wait 16s → 
Attempt 5 → Success ✅
```

**Why Exponential Backoff?**
- Gives services time to recover
- Reduces load on struggling services
- Prevents "thundering herd" problem
- Industry best practice

### 4. **Jitter Strategy**

Adds randomness to delays to avoid synchronized retries.

```csharp
var pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(2),
        UseJitter = true // Randomizes delay slightly
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

---

## Implementation Guide

### Step 1: Install Polly (Already Done for PaymentService ✅)

```xml
<PackageReference Include="Polly" Version="8.6.4" />
```

**Install for other services**:
```bash
cd src/BookingService
dotnet add package Polly --version 8.6.4

cd ../ApiGateway
dotnet add package Polly --version 8.6.4
```

### Step 2: Configure Retry Policies in Dependency Injection

#### Option A: Service-Level Configuration (Recommended)

**Location**: `Program.cs`

```csharp
using Polly;
using Polly.Retry;

var builder = WebApplication.CreateBuilder(args);

// Configure Polly Resilience Pipelines
builder.Services.AddResiliencePipeline("event-publishing", builder =>
{
    builder.AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(2),
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        OnRetry = args =>
        {
            var logger = args.Context.ServiceProvider
                .GetRequiredService<ILogger<Program>>();
            
            logger.LogWarning(
                "Retry attempt {Attempt} after {Delay}ms due to: {Exception}",
                args.AttemptNumber,
                args.RetryDelay.TotalMilliseconds,
                args.Outcome.Exception?.Message ?? "Unknown error");
            
            return ValueTask.CompletedTask;
        }
    });
});

builder.Services.AddResiliencePipeline("database-operations", builder =>
{
    builder.AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 5,
        Delay = TimeSpan.FromSeconds(1),
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        ShouldHandle = new PredicateBuilder().Handle<TimeoutException>()
            .Handle<Npgsql.NpgsqlException>()
            .Handle<MongoDB.Driver.MongoConnectionException>()
    });
});

// The rest of your service configuration...
```

#### Option B: Custom Resilience Service

**Create**: `Services/ResiliencePipelineService.cs`

```csharp
using Polly;
using Polly.Retry;

namespace PaymentService.Services;

public interface IResiliencePipelineService
{
    ResiliencePipeline GetEventPublishingPipeline();
    ResiliencePipeline GetDatabasePipeline();
}

public class ResiliencePipelineService : IResiliencePipelineService
{
    private readonly ResiliencePipeline _eventPublishingPipeline;
    private readonly ResiliencePipeline _databasePipeline;
    private readonly ILogger<ResiliencePipelineService> _logger;

    public ResiliencePipelineService(ILogger<ResiliencePipelineService> logger)
    {
        _logger = logger;
        _eventPublishingPipeline = CreateEventPublishingPipeline();
        _databasePipeline = CreateDatabasePipeline();
    }

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
                        "Event publishing retry {Attempt}/{MaxAttempts} after {Delay}ms. Error: {Error}",
                        args.AttemptNumber,
                        3,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message ?? "Unknown");
                    
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(10)) // Overall timeout
            .Build();
    }

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
            .Build();
    }

    public ResiliencePipeline GetEventPublishingPipeline() => _eventPublishingPipeline;
    public ResiliencePipeline GetDatabasePipeline() => _databasePipeline;
}
```

**Register in `Program.cs`**:

```csharp
builder.Services.AddSingleton<IResiliencePipelineService, ResiliencePipelineService>();
```

### Step 3: Apply Retry Logic to Event Publishing

**Update**: `PaymentService/Services/PaymentServiceImpl.cs`

```csharp
using Polly;

public class PaymentServiceImpl : IPaymentService
{
    private readonly IEventBus _eventBus;
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly ILogger<PaymentServiceImpl> _logger;

    public PaymentServiceImpl(
        IEventBus eventBus,
        IResiliencePipelineService resiliencePipelineService,
        ILogger<PaymentServiceImpl> logger)
    {
        _eventBus = eventBus;
        _resiliencePipeline = resiliencePipelineService.GetEventPublishingPipeline();
        _logger = logger;
    }

    private async Task PublishPaymentSucceededEventAsync(Payment payment)
    {
        var paymentEvent = new PaymentSucceededEvent
        {
            EventId = Guid.NewGuid(),
            EventName = "PaymentSucceeded",
            Timestamp = DateTime.UtcNow,
            Data = new PaymentSucceededData
            {
                PaymentId = payment.Id,
                BookingId = payment.BookingId,
                Amount = payment.Amount,
                Status = "SUCCESS"
            }
        };

        var queueName = _rabbitMQSettings.Queues
            .GetValueOrDefault("PaymentSucceeded", "payment_succeeded");

        try
        {
            // ✅ Execute with retry policy
            await _resiliencePipeline.ExecuteAsync(async ct =>
            {
                await _eventBus.PublishAsync(paymentEvent, queueName, ct);
            }, CancellationToken.None);

            _logger.LogInformation(
                "PaymentSucceeded event published for PaymentId: {PaymentId}, BookingId: {BookingId}",
                payment.Id, payment.BookingId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to publish PaymentSucceeded event after retries for PaymentId: {PaymentId}",
                payment.Id);
            
            // Consider: Store event in dead-letter table for manual retry
            throw;
        }
    }
}
```

### Step 4: Enhanced RabbitMQ Consumer with Polly

**Update**: `BookingService/Consumers/PaymentSucceededConsumer.cs`

```csharp
using Polly;
using Polly.Retry;

public class PaymentSucceededConsumer : BackgroundService
{
    private readonly ResiliencePipeline _resiliencePipeline;
    private int _retryCount = 0;
    private const int MAX_REQUEUE_ATTEMPTS = 3;

    public PaymentSucceededConsumer(
        IServiceProvider serviceProvider,
        IOptions<RabbitMQSettings> settings,
        ILogger<PaymentSucceededConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _settings = settings.Value;
        _logger = logger;
        
        // Create inline resilience pipeline for message processing
        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "Retrying message processing. Attempt {Attempt}",
                        args.AttemptNumber);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    private async Task HandleMessageAsync(BasicDeliverEventArgs ea)
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);

        try
        {
            _logger.LogInformation("Received PaymentSucceeded event: {Message}", message);

            var paymentEvent = JsonSerializer.Deserialize<PaymentSucceededEvent>(message);
            
            if (paymentEvent?.Data == null)
            {
                _logger.LogWarning("Invalid PaymentSucceeded event format");
                // ❌ Permanent failure - don't requeue
                _channel!.BasicNack(ea.DeliveryTag, false, requeue: false);
                return;
            }

            // ✅ Process with retry policy
            await _resiliencePipeline.ExecuteAsync(async ct =>
            {
                await ProcessPaymentSucceededAsync(paymentEvent);
            }, CancellationToken.None);

            // ✅ Success - acknowledge
            _channel!.BasicAck(ea.DeliveryTag, false);
            _retryCount = 0; // Reset counter
            
            _logger.LogInformation(
                "PaymentSucceeded event processed successfully for BookingId: {BookingId}",
                paymentEvent.Data.BookingId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PaymentSucceeded event: {Message}", message);
            
            _retryCount++;
            
            if (_retryCount >= MAX_REQUEUE_ATTEMPTS)
            {
                // ❌ Max retries reached - send to dead letter queue or log
                _logger.LogError(
                    "Message failed after {Attempts} requeue attempts. Moving to DLQ.",
                    MAX_REQUEUE_ATTEMPTS);
                
                _channel!.BasicNack(ea.DeliveryTag, false, requeue: false);
                _retryCount = 0;
                
                // TODO: Store in dead-letter table for manual investigation
            }
            else
            {
                // ⚠️ Requeue for retry
                _logger.LogWarning("Requeuing message. Attempt {Attempt}/{Max}",
                    _retryCount, MAX_REQUEUE_ATTEMPTS);
                
                _channel!.BasicNack(ea.DeliveryTag, false, requeue: true);
            }
        }
    }
}
```

### Step 5: Add Circuit Breaker (Optional but Recommended)

Circuit breaker prevents cascading failures by "opening" after consecutive failures.

```csharp
builder.Services.AddResiliencePipeline("event-publishing-with-breaker", builder =>
{
    // Step 1: Retry
    builder.AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(2),
        BackoffType = DelayBackoffType.Exponential
    });

    // Step 2: Circuit Breaker
    builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
    {
        FailureRatio = 0.5, // Open if 50% of requests fail
        MinimumThroughput = 10, // Need at least 10 requests before evaluating
        BreakDuration = TimeSpan.FromSeconds(30), // Stay open for 30s
        OnOpened = args =>
        {
            var logger = args.Context.ServiceProvider
                .GetRequiredService<ILogger<Program>>();
            logger.LogError("Circuit breaker OPENED - Event publishing unavailable");
            return ValueTask.CompletedTask;
        },
        OnClosed = args =>
        {
            var logger = args.Context.ServiceProvider
                .GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Circuit breaker CLOSED - Event publishing restored");
            return ValueTask.CompletedTask;
        }
    });

    // Step 3: Timeout
    builder.AddTimeout(TimeSpan.FromSeconds(10));
});
```

**Circuit Breaker States**:

```
CLOSED (Normal)
    ↓ (50% failure rate)
OPEN (Block all requests)
    ↓ (After 30s)
HALF-OPEN (Test if service recovered)
    ↓ (Success) or ↓ (Failure)
CLOSED           OPEN
```

---

## Polly in Action: Use Cases

### Use Case 1: Resilient Event Publishing

**Scenario**: RabbitMQ is restarting during deployment.

```csharp
// Without Polly
await _eventBus.PublishAsync(event, queue);
// ❌ Exception: Connection refused → Event lost forever

// With Polly
await _resiliencePipeline.ExecuteAsync(async ct =>
{
    await _eventBus.PublishAsync(event, queue, ct);
});
// ✅ Retry 1: Failed
// ✅ Retry 2 (after 2s): Failed
// ✅ Retry 3 (after 4s): Success - RabbitMQ back online
```

### Use Case 2: Database Connection Resilience

**Scenario**: PostgreSQL connection pool exhausted temporarily.

```csharp
await _databasePipeline.ExecuteAsync(async ct =>
{
    var booking = await _dbContext.Bookings
        .FirstOrDefaultAsync(b => b.Id == bookingId, ct);
    
    booking.Status = "CONFIRMED";
    await _dbContext.SaveChangesAsync(ct);
});
```

### Use Case 3: RabbitMQ Connection Establishment

**Update**: `Shared/EventBus/RabbitMQEventBus.cs` (if exists)

```csharp
public class RabbitMQEventBus : IEventBus
{
    private IConnection? _connection;
    private readonly ResiliencePipeline _connectionPipeline;

    public RabbitMQEventBus(ILogger<RabbitMQEventBus> logger)
    {
        _connectionPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 10,
                Delay = TimeSpan.FromSeconds(5),
                BackoffType = DelayBackoffType.Exponential,
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "RabbitMQ connection retry {Attempt}",
                        args.AttemptNumber);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    private void EnsureConnection()
    {
        if (_connection != null && _connection.IsOpen)
            return;

        _connectionPipeline.Execute(() =>
        {
            var factory = new ConnectionFactory { /* settings */ };
            _connection = factory.CreateConnection();
        });
    }
}
```

### Use Case 4: Future HTTP Client Calls (Proactive)

When services start making HTTP calls:

```csharp
builder.Services.AddHttpClient("BookingServiceClient")
    .AddResilienceHandler("http-resilience", builder =>
    {
        builder.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(1),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true
        });
        
        builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            MinimumThroughput = 10,
            BreakDuration = TimeSpan.FromSeconds(30)
        });
        
        builder.AddTimeout(TimeSpan.FromSeconds(10));
    });
```

---

## Best Practices

### 1. **Choose Appropriate Retry Strategies**

| Operation Type | Strategy | Reason |
|----------------|----------|--------|
| Event Publishing | Exponential backoff + jitter | Gives broker time to recover |
| Database Queries | Exponential backoff (5 attempts) | Connection pools need time |
| HTTP API Calls | Exponential backoff + circuit breaker | Protect downstream services |
| Cache Reads | Simple retry (2 attempts) | Fast recovery, no backpressure issue |

### 2. **Set Reasonable Max Attempts**

```csharp
// ❌ Too few retries - gives up too quickly
MaxRetryAttempts = 1

// ✅ Good for most scenarios
MaxRetryAttempts = 3

// ⚠️ Use carefully - can delay failure detection
MaxRetryAttempts = 10
```

### 3. **Always Use Timeouts**

```csharp
var pipeline = new ResiliencePipelineBuilder()
    .AddRetry(/* ... */)
    .AddTimeout(TimeSpan.FromSeconds(30)) // ✅ Prevent hanging
    .Build();
```

### 4. **Log Retry Attempts**

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

### 5. **Don't Retry Everything**

Some failures should **not** be retried:

```csharp
ShouldHandle = new PredicateBuilder()
    .Handle<HttpRequestException>() // ✅ Retry network errors
    .Handle<TimeoutException>()     // ✅ Retry timeouts
    .HandleResult(r => r.StatusCode == HttpStatusCode.TooManyRequests) // ✅ Retry rate limits
    // ❌ Don't retry validation errors (400, 422)
    // ❌ Don't retry authorization errors (401, 403)
    // ❌ Don't retry not found errors (404)
```

### 6. **Combine with Idempotency**

Ensure operations can be safely retried:

```csharp
// ✅ Idempotent - safe to retry
if (existingPayment != null)
{
    _logger.LogWarning("Payment already exists");
    return existingPayment; // Don't create duplicate
}

// ❌ Not idempotent - creates duplicates on retry
var payment = new Payment { /* ... */ };
await _dbContext.Payments.InsertOneAsync(payment);
```

### 7. **Use Correlation IDs**

Track retries across the system:

```csharp
var correlationId = Guid.NewGuid();

_logger.LogInformation("Request {CorrelationId} - Attempt {Attempt}",
    correlationId, attemptNumber);
```

### 8. **Configure for Environment**

```csharp
// Development - fail fast for debugging
MaxRetryAttempts = builder.Environment.IsDevelopment() ? 1 : 3

// Production - more resilient
MaxRetryAttempts = builder.Environment.IsProduction() ? 5 : 2
```

---

## Testing Retry Logic

### 1. **Unit Testing Polly Policies**

```csharp
using Xunit;
using Polly;

public class ResiliencePipelineTests
{
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

    [Fact]
    public async Task EventPublishing_ExhaustsRetries_ThrowsException()
    {
        // Arrange
        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 2 })
            .Build();

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(async () =>
        {
            await pipeline.ExecuteAsync(async ct =>
            {
                throw new Exception("Always fails");
            });
        });
    }
}
```

### 2. **Integration Testing with Testcontainers**

```csharp
public class EventPublishingRetryTests : IAsyncLifetime
{
    private RabbitMQContainer _rabbitMqContainer;

    public async Task InitializeAsync()
    {
        _rabbitMqContainer = new RabbitMQContainer();
        await _rabbitMqContainer.StartAsync();
    }

    [Fact]
    public async Task PublishEvent_RabbitMQRestart_EventuallySucceeds()
    {
        // Arrange
        var eventBus = CreateEventBus();

        // Act
        var publishTask = eventBus.PublishAsync(new TestEvent(), "test_queue");
        
        // Simulate RabbitMQ restart mid-operation
        await _rabbitMqContainer.StopAsync();
        await Task.Delay(2000);
        await _rabbitMqContainer.StartAsync();

        // Assert
        await publishTask; // Should succeed after retries
    }
}
```

### 3. **Chaos Engineering**

Inject failures to test resilience:

```csharp
public class ChaosEventBus : IEventBus
{
    private readonly IEventBus _innerEventBus;
    private int _callCount = 0;

    public async Task PublishAsync<T>(T @event, string queueName, CancellationToken ct)
    {
        _callCount++;
        
        // Fail first 2 attempts
        if (_callCount <= 2)
            throw new Exception("Chaos failure!");
        
        // Succeed on 3rd attempt
        await _innerEventBus.PublishAsync(@event, queueName, ct);
    }
}
```

---

## Monitoring and Observability

### 1. **Structured Logging with Serilog**

```csharp
OnRetry = args =>
{
    _logger.LogWarning(
        "Retry {AttemptNumber}/{MaxRetries} for {Operation} after {Delay}ms. " +
        "Error: {ErrorType} - {ErrorMessage}. CorrelationId: {CorrelationId}",
        args.AttemptNumber,
        maxRetries,
        operationName,
        args.RetryDelay.TotalMilliseconds,
        args.Outcome.Exception?.GetType().Name ?? "Unknown",
        args.Outcome.Exception?.Message ?? "No message",
        correlationId);
    
    return ValueTask.CompletedTask;
}
```

### 2. **Metrics Collection**

```csharp
public class RetryMetrics
{
    private static int _totalRetries = 0;
    private static int _successAfterRetry = 0;
    private static int _exhaustedRetries = 0;

    public static void RecordRetryAttempt() => Interlocked.Increment(ref _totalRetries);
    public static void RecordSuccess() => Interlocked.Increment(ref _successAfterRetry);
    public static void RecordExhaustion() => Interlocked.Increment(ref _exhaustedRetries);

    public static (int Total, int Success, int Exhausted) GetMetrics()
        => (_totalRetries, _successAfterRetry, _exhaustedRetries);
}

// In retry handler
OnRetry = args =>
{
    RetryMetrics.RecordRetryAttempt();
    return ValueTask.CompletedTask;
}
```

### 3. **Health Checks**

```csharp
builder.Services.AddHealthChecks()
    .AddCheck("event-bus-resilience", () =>
    {
        var metrics = RetryMetrics.GetMetrics();
        
        if (metrics.Exhausted > 10)
            return HealthCheckResult.Unhealthy(
                "Too many exhausted retries - event bus may be down");
        
        return HealthCheckResult.Healthy();
    });
```

### 4. **Seq Dashboard Queries**

In Seq (http://localhost:5341), create dashboard with queries:

```sql
-- Total retry attempts in last hour
select count(*) 
from stream 
where @Message like '%Retry%' 
  and @Timestamp > Now() - 1h

-- Failed operations after exhausting retries
select * 
from stream 
where @Message like '%failed after retries%'
order by @Timestamp desc

-- Most retried operations
select OperationName, count(*) as RetryCount
from stream
where @Message like '%Retry attempt%'
group by OperationName
order by RetryCount desc
```

---

## Common Pitfalls

### 1. **Retrying Non-Idempotent Operations**

❌ **Problem**:
```csharp
// Creates duplicate payments on retry
await _resiliencePipeline.ExecuteAsync(async ct =>
{
    var payment = new Payment { Amount = 100 };
    await _dbContext.Payments.InsertOneAsync(payment); // Creates duplicate!
});
```

✅ **Solution**:
```csharp
// Check for duplicates first
var existing = await _dbContext.Payments
    .Find(p => p.BookingId == bookingId)
    .FirstOrDefaultAsync();

if (existing != null)
    return existing; // Already exists

await _resiliencePipeline.ExecuteAsync(async ct =>
{
    var payment = new Payment { Amount = 100 };
    await _dbContext.Payments.InsertOneAsync(payment);
});
```

### 2. **Retrying Validation Errors**

❌ **Problem**:
```csharp
// Retries even when request is invalid
await _resiliencePipeline.ExecuteAsync(async ct =>
{
    // Throws ArgumentException - will never succeed!
    if (request.Amount <= 0)
        throw new ArgumentException("Invalid amount");
});
```

✅ **Solution**:
```csharp
// Only retry transient errors
var pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        ShouldHandle = new PredicateBuilder()
            .Handle<TimeoutException>()
            .Handle<HttpRequestException>()
            // Don't retry validation errors
    })
    .Build();
```

### 3. **Infinite Retry Loops**

❌ **Problem**:
```csharp
// No max attempts - could retry forever
_channel.BasicNack(ea.DeliveryTag, false, requeue: true);
```

✅ **Solution**:
```csharp
// Track retry count and give up eventually
if (_retryCount >= MAX_REQUEUE_ATTEMPTS)
{
    _channel.BasicNack(ea.DeliveryTag, false, requeue: false);
    // Move to dead letter queue
}
```

### 4. **No Jitter - Thundering Herd**

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

### 5. **Retrying Without Timeout**

❌ **Problem**:
```csharp
// Can hang forever waiting for response
await _resiliencePipeline.ExecuteAsync(async ct =>
{
    await _httpClient.GetAsync(url); // No timeout!
});
```

✅ **Solution**:
```csharp
var pipeline = new ResiliencePipelineBuilder()
    .AddRetry(/* ... */)
    .AddTimeout(TimeSpan.FromSeconds(30)) // ✅ Fail fast if hanging
    .Build();
```

### 6. **Not Logging Retry Attempts**

❌ **Problem**:
```csharp
// Silent retries - hard to debug
var pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 5 })
    .Build();
```

✅ **Solution**:
```csharp
var pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 5,
        OnRetry = args =>
        {
            _logger.LogWarning("Retry attempt {Attempt}", args.AttemptNumber);
            return ValueTask.CompletedTask;
        }
    })
    .Build();
```

---

## Recommended Implementation Roadmap

### Phase 1: Event Publishing (High Priority) ✅

**Target**: PaymentService, BookingService

1. Add `Polly` package
2. Create `ResiliencePipelineService`
3. Apply retry to event publishing methods
4. Test with RabbitMQ restart scenarios

**Estimated Effort**: 2-3 hours

### Phase 2: Event Consumption (Medium Priority)

**Target**: All consumers

1. Add max requeue limit tracking
2. Implement dead-letter queue logic
3. Add exponential backoff between requeues
4. Create monitoring dashboard

**Estimated Effort**: 3-4 hours

### Phase 3: Database Operations (Medium Priority)

**Target**: All services

1. Identify transient database errors
2. Create database-specific pipeline
3. Apply to critical operations
4. Add circuit breaker

**Estimated Effort**: 2-3 hours

### Phase 4: Connection Management (Low Priority) ✅

**Target**: RabbitMQ connection establishment

1. ✅ Add retry to initial connection
2. ✅ Implement reconnection logic
3. ✅ Test with broker restarts
4. ✅ Enable automatic recovery

**Estimated Effort**: 1-2 hours ✅ **COMPLETE**

**Implementation Details**:
- RabbitMQEventBus connection retry (both services)
- Consumer connection retry (both services)
- 10 retry attempts with exponential backoff
- 5-second base delay, capped at 60 seconds
- Handles BrokerUnreachableException, SocketException, TimeoutException
- Automatic recovery enabled on RabbitMQ client
- See [PHASE4_CONNECTION_RETRY.md](PHASE4_CONNECTION_RETRY.md) for details

### Phase 5: Observability (Ongoing)

**Target**: All services

1. Add correlation IDs
2. Create Seq dashboards
3. Set up alerts for retry exhaustion
4. Document runbooks

**Estimated Effort**: 2-3 hours

---

## Summary

### Current State
- ✅ Basic RabbitMQ consumer requeue (no limits)
- ❌ No retry for event publishing
- ❌ No retry for database operations
- ❌ Polly installed but not utilized

### Recommended State
- ✅ Exponential backoff with jitter for all network operations
- ✅ Circuit breakers for external dependencies
- ✅ Max retry limits with dead-letter queues
- ✅ Comprehensive logging and monitoring
- ✅ Idempotency checks

### Key Takeaways

1. **Retry logic is essential** in distributed systems
2. **Polly provides battle-tested resilience** patterns
3. **Exponential backoff with jitter** is the gold standard
4. **Always combine retry with idempotency** checks
5. **Monitor and alert on retry exhaustion** to detect systemic issues
6. **Don't retry everything** - permanent errors should fail fast

---

## References

- [Polly Official Documentation](https://github.com/App-vNext/Polly)
- [Polly v8 Migration Guide](https://www.pollydocs.org/migration-v8.html)
- [Microsoft: Implement resilient applications](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/)
- [AWS: Exponential Backoff and Jitter](https://aws.amazon.com/blogs/architecture/exponential-backoff-and-jitter/)
- [Martin Fowler: Circuit Breaker](https://martinfowler.com/bliki/CircuitBreaker.html)

---

**Last Updated**: November 4, 2025  
**Related Documentation**:
- [Event Bus Explained](EVENT_BUS_EXPLAINED.md)
- [PaymentService Implementation](PAYMENTSERVICE_IMPLEMENTATION.md)
- [Project README](../README.md)
