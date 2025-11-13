# Connection Resilience

## 📚 What is Connection Resilience?

**Connection resilience** ensures that services can establish and maintain connections to infrastructure components (databases, message brokers, caches) even when those components are temporarily unavailable or experiencing issues.

### The Problem

In distributed systems, services often depend on external infrastructure:

```
Service Startup → Database not ready → Connection fails → Service crashes ❌
Service Running → RabbitMQ restarts → Connection lost → Service crashes ❌
```

**Impact**:
- Services fail during orchestrated deployments (Docker, Kubernetes)
- Manual intervention required to restart services
- Poor production resilience
- Cascading failures during infrastructure maintenance

---

## ✅ Solution: Retry-Based Connection Management

```
Service Startup → Database not ready → Retry with backoff
              → Attempt 1 (5s)  ❌
              → Attempt 2 (10s) ❌
              → Attempt 3 (20s) ✅ Connected!
              → Service starts successfully
```

**Benefits**:
- ✅ Services wait for infrastructure to become available
- ✅ Automatic recovery without manual intervention
- ✅ Survives orchestration timing issues
- ✅ Production-ready resilience

---

## 🔧 Implementation in This Project

### 1. RabbitMQ Connection Resilience

**File**: `PaymentService/EventBus/RabbitMQEventBus.cs`, `BookingService/EventBus/RabbitMQEventBus.cs`

#### Connection Establishment with Retry

```csharp
using Polly;
using Polly.Retry;

public class RabbitMQEventBus : IEventBus
{
    private IConnection? _connection;
    private readonly ResiliencePipeline _connectionPipeline;

    public RabbitMQEventBus(ILogger<RabbitMQEventBus> logger)
    {
        _connectionPipeline = CreateConnectionResiliencePipeline();
    }

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
                    .Handle<TimeoutException>(),
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "RabbitMQ connection retry {Attempt}/{MaxAttempts} after {Delay}ms. " +
                        "Waiting for RabbitMQ to become available...",
                        args.AttemptNumber, 
                        10, 
                        args.RetryDelay.TotalMilliseconds);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    private void EnsureConnection()
    {
        if (_connection != null && _connection.IsOpen)
            return;

        _logger.LogInformation("Establishing connection to RabbitMQ...");

        _connectionPipeline.Execute(() =>
        {
            var factory = new ConnectionFactory
            {
                HostName = _settings.HostName,
                Port = _settings.Port,
                UserName = _settings.UserName,
                Password = _settings.Password,
                VirtualHost = _settings.VirtualHost,
                
                // ✅ Enable automatic recovery
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
            };

            _connection = factory.CreateConnection();
            _logger.LogInformation(
                "RabbitMQ connection established to {Host}:{Port}",
                _settings.HostName, 
                _settings.Port);
        });
    }
}
```

#### Key Features

**1. Exponential Backoff with Jitter**:
```
Attempt 1: 0s   + ~5s   = 5s    ❌
Attempt 2: 5s   + ~10s  = 15s   ❌
Attempt 3: 15s  + ~20s  = 35s   ❌
Attempt 4: 35s  + ~40s  = 75s   ❌
Attempt 5: 75s  + ~60s  = 135s  ✅ (capped at 60s)

Total wait time: ~8 minutes maximum
```

**2. Handled Exceptions**:
- `BrokerUnreachableException` - RabbitMQ not running
- `SocketException` - Network connectivity issues
- `TimeoutException` - Connection timeout

**3. Automatic Recovery**:
```csharp
AutomaticRecoveryEnabled = true
NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
```

RabbitMQ client automatically attempts to reconnect if connection is lost during operation.

---

### 2. Consumer Connection Resilience

**File**: `PaymentService/Consumers/BookingCreatedConsumer.cs`

```csharp
public class BookingCreatedConsumer : BackgroundService
{
    private IConnection? _connection;
    private IChannel? _channel;
    private readonly ResiliencePipeline _connectionPipeline;

    public BookingCreatedConsumer(
        IServiceProvider serviceProvider,
        ILogger<BookingCreatedConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        // Create connection resilience pipeline
        _connectionPipeline = new ResiliencePipelineBuilder()
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
                    .Handle<TimeoutException>(),
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "RabbitMQ connection retry {Attempt}/{MaxAttempts}...",
                        args.AttemptNumber, 10);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for RabbitMQ to be available
        InitializeRabbitMQ();
        
        // Start consuming messages
        await StartConsumingAsync(stoppingToken);
    }

    private void InitializeRabbitMQ()
    {
        _connectionPipeline.Execute(() =>
        {
            var factory = new ConnectionFactory
            {
                HostName = _settings.HostName,
                Port = _settings.Port,
                UserName = _settings.UserName,
                Password = _settings.Password,
                VirtualHost = _settings.VirtualHost,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateChannel();
            
            _logger.LogInformation(
                "BookingCreatedConsumer established connection to RabbitMQ");
        });
    }
}
```

---

### 3. Database Connection Resilience (Entity Framework Core)

**File**: `BookingService/Program.cs`

```csharp
builder.Services.AddDbContext<BookingDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions =>
        {
            // ✅ Enable connection resilience
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorCodesToAdd: null);
            
            // Command timeout
            npgsqlOptions.CommandTimeout(30);
        }));
```

**Retry Configuration**:
- **Max Retry Count**: 5 attempts
- **Max Retry Delay**: 30 seconds
- **Handled Errors**: Transient PostgreSQL errors

**Automatic Retry for**:
- Connection failures
- Deadlocks
- Timeout exceptions
- Transient network errors

---

### 4. MongoDB Connection Resilience

**File**: `PaymentService/Program.cs`

```csharp
builder.Services.Configure<MongoDBSettings>(
    builder.Configuration.GetSection("MongoDB"));

builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<MongoDBSettings>>().Value;
    
    var mongoSettings = MongoClientSettings.FromConnectionString(
        settings.ConnectionString);
    
    // ✅ Configure connection resilience
    mongoSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(10);
    mongoSettings.ConnectTimeout = TimeSpan.FromSeconds(10);
    mongoSettings.SocketTimeout = TimeSpan.FromSeconds(30);
    
    // ✅ Retry reads on network errors
    mongoSettings.RetryReads = true;
    
    // ✅ Retry writes on network errors (MongoDB 3.6+)
    mongoSettings.RetryWrites = true;
    
    return new MongoClient(mongoSettings);
});
```

**Retry Configuration**:
- **Server Selection Timeout**: 10 seconds
- **Connect Timeout**: 10 seconds
- **Socket Timeout**: 30 seconds
- **Retry Reads**: Enabled
- **Retry Writes**: Enabled

---

## 📊 Retry Configuration Comparison

| Component | Max Attempts | Base Delay | Max Delay | Total Wait Time |
|-----------|--------------|------------|-----------|-----------------|
| **RabbitMQ Connection** | 10 | 5s | 60s | ~8 minutes |
| **RabbitMQ Consumer** | 10 | 5s | 60s | ~8 minutes |
| **PostgreSQL (EF Core)** | 5 | - | 30s | ~2 minutes |
| **MongoDB** | Built-in | - | - | Driver-managed |

---

## 🧪 Testing Connection Resilience

### Test 1: Start Services Before Infrastructure

**Scenario**: Services start before RabbitMQ is ready

```bash
# Start services first
docker-compose up paymentservice bookingservice

# Start RabbitMQ 30 seconds later
docker-compose up rabbitmq
```

**Expected Behavior**:
- Services show retry warnings in logs
- Services successfully connect once RabbitMQ available
- No service crashes

**Log Output**:
```
[Warning] RabbitMQ connection retry 1/10 after 5000ms. Waiting for RabbitMQ...
[Warning] RabbitMQ connection retry 2/10 after 10000ms. Waiting for RabbitMQ...
[Information] RabbitMQ connection established to rabbitmq:5672
[Information] BookingCreatedConsumer established connection to RabbitMQ
```

---

### Test 2: Infrastructure Restart During Operation

**Scenario**: RabbitMQ restarts while services are running

```bash
# System running normally
docker restart rabbitmq
```

**Expected Behavior**:
- RabbitMQ automatic recovery handles reconnection
- No manual intervention needed
- Consumers automatically reconnect

**Log Output**:
```
[Warning] Connection lost to RabbitMQ
[Information] Automatic recovery initiated
[Information] Connection re-established
```

---

### Test 3: Database Connection Failure

**Scenario**: Database unavailable during query

```bash
# Stop PostgreSQL
docker stop bookingdb

# Try to query bookings
curl http://localhost:5002/api/bookings/123

# Start PostgreSQL
docker start bookingdb
```

**Expected Behavior**:
- EF Core automatically retries (up to 5 times)
- Operation succeeds once database available
- No exception thrown if database recovers quickly

---

## 🎯 Best Practices

### 1. Use Generous Timeouts for Startup

```csharp
// ✅ Production: Patient startup
MaxRetryAttempts = 10
Delay = 5 seconds
MaxDelay = 60 seconds

// ❌ Development: Fast failure for debugging
MaxRetryAttempts = 3
Delay = 2 seconds
MaxDelay = 10 seconds
```

**Why?** In production, infrastructure startup can take several minutes (especially in cloud environments).

---

### 2. Enable Automatic Recovery

```csharp
// ✅ RabbitMQ
AutomaticRecoveryEnabled = true
NetworkRecoveryInterval = TimeSpan.FromSeconds(10)

// ✅ Entity Framework Core
options.EnableRetryOnFailure(maxRetryCount: 5)

// ✅ MongoDB
mongoSettings.RetryReads = true
mongoSettings.RetryWrites = true
```

---

### 3. Log Connection Attempts

```csharp
OnRetry = args =>
{
    _logger.LogWarning(
        "Connection retry {Attempt}/{MaxAttempts} after {Delay}ms. " +
        "Error: {ErrorType} - {ErrorMessage}",
        args.AttemptNumber,
        maxAttempts,
        args.RetryDelay.TotalMilliseconds,
        args.Outcome.Exception?.GetType().Name,
        args.Outcome.Exception?.Message);
    
    return ValueTask.CompletedTask;
}
```

**Benefits**:
- Diagnose infrastructure timing issues
- Identify patterns in connection failures
- Monitor connection retry rates

---

### 4. Use Health Checks

```csharp
builder.Services.AddHealthChecks()
    .AddRabbitMQ(
        rabbitConnectionString: "amqp://guest:guest@localhost:5672",
        name: "rabbitmq",
        failureStatus: HealthStatus.Degraded)
    .AddNpgSql(
        connectionString: connectionString,
        name: "bookingdb",
        failureStatus: HealthStatus.Unhealthy)
    .AddMongoDb(
        mongodbConnectionString: mongoConnectionString,
        name: "paymentdb");
```

**Endpoint**: `GET /health`

**Response**:
```json
{
  "status": "Healthy",
  "results": {
    "rabbitmq": { "status": "Healthy" },
    "bookingdb": { "status": "Healthy" },
    "paymentdb": { "status": "Healthy" }
  }
}
```

---

### 5. Configure for Environment

```csharp
// appsettings.Development.json
{
  "ConnectionResilience": {
    "MaxRetryAttempts": 3,
    "BaseDelaySeconds": 2
  }
}

// appsettings.Production.json
{
  "ConnectionResilience": {
    "MaxRetryAttempts": 10,
    "BaseDelaySeconds": 5
  }
}
```

---

## 📊 Monitoring Connection Health

### Seq Queries

**Connection Retry Rate**:
```sql
select count(*) as RetryCount
from stream
where @Message like '%connection retry%'
  and @Timestamp > Now() - 1h
```

**Failed Connections**:
```sql
select Service, ErrorMessage, count(*) as FailureCount
from stream
where @Message like '%connection failed%'
  and @Timestamp > Now() - 24h
group by Service, ErrorMessage
order by FailureCount desc
```

**Connection Establishment Time**:
```sql
select Service, avg(Duration) as AvgConnectionTime
from stream
where @Message like '%connection established%'
group by Service
```

---

## 🚀 Deployment Recommendations

### Docker Compose

**No dependency management needed** - services handle startup order automatically:

```yaml
services:
  rabbitmq:
    image: rabbitmq:3-management
    # No depends_on needed
  
  paymentservice:
    depends_on:
      - paymentdb
      # Don't need to wait for rabbitmq - service handles it
  
  bookingservice:
    depends_on:
      - bookingdb
      # Connection resilience handles rabbitmq availability
```

---

### Kubernetes

**Liveness Probe**:
```yaml
livenessProbe:
  httpGet:
    path: /health
    port: 80
  initialDelaySeconds: 120  # Allow time for connection establishment
  periodSeconds: 30
  timeoutSeconds: 5
  failureThreshold: 3
```

**Readiness Probe**:
```yaml
readinessProbe:
  httpGet:
    path: /health/ready
    port: 80
  initialDelaySeconds: 30
  periodSeconds: 10
  timeoutSeconds: 3
  failureThreshold: 3
```

---

## ⚠️ Common Pitfalls

### 1. Insufficient Retry Attempts

❌ **Problem**:
```csharp
MaxRetryAttempts = 3  // Only 15 seconds max wait
```

In production, infrastructure can take 30-60 seconds to start.

✅ **Solution**:
```csharp
MaxRetryAttempts = 10  // ~8 minutes max wait
```

---

### 2. No Maximum Delay Cap

❌ **Problem**:
```csharp
BackoffType = DelayBackoffType.Exponential
// No MaxDelay - delays can become extremely long
```

Delays can grow to minutes: 64s, 128s, 256s...

✅ **Solution**:
```csharp
BackoffType = DelayBackoffType.Exponential,
MaxDelay = TimeSpan.FromSeconds(60)  // Cap at 60 seconds
```

---

### 3. Missing Jitter

❌ **Problem**:
```csharp
// All services retry at exactly the same time
UseJitter = false
```

Creates synchronized retry storms.

✅ **Solution**:
```csharp
UseJitter = true  // Randomize delays ±25%
```

---

### 4. Not Logging Connection Attempts

❌ **Problem**:
```csharp
// Silent retries - hard to diagnose
var pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 10 })
    .Build();
```

✅ **Solution**:
```csharp
OnRetry = args =>
{
    _logger.LogWarning("Connection retry {Attempt}...", args.AttemptNumber);
    return ValueTask.CompletedTask;
}
```

---

## 🎓 Key Takeaways

1. **Connection resilience is critical** for production deployments
2. **Exponential backoff with jitter** prevents synchronized retries
3. **Generous timeouts** accommodate infrastructure startup delays
4. **Automatic recovery** handles transient connection losses
5. **Health checks** provide visibility into connection status
6. **Logging** enables troubleshooting of connection issues
7. **Environment-specific configuration** balances debugging speed vs. resilience

---

## 📚 Further Reading

- [RabbitMQ .NET Client Guide](https://www.rabbitmq.com/dotnet-api-guide.html)
- [EF Core Connection Resiliency](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency)
- [MongoDB Connection Pooling](https://www.mongodb.com/docs/drivers/csharp/current/fundamentals/connection/connection-options/)
- [Polly Resilience Pipelines](https://www.pollydocs.org/)

---

## 🔗 Related Documentation

- [Retry Patterns with Polly](./retry-patterns-polly.md)
- [Circuit Breaker](./circuit-breaker.md)
- [Dead Letter Queue](./dead-letter-queue.md)
- [Phase 4 Connection Retry Implementation](/docs/phase3-event-integration/PHASE4_CONNECTION_RETRY.md)

---

**Implementation Status**: ✅ Production Ready  
**Last Updated**: November 12, 2025
