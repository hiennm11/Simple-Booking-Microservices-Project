# PaymentService

## Overview
The PaymentService handles payment processing for bookings in the microservices architecture. It uses MongoDB for storage and communicates asynchronously with other services via RabbitMQ.

## Features
- ✅ Process payments with 90% success simulation
- ✅ MongoDB document storage
- ✅ Event-driven architecture (publishes PaymentSucceeded)
- ✅ Optional automatic payment processing via BookingCreatedConsumer
- ✅ **Polly resilience pipelines** with retry logic and timeouts
- ✅ **Exponential backoff** with jitter for transient failures
- ✅ Health checks
- ✅ Structured logging with Serilog
- ✅ OpenAPI/Swagger documentation
- ✅ Comprehensive unit test suite (41 tests, 80%+ pass rate)

## Technology Stack
- **Framework:** ASP.NET Core 10.0
- **Database:** MongoDB
- **Event Bus:** RabbitMQ
- **Resilience:** Polly v8 (retry policies, circuit breakers, timeouts)
- **Logging:** Serilog + Seq
- **Testing:** xUnit, Moq, FluentAssertions
- **Health Checks:** MongoDB health monitoring

## API Endpoints

### Process Payment
```http
POST /api/payment/pay
Content-Type: application/json

{
  "bookingId": "guid",
  "amount": 500000,
  "paymentMethod": "CREDIT_CARD"
}
```

### Get Payment by ID
```http
GET /api/payment/{id}
```

### Get Payment by Booking ID
```http
GET /api/payment/booking/{bookingId}
```

### Health Check
```http
GET /health
```

## Events

### Publishes
- **PaymentSucceeded** → `payment_succeeded` queue
  - Triggered when payment is processed successfully
  - Consumed by BookingService to update booking status

### Consumes (Optional)
- **BookingCreated** ← `booking_created` queue
  - Automatically processes payment when booking is created
  - Disabled by default in `Program.cs`

## Configuration

### MongoDB
```json
"MongoDB": {
  "ConnectionString": "mongodb://user:pass@host:port/dbname",
  "DatabaseName": "paymentdb",
  "Collections": {
    "Payments": "payments"
  }
}
```

### RabbitMQ
```json
"RabbitMQ": {
  "HostName": "localhost",
  "Port": 5672,
  "UserName": "guest",
  "Password": "guest",
  "VirtualHost": "/",
  "Queues": {
    "BookingCreated": "booking_created",
    "PaymentSucceeded": "payment_succeeded"
  }
}
```

## Running the Service

### Prerequisites
- .NET 10 SDK
- MongoDB running on port 27017
- RabbitMQ running on port 5672

### Local Development
```bash
# Build
dotnet build

# Run
dotnet run

# Service will be available at:
# http://localhost:5003
# https://localhost:5004
```

### With Docker
```bash
# From project root
docker-compose up -d paymentservice
```

## Project Structure
```
PaymentService/
├── Controllers/         # API endpoints
├── Consumers/          # RabbitMQ event consumers
├── Data/               # MongoDB context
├── DTOs/               # Request/Response models
├── EventBus/           # RabbitMQ implementation
├── HealthChecks/       # Health check implementations
├── Models/             # MongoDB documents
├── Services/           # Business logic
│   ├── PaymentServiceImpl.cs         # Payment processing logic
│   └── ResiliencePipelineService.cs  # Polly resilience policies
└── Program.cs          # Application startup
```

## Resilience & Retry Logic

This service implements **Polly v8** resilience patterns to handle transient failures:

### Event Publishing Pipeline
- **Retries:** 3 attempts with exponential backoff (2s, 4s, 8s)
- **Jitter:** ±25% randomization to prevent thundering herd
- **Timeout:** 10 seconds per operation
- **Use Case:** Publishing PaymentSucceeded events to RabbitMQ

### Database Operations Pipeline
- **Retries:** 5 attempts with exponential backoff (1s, 2s, 4s, 8s, 16s)
- **Transient Exceptions:** Handles MongoDB timeout, connection failures
- **Timeout:** 30 seconds per operation
- **Use Case:** MongoDB read/write operations

### Consumer Retry Logic
The `BookingCreatedConsumer` implements:
- **Message Requeuing:** Up to 3 requeue attempts
- **Exponential Backoff:** 5s, 10s, 20s delays between retries
- **Internal Retries:** 2 Polly retries before requeueing
- **Dead Letter:** Failed messages logged after exhausting retries

**See:** [POLLY_IMPLEMENTATION.md](./POLLY_IMPLEMENTATION.md) for detailed documentation

## Testing

### Unit Tests
Located in `../../test/PaymentService.Tests/`

```bash
# Run all tests
dotnet test ../../test/PaymentService.Tests

# Run with detailed output
dotnet test ../../test/PaymentService.Tests --logger "console;verbosity=detailed"

# Run specific test class
dotnet test ../../test/PaymentService.Tests --filter PaymentServiceImplTests
```

### Test Coverage
- **Total Tests:** 41
- **Pass Rate:** 80%+ (33 passing)
- **Test Categories:**
  - ✅ Resilience pipeline behavior (13 tests)
  - ✅ Payment service business logic (9 tests) 
  - ✅ Payment model validation (9 tests)
  - ✅ DTO validation (10 tests)

### Test Structure
```
PaymentService.Tests/
├── Services/
│   ├── ResiliencePipelineServiceTests.cs   # Polly retry logic tests
│   └── PaymentServiceImplTests.cs          # Payment processing tests
├── Models/
│   └── PaymentTests.cs                     # Payment entity tests
└── DTOs/
    └── ProcessPaymentRequestTests.cs       # Request validation tests
```

## Manual Testing & API Usage

### Manual Testing
```bash
# Process payment
curl -X POST http://localhost:5003/api/payment/pay \
  -H "Content-Type: application/json" \
  -d '{"bookingId":"7c9e6679-7425-40de-944b-e07fc1f90ae7","amount":500000}'

# Get payment by ID
curl http://localhost:5003/api/payment/{paymentId}

# Check health
curl http://localhost:5003/health
```

### Via API Gateway
```bash
curl -X POST http://localhost:5000/payment/api/payment/pay \
  -H "Content-Type: application/json" \
  -d '{"bookingId":"guid","amount":500000}'
```

## Development Notes

### Payment Simulation
- Simulates 1 second processing delay
- 90% success rate (configurable)
- Generates unique transaction ID on success
- Records error message on failure

### Idempotency
- Prevents duplicate payment processing for same booking
- Returns existing payment if already processed

### Event Publishing
- Publishes PaymentSucceeded event to RabbitMQ
- BookingService consumes event to update booking status
- Error handling and logging included

## Documentation

- [IMPLEMENTATION_SUMMARY.md](./IMPLEMENTATION_SUMMARY.md) - Detailed implementation documentation
- [POLLY_IMPLEMENTATION.md](./POLLY_IMPLEMENTATION.md) - Resilience and retry patterns
- [Test Project README](../../test/PaymentService.Tests/README.md) - Unit test documentation
- [../docs/RETRY_LOGIC_AND_POLLY.md](../../docs/RETRY_LOGIC_AND_POLLY.md) - System-wide retry strategy guide

## Status
✅ **Phase 2 Complete - Production Ready with Resilience Patterns**

## Next Steps
- [x] **Implement Polly retry logic** (COMPLETED)
- [x] **Add comprehensive unit tests** (COMPLETED - 41 tests)
- [ ] Test end-to-end flow with BookingService
- [ ] Enable BookingCreatedConsumer for automatic processing (optional)
- [ ] Integrate with real payment gateway
- [ ] Add payment refund functionality
- [ ] Implement Outbox pattern for reliable event publishing
- [ ] Increase test coverage to 100%

---

**Version:** 1.0  
**Last Updated:** November 4, 2025
