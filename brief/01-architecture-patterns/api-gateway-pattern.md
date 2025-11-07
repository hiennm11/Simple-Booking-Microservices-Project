# 🚪 API Gateway Pattern

**Category**: Architecture Patterns  
**Difficulty**: Intermediate  
**Focus**: Single entry point for microservices architecture

---

## 📖 Overview

An **API Gateway** is a server that acts as the **single entry point** for all client requests. It routes requests to appropriate microservices, handles cross-cutting concerns, and provides a unified API to clients.

---

## 🎯 Why API Gateway?

### Without API Gateway (Problems)

```text
❌ Direct Client-to-Service Communication:

┌──────────────┐
│   Browser    │
│   (Client)   │
└──────┬───────┘
       │
       ├──────→ http://userservice:5002/api/users        (CORS issue!)
       │
       ├──────→ http://bookingservice:5003/api/bookings  (Multiple hosts!)
       │
       └──────→ http://paymentservice:5004/api/payments  (No auth!)

Problems:
1. Client must know all service addresses
2. CORS: Different origins (ports 5002, 5003, 5004)
3. No centralized authentication
4. No rate limiting
5. Services exposed directly to internet (security risk)
6. N+1 network calls for aggregated data
```

### With API Gateway (Solution)

```text
✅ Client → API Gateway → Services:

┌──────────────┐
│   Browser    │
│   (Client)   │
└──────┬───────┘
       │
       │ http://localhost:5000/api/...  (Single endpoint!)
       ↓
┌────────────────────────────────────────────────┐
│            API Gateway (YARP)                  │
│  Port: 5000 (HTTP), 5001 (HTTPS)              │
│                                                │
│  ┌──────────────────────────────────────────┐ │
│  │ • Authentication (JWT validation)        │ │
│  │ • Rate Limiting (Token bucket)           │ │
│  │ • Request Routing (Path-based)           │ │
│  │ • Load Balancing (Round robin)           │ │
│  │ • Health Checks (Circuit breaker ready)  │ │
│  └──────────────────────────────────────────┘ │
└───────┬────────────┬────────────┬──────────────┘
        │            │            │
        ↓            ↓            ↓
   ┌─────────┐  ┌─────────┐  ┌─────────┐
   │  User   │  │ Booking │  │ Payment │
   │ Service │  │ Service │  │ Service │
   │  :5002  │  │  :5003  │  │  :5004  │
   └─────────┘  └─────────┘  └─────────┘

Benefits:
✅ Single entry point (localhost:5000)
✅ Centralized authentication
✅ Rate limiting protects services
✅ Routing logic hidden from clients
✅ Services not directly exposed
✅ Easy to add/remove services
```

---

## 🏗️ Implementation in Your Project

### Technology: YARP (Yet Another Reverse Proxy)

**Why YARP?**
- Built by Microsoft for .NET
- High performance (minimal overhead)
- Configuration-based routing
- Extensible with middleware
- Native ASP.NET Core integration

### Project Structure

```text
src/ApiGateway/
├── Program.cs                    ← Gateway setup
├── appsettings.json              ← Route configuration
├── Middleware/
│   ├── RateLimitingMiddleware.cs ← Token bucket rate limiter
│   └── JwtAuthenticationMiddleware.cs
├── Extensions/
│   └── ServiceCollectionExtensions.cs
└── Models/
    └── RateLimitOptions.cs
```

### Core Configuration

**File**: `src/ApiGateway/appsettings.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  
  "Jwt": {
    "SecretKey": "YourSuperSecretKeyForJwtTokenGeneration123!",
    "Issuer": "BookingSystemGateway",
    "Audience": "BookingSystemServices",
    "ExpirationMinutes": 60
  },
  
  "RateLimiting": {
    "EnableRateLimiting": true,
    "PermitLimit": 50,          // Burst capacity (tokens in bucket)
    "WindowInSeconds": 60,      // Refill window
    "QueueLimit": 0             // No queueing, reject immediately
  },
  
  "ReverseProxy": {
    "Routes": {
      "user-route": {
        "ClusterId": "user-cluster",
        "Match": {
          "Path": "/api/users/{**catch-all}"
        }
      },
      "booking-route": {
        "ClusterId": "booking-cluster",
        "Match": {
          "Path": "/api/bookings/{**catch-all}"
        },
        "Metadata": {
          "RequireAuthentication": "true"
        }
      },
      "payment-route": {
        "ClusterId": "payment-cluster",
        "Match": {
          "Path": "/api/payments/{**catch-all}"
        },
        "Metadata": {
          "RequireAuthentication": "true"
        }
      }
    },
    
    "Clusters": {
      "user-cluster": {
        "Destinations": {
          "user-destination": {
            "Address": "http://userservice:8080"
          }
        },
        "LoadBalancingPolicy": "RoundRobin",
        "HealthCheck": {
          "Active": {
            "Enabled": true,
            "Interval": "00:00:10",
            "Timeout": "00:00:05",
            "Policy": "ConsecutiveFailures",
            "Path": "/health"
          }
        }
      },
      
      "booking-cluster": {
        "Destinations": {
          "booking-destination": {
            "Address": "http://bookingservice:8080"
          }
        },
        "LoadBalancingPolicy": "RoundRobin",
        "HealthCheck": {
          "Active": {
            "Enabled": true,
            "Interval": "00:00:10",
            "Timeout": "00:00:05",
            "Policy": "ConsecutiveFailures",
            "Path": "/health"
          }
        }
      },
      
      "payment-cluster": {
        "Destinations": {
          "payment-destination": {
            "Address": "http://paymentservice:8080"
          }
        },
        "LoadBalancingPolicy": "RoundRobin"
      }
    }
  }
}
```

**Key Concepts**:

1. **Routes**: Define URL patterns to match
   - `Path`: "/api/users/{**catch-all}" matches all paths starting with /api/users
   - `ClusterId`: Which cluster handles this route

2. **Clusters**: Define backend services
   - `Destinations`: Actual service addresses
   - `LoadBalancingPolicy`: How to distribute load
   - `HealthCheck`: Monitor service health

3. **Metadata**: Custom data for middleware
   - `RequireAuthentication`: Our custom flag for JWT middleware

### Gateway Setup

**File**: `src/ApiGateway/Program.cs`

```csharp
using ApiGateway.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure CORS (allow frontend)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add YARP Reverse Proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Add JWT Authentication
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"])
            )
        };
    });

builder.Services.AddAuthorization();

// Add Rate Limiting configuration
builder.Services.Configure<RateLimitOptions>(
    builder.Configuration.GetSection("RateLimiting")
);

var app = builder.Build();

// Configure middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

// Custom middleware (order matters!)
app.UseMiddleware<RateLimitingMiddleware>();  // 1. Rate limit first
app.UseAuthentication();                      // 2. Then authenticate
app.UseAuthorization();                       // 3. Then authorize

// Map reverse proxy (this handles routing)
app.MapReverseProxy();

app.Run();
```

**Middleware Order**:
```text
Request Flow:
1. RateLimitingMiddleware → Reject if too many requests
2. Authentication → Validate JWT token
3. Authorization → Check permissions
4. YARP Routing → Forward to backend service
```

---

## 🔒 Authentication in Gateway

### JWT Token Validation

**File**: `src/ApiGateway/Middleware/JwtAuthenticationMiddleware.cs`

```csharp
public class JwtAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<JwtAuthenticationMiddleware> _logger;
    
    public JwtAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<JwtAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        // Get route metadata
        var endpoint = context.GetEndpoint();
        var requiresAuth = endpoint?.Metadata
            .GetMetadata<IRouteConfig>()?
            .Metadata
            .ContainsKey("RequireAuthentication") ?? false;
        
        if (requiresAuth)
        {
            // Check if authenticated
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Unauthorized",
                    message = "Valid JWT token required"
                });
                return;
            }
            
            // Log authenticated user
            var userId = context.User.FindFirst("sub")?.Value;
            _logger.LogInformation(
                "Authenticated request from user {UserId} to {Path}",
                userId, context.Request.Path
            );
        }
        
        await _next(context);
    }
}
```

### Token Flow

```text
1. Client Logs In:
   POST /api/users/login
   → UserService validates credentials
   → Returns JWT token

2. Client Stores Token:
   localStorage.setItem('token', jwt_token)

3. Client Makes Authenticated Request:
   GET /api/bookings
   Headers: Authorization: Bearer eyJhbGc...
   → API Gateway validates token
   → Extracts user ID from token
   → Forwards to BookingService (with user ID)

4. BookingService Processes:
   → Knows authenticated user ID
   → Returns only user's bookings
```

**Example Request**:

```bash
# 1. Login (get token)
curl -X POST http://localhost:5000/api/users/login \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com","password":"password123"}'

# Response:
# {
#   "token": "eyJhbGciOiJIUzI1NiIs...",
#   "userId": "user-123",
#   "email": "user@example.com"
# }

# 2. Use token for authenticated request
curl -X GET http://localhost:5000/api/bookings \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIs..."

# API Gateway validates token, forwards to BookingService
# BookingService returns bookings for user-123
```

---

## 🛡️ Rate Limiting

### Token Bucket Algorithm

**File**: `src/ApiGateway/Middleware/RateLimitingMiddleware.cs`

```csharp
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly ConcurrentDictionary<string, TokenBucket> _buckets;
    private readonly RateLimitOptions _options;
    
    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        IOptions<RateLimitOptions> options)
    {
        _next = next;
        _logger = logger;
        _options = options.Value;
        _buckets = new ConcurrentDictionary<string, TokenBucket>();
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.EnableRateLimiting)
        {
            await _next(context);
            return;
        }
        
        // Get client identifier (IP address)
        var clientId = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        
        // Get or create bucket for this client
        var bucket = _buckets.GetOrAdd(clientId, _ => new TokenBucket(
            capacity: _options.PermitLimit,
            refillRate: _options.PermitLimit / (double)_options.WindowInSeconds
        ));
        
        // Try to consume token
        if (bucket.TryConsume())
        {
            // Token available, allow request
            await _next(context);
        }
        else
        {
            // No token available, rate limited
            _logger.LogWarning(
                "Rate limit exceeded for client {ClientId} on {Path}",
                clientId, context.Request.Path
            );
            
            context.Response.StatusCode = 429; // Too Many Requests
            context.Response.Headers["Retry-After"] = "60";
            
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Too Many Requests",
                message = $"Rate limit exceeded. Maximum {_options.PermitLimit} requests per {_options.WindowInSeconds} seconds.",
                retryAfter = 60
            });
        }
    }
}

public class TokenBucket
{
    private readonly double _capacity;
    private readonly double _refillRate;
    private double _tokens;
    private DateTime _lastRefill;
    private readonly SemaphoreSlim _lock = new(1, 1);
    
    public TokenBucket(double capacity, double refillRate)
    {
        _capacity = capacity;
        _refillRate = refillRate;
        _tokens = capacity;
        _lastRefill = DateTime.UtcNow;
    }
    
    public bool TryConsume()
    {
        _lock.Wait();
        try
        {
            // Refill tokens based on time elapsed
            var now = DateTime.UtcNow;
            var elapsedSeconds = (now - _lastRefill).TotalSeconds;
            var tokensToAdd = elapsedSeconds * _refillRate;
            
            if (tokensToAdd > 0)
            {
                _tokens = Math.Min(_capacity, _tokens + tokensToAdd);
                _lastRefill = now;
            }
            
            // Try to consume token
            if (_tokens >= 1.0)
            {
                _tokens -= 1.0;
                return true;
            }
            
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }
}
```

**How It Works**:

```text
Bucket: Capacity = 50 tokens, Refill rate = 10 tokens/sec

t=0s:  [●●●●●●●●●● ... ●●●] 50 tokens (full)
       Request 1 → Consume 1 token
       [●●●●●●●●●● ... ●●] 49 tokens

t=1s:  Refill: +10 tokens = min(50, 49+10) = 50 tokens
       [●●●●●●●●●● ... ●●●] 50 tokens (full again)

t=0s:  Burst: 50 requests in 1 second
       [○○○○○○○○○○ ... ○○○] 0 tokens (empty)
       Request 51 → REJECTED (429 Too Many Requests)

t=5s:  Refill: +50 tokens (5 sec × 10/sec)
       [●●●●●●●●●● ... ●●●] 50 tokens
       Ready for more requests
```

**Benefits**:
- ✅ Allows bursts (good UX for legitimate users)
- ✅ Protects backend services from overload
- ✅ Per-client limiting (IP-based)
- ✅ Configurable limits

---

## 🔀 Request Routing

### Path-Based Routing

```text
Request: GET /api/users/123

1. Gateway receives request
2. Matches route pattern: /api/users/{**catch-all}
3. Finds cluster: user-cluster
4. Gets destination: http://userservice:8080
5. Forwards: GET http://userservice:8080/api/users/123
6. Returns response to client
```

**Configuration**:

```json
{
  "Routes": {
    "user-route": {
      "ClusterId": "user-cluster",
      "Match": {
        "Path": "/api/users/{**catch-all}"
      }
    }
  },
  "Clusters": {
    "user-cluster": {
      "Destinations": {
        "user-destination": {
          "Address": "http://userservice:8080"
        }
      }
    }
  }
}
```

### Load Balancing (Multi-Instance)

**If multiple service instances**:

```json
{
  "Clusters": {
    "booking-cluster": {
      "Destinations": {
        "booking-1": {
          "Address": "http://bookingservice1:8080"
        },
        "booking-2": {
          "Address": "http://bookingservice2:8080"
        },
        "booking-3": {
          "Address": "http://bookingservice3:8080"
        }
      },
      "LoadBalancingPolicy": "RoundRobin"
    }
  }
}
```

**Load Balancing Policies**:

| Policy | Description | Use Case |
|--------|-------------|----------|
| `RoundRobin` | Distribute evenly in order | Default, simple |
| `LeastRequests` | Route to least loaded instance | Uneven request times |
| `Random` | Random selection | Simple, stateless |
| `PowerOfTwoChoices` | Pick 2 random, choose least loaded | Good performance |

**Example Flow**:

```text
Round Robin with 3 instances:

Request 1 → booking-1
Request 2 → booking-2
Request 3 → booking-3
Request 4 → booking-1 (wrap around)
Request 5 → booking-2
...
```

---

## 🏥 Health Checks

### Active Health Checks

**Configuration**:

```json
{
  "Clusters": {
    "user-cluster": {
      "HealthCheck": {
        "Active": {
          "Enabled": true,
          "Interval": "00:00:10",      // Check every 10 seconds
          "Timeout": "00:00:05",       // 5 second timeout
          "Policy": "ConsecutiveFailures",
          "Path": "/health"            // Health endpoint
        }
      }
    }
  }
}
```

**How It Works**:

```text
Every 10 seconds:
1. Gateway → GET http://userservice:8080/health
2. If 200 OK: Mark as healthy
3. If timeout or error: Mark as unhealthy
4. If unhealthy: Don't route requests to this instance
5. Keep checking: Auto-recover when healthy again
```

**Service Health Endpoint**:

```csharp
// UserService/Program.cs
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        };
        await context.Response.WriteAsJsonAsync(response);
    }
});

// Add health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection"))
    .AddCheck("UserService", () => HealthCheckResult.Healthy("Service is running"));
```

**Response Example**:

```json
{
  "status": "Healthy",
  "checks": [
    {
      "name": "UserService",
      "status": "Healthy",
      "description": "Service is running",
      "duration": 0.5
    },
    {
      "name": "PostgreSQL",
      "status": "Healthy",
      "description": null,
      "duration": 12.3
    }
  ],
  "totalDuration": 12.8
}
```

---

## 📊 Observability

### Logging

```csharp
// Log all requests through gateway
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    
    var stopwatch = Stopwatch.StartNew();
    
    logger.LogInformation(
        "Incoming request: {Method} {Path} from {ClientIP}",
        context.Request.Method,
        context.Request.Path,
        context.Connection.RemoteIpAddress
    );
    
    await next();
    
    stopwatch.Stop();
    
    logger.LogInformation(
        "Request completed: {Method} {Path} → {StatusCode} in {Duration}ms",
        context.Request.Method,
        context.Request.Path,
        context.Response.StatusCode,
        stopwatch.ElapsedMilliseconds
    );
});
```

**Log Output** (with Serilog → Seq):

```text
[INF] Incoming request: GET /api/bookings from 172.18.0.1
[INF] Rate limiting: Client 172.18.0.1 has 49 tokens remaining
[INF] Authentication: User user-123 authenticated
[INF] Routing: Forwarding to http://bookingservice:8080/api/bookings
[INF] Request completed: GET /api/bookings → 200 in 45ms
```

### Metrics

Track gateway performance:

```csharp
public class GatewayMetrics
{
    private long _totalRequests;
    private long _totalErrors;
    private long _rateLimitedRequests;
    
    public void IncrementTotalRequests() => 
        Interlocked.Increment(ref _totalRequests);
    
    public void IncrementErrors() => 
        Interlocked.Increment(ref _totalErrors);
    
    public void IncrementRateLimited() => 
        Interlocked.Increment(ref _rateLimitedRequests);
    
    public (long total, long errors, long rateLimited) GetMetrics() =>
        (_totalRequests, _totalErrors, _rateLimitedRequests);
}

// Expose metrics endpoint
app.MapGet("/metrics", (GatewayMetrics metrics) =>
{
    var (total, errors, rateLimited) = metrics.GetMetrics();
    return new
    {
        totalRequests = total,
        totalErrors = errors,
        rateLimitedRequests = rateLimited,
        errorRate = total > 0 ? (double)errors / total : 0,
        rateLimitRate = total > 0 ? (double)rateLimited / total : 0
    };
});
```

---

## 🎓 Key Takeaways

### Benefits of API Gateway

1. **Single Entry Point**: Clients need only one URL
2. **Security**: Centralized authentication, services not exposed
3. **Rate Limiting**: Protect services from overload
4. **Routing**: Abstract backend topology from clients
5. **Cross-Cutting Concerns**: Logging, metrics, CORS in one place
6. **Load Balancing**: Distribute load across instances
7. **Health Checks**: Auto-remove unhealthy instances

### Your Project Implementation

| Feature | Technology | Status |
|---------|-----------|---------|
| **Reverse Proxy** | YARP (Microsoft) | ✅ Implemented |
| **Authentication** | JWT Bearer tokens | ✅ Implemented |
| **Rate Limiting** | Token bucket algorithm | ✅ Implemented |
| **Routing** | Path-based, configuration | ✅ Implemented |
| **Load Balancing** | Round robin | ✅ Configured |
| **Health Checks** | Active polling | ✅ Implemented |
| **Logging** | Serilog → Seq | ✅ Implemented |
| **CORS** | Allow all origins (dev) | ✅ Implemented |

### Configuration Summary

```text
Port: 5000 (HTTP), 5001 (HTTPS)
Routes:
  /api/users/**     → UserService:5002
  /api/bookings/**  → BookingService:5003 (requires auth)
  /api/payments/**  → PaymentService:5004 (requires auth)

Rate Limiting:
  50 requests per minute (burst)
  10 requests/sec sustained

Health Checks:
  Interval: 10 seconds
  Timeout: 5 seconds
  Endpoint: /health
```

### Best Practices

1. **Keep Gateway Thin**: Don't add business logic
2. **Use Configuration**: Routes, limits in config files
3. **Monitor Health**: Active health checks
4. **Log Everything**: All requests through gateway
5. **Rate Limit**: Protect backend services
6. **Fail Fast**: Timeout requests quickly
7. **Cache Aggressively**: Cache responses when possible (future)

---

## 📚 Further Study

### Related Documents

- [Microservices Fundamentals](./microservices-fundamentals.md)
- [Database Per Service](./database-per-service.md)
- [JWT Authentication Implementation](/docs/phase4-gateway-security/JWT_AUTHENTICATION_IMPLEMENTATION.md)
- [Rate Limiting Implementation](/docs/phase4-gateway-security/RATE_LIMITING_IMPLEMENTATION.md)

### Project Documentation

- `/docs/phase4-gateway-security/APIGATEWAY_IMPLEMENTATION.md`
- `/docs/phase4-gateway-security/AUTHORIZATION_GUIDE.md`
- `src/ApiGateway/appsettings.json`

### External Resources

- [YARP Documentation](https://microsoft.github.io/reverse-proxy/)
- [API Gateway Pattern - Microsoft](https://learn.microsoft.com/en-us/azure/architecture/microservices/design/gateway)
- [Pattern: API Gateway - Chris Richardson](https://microservices.io/patterns/apigateway.html)

---

**Last Updated**: November 7, 2025  
**Status**: ✅ Fully implemented in your project  
**Next**: [Service Discovery](./service-discovery.md)
