# API Gateway Configuration Guide

This document explains the configuration options for the API Gateway.

## Table of Contents
- [Configuration Files](#configuration-files)
- [Reverse Proxy Configuration](#reverse-proxy-configuration)
- [Health Check Configuration](#health-check-configuration)
- [Logging Configuration](#logging-configuration)
- [Environment-Specific Settings](#environment-specific-settings)

## Configuration Files

### appsettings.json
Production/Docker configuration with service discovery names.

### appsettings.Development.json
Local development configuration with localhost URLs.

## Reverse Proxy Configuration

### Routes

Routes define how incoming requests are matched and forwarded to backend services.

```json
"Routes": {
  "users-route": {
    "ClusterId": "users-cluster",
    "Match": {
      "Path": "/api/users/{**catch-all}"
    },
    "Transforms": [
      {
        "PathPattern": "/api/users/{**catch-all}"
      }
    ]
  }
}
```

**Key Properties:**
- `ClusterId`: Links to the destination cluster
- `Match.Path`: URL pattern to match (supports wildcards)
- `Transforms`: Modify request before forwarding

### Clusters

Clusters define the backend service destinations and health check policies.

```json
"Clusters": {
  "users-cluster": {
    "Destinations": {
      "destination1": {
        "Address": "http://userservice"
      }
    },
    "HealthCheck": {
      "Active": {
        "Enabled": true,
        "Interval": "00:00:10",
        "Timeout": "00:00:05",
        "Policy": "ConsecutiveFailures",
        "Path": "/health"
      }
    }
  }
}
```

**Key Properties:**
- `Destinations`: One or more backend service URLs
- `HealthCheck.Interval`: How often to check health
- `HealthCheck.Timeout`: Maximum time to wait for response
- `HealthCheck.Path`: Health check endpoint path

## Health Check Configuration

The gateway monitors downstream services:

### URL-Based Health Checks

```csharp
builder.Services.AddHealthChecks()
    .AddUrlGroup(new Uri("http://userservice/health"), 
                 name: "userservice", 
                 tags: new[] { "services" });
```

### Active Health Checks (YARP)

YARP automatically performs health checks based on cluster configuration:

- **Enabled**: Health checks are active
- **Interval**: Check every 10 seconds
- **Timeout**: 5 seconds per check
- **Policy**: ConsecutiveFailures (marks unhealthy after consecutive failures)

## Logging Configuration

### Serilog Configuration

```json
"Serilog": {
  "MinimumLevel": {
    "Default": "Information",
    "Override": {
      "Microsoft": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "System": "Warning",
      "Yarp": "Information"
    }
  }
}
```

### Log Sinks

1. **Console**: Structured output to console
2. **Seq**: Centralized logging server

### Custom Request/Response Logging

The `RequestResponseLoggingMiddleware` logs:
- Request method and path
- Unique request ID (added to response headers)
- Response status code
- Execution time in milliseconds

## Environment-Specific Settings

### Docker/Production

**Service Discovery**: Uses Docker service names
```json
"Address": "http://userservice"
```

**Network**: Services communicate on `booking-network`

### Local Development

**Direct Connection**: Uses localhost ports
```json
"Address": "http://localhost:5001"
```

**Service Ports**:
- UserService: 5001
- BookingService: 5002
- PaymentService: 5003

## Advanced Configuration Options

### Load Balancing

YARP supports multiple load balancing policies:

```json
"LoadBalancingPolicy": "RoundRobin"
```

Options: `RoundRobin`, `LeastRequests`, `Random`, `PowerOfTwoChoices`

### Session Affinity

Enable sticky sessions:

```json
"SessionAffinity": {
  "Enabled": true,
  "Policy": "Cookie",
  "AffinityKeyName": ".Yarp.Affinity"
}
```

### Request Timeouts

Configure per-route timeouts:

```json
"Timeout": "00:01:00"
```

### Request Size Limits

Configure in `Program.cs`:

```csharp
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
});
```

### CORS Configuration

For production, specify allowed origins:

```csharp
options.AddDefaultPolicy(policy =>
{
    policy.WithOrigins("https://yourdomain.com")
          .AllowAnyMethod()
          .AllowAnyHeader();
});
```

## Security Considerations

### JWT Authentication (Future)

Add JWT authentication middleware:

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => { /* config */ });
```

### Rate Limiting (Future)

Implement rate limiting:

```csharp
builder.Services.AddRateLimiter(options => 
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
        context => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? context.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));
});
```

## Troubleshooting

### Configuration Not Loading

1. Check environment variable substitution
2. Verify JSON syntax
3. Review application startup logs

### Routes Not Working

1. Verify route path patterns
2. Check cluster IDs match
3. Test backend service directly

### Health Checks Failing

1. Verify backend `/health` endpoints
2. Check network connectivity
3. Review timeout settings

## Configuration Examples

### Adding a New Service Route

1. Add route definition:
```json
"new-service-route": {
  "ClusterId": "new-service-cluster",
  "Match": {
    "Path": "/api/newservice/{**catch-all}"
  }
}
```

2. Add cluster definition:
```json
"new-service-cluster": {
  "Destinations": {
    "destination1": {
      "Address": "http://newservice"
    }
  }
}
```

3. Add health check:
```csharp
.AddUrlGroup(new Uri("http://newservice/health"), 
             name: "newservice", 
             tags: new[] { "services" })
```

## Environment Variables

The following environment variables are used:

| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Environment name | Development |
| `SEQ_URL` | Seq server URL | http://seq:5341 |
| `SEQ_API_KEY` | Seq API key | (optional) |

## References

- [YARP Configuration](https://microsoft.github.io/reverse-proxy/articles/config-files.html)
- [YARP Transforms](https://microsoft.github.io/reverse-proxy/articles/transforms.html)
- [ASP.NET Core Health Checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)
