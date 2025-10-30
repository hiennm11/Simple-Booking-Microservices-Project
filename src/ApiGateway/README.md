# API Gateway

The API Gateway serves as the single entry point for all client requests to the microservices backend. It's built using **YARP (Yet Another Reverse Proxy)**, Microsoft's modern, high-performance reverse proxy solution.

## Features

- **Unified Entry Point**: Single endpoint for accessing all microservices
- **Route Management**: Intelligent routing to UserService, BookingService, and PaymentService
- **Health Monitoring**: Active health checks for all downstream services
- **Request Logging**: Comprehensive logging with Serilog and Seq integration
- **CORS Support**: Configured for cross-origin resource sharing
- **Load Balancing**: Built-in support for distributing traffic across service instances
- **Performance**: High-throughput, low-latency proxy using YARP

## Architecture

```
Client Request
     ↓
API Gateway (YARP)
     ↓
├── /api/users/*     → UserService (Port 5001)
├── /api/bookings/*  → BookingService (Port 5002)
└── /api/payments/*  → PaymentService (Port 5003)
```

## Configuration

### Routes

The gateway defines three main routes in `appsettings.json`:

1. **User Routes**: `/api/users/{**catch-all}` → UserService
2. **Booking Routes**: `/api/bookings/{**catch-all}` → BookingService
3. **Payment Routes**: `/api/payments/{**catch-all}` → PaymentService

### Clusters

Each service has a dedicated cluster with:
- Destination address (service URL)
- Active health checks (10-second intervals)
- Failure detection policy
- Timeout configuration (5 seconds)

### Environment-Specific Settings

- **Production** (`appsettings.json`): Uses Docker service names (e.g., `http://userservice`)
- **Development** (`appsettings.Development.json`): Uses localhost ports (e.g., `http://localhost:5001`)

## Endpoints

### Gateway Management

- `GET /` - Gateway information and available routes
- `GET /health` - Gateway health status with downstream service checks

### Proxied Services

All service endpoints are accessed through the gateway:

#### User Service
- `POST /api/users/register` - Register new user
- `POST /api/users/login` - User authentication
- `GET /api/users/{id}` - Get user by ID

#### Booking Service (Future)
- `GET /api/bookings` - List bookings
- `POST /api/bookings` - Create booking
- `GET /api/bookings/{id}` - Get booking details

#### Payment Service (Future)
- `GET /api/payments` - List payments
- `POST /api/payments` - Process payment
- `GET /api/payments/{id}` - Get payment details

## Running the Gateway

### Using Docker Compose

```bash
docker-compose up apigateway
```

The gateway will be available at: `http://localhost:5000`

### Local Development

```bash
cd src/ApiGateway
dotnet run
```

### Configuration Requirements

Ensure the following environment variables are set:

```env
SEQ_URL=http://seq:5341
SEQ_API_KEY=your-seq-api-key
```

## Health Checks

The gateway performs active health checks on all downstream services:

- **Interval**: Every 10 seconds
- **Timeout**: 5 seconds
- **Policy**: ConsecutiveFailures
- **Endpoint**: `/health` on each service

If a service becomes unhealthy, YARP automatically stops routing traffic to it until it recovers.

## CORS Policy

The gateway is configured with a permissive CORS policy for development:
- Allows any origin
- Allows any HTTP method
- Allows any header

**Note**: In production, configure specific allowed origins for security.

## Logging

All requests are logged with:
- HTTP method
- Request path
- Status code
- Response time (milliseconds)

Logs are sent to:
1. Console (structured output)
2. Seq (centralized logging at http://localhost:5341)

## Performance Features

YARP provides:
- **Zero-allocation routing**: Efficient memory usage
- **HTTP/2 and HTTP/3 support**: Modern protocol support
- **Request/Response transformation**: Header manipulation, path rewriting
- **Session affinity**: Sticky sessions when needed
- **Rate limiting**: Traffic control (can be configured)

## Testing

Use the included `ApiGateway.http` file to test endpoints:

```http
GET http://localhost:5000/health
```

## Troubleshooting

### Service Not Responding

1. Check service health: `GET /health`
2. Verify downstream services are running
3. Check Seq logs for routing errors

### Configuration Issues

1. Verify service addresses in `appsettings.json`
2. Ensure Docker network connectivity
3. Check environment variable substitution

### Performance Issues

1. Review health check intervals
2. Monitor response times in Seq
3. Check for service bottlenecks

## Future Enhancements

- [ ] JWT Authentication middleware
- [ ] Rate limiting per client
- [ ] API versioning support
- [ ] Request/Response caching
- [ ] Circuit breaker pattern
- [ ] Distributed tracing with OpenTelemetry
- [ ] API documentation aggregation (Swagger UI)

## Dependencies

- **Yarp.ReverseProxy** (2.3.0): Core reverse proxy functionality
- **AspNetCore.HealthChecks.Uris** (9.0.0): URL-based health checks
- **Serilog.AspNetCore** (9.0.0): Structured logging
- **Serilog.Sinks.Seq** (9.0.0): Seq integration

## References

- [YARP Documentation](https://microsoft.github.io/reverse-proxy/)
- [ASP.NET Core Health Checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)
- [API Gateway Pattern](https://microservices.io/patterns/apigateway.html)
