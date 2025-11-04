# Booking Service

## Overview
The Booking Service is a microservice responsible for managing booking operations in the system. It handles booking creation, retrieval, and status updates, and publishes events when bookings are created.

## Features
- ✅ Create new bookings
- ✅ Get booking by ID
- ✅ Get all bookings for a specific user
- ✅ Update booking status (PENDING → CONFIRMED → CANCELLED)
- ✅ Publish `BookingCreated` events to RabbitMQ
- ✅ Consume `PaymentSucceeded` events to automatically confirm bookings
- ✅ PostgreSQL database with Entity Framework Core
- ✅ Structured logging with Serilog and Seq
- ✅ Health checks for database connectivity

## Technology Stack
- **Framework**: ASP.NET Core 10.0 (preview)
- **Database**: PostgreSQL
- **ORM**: Entity Framework Core
- **Message Broker**: RabbitMQ
- **Logging**: Serilog + Seq
- **API Documentation**: OpenAPI/Swagger

## Architecture

### Database Schema
```sql
CREATE TABLE bookings (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL,
    room_id VARCHAR(100) NOT NULL,
    amount DECIMAL(18,2) NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'PENDING',
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NULL,
    confirmed_at TIMESTAMP NULL,
    cancelled_at TIMESTAMP NULL,
    cancellation_reason VARCHAR(500) NULL
);

CREATE INDEX idx_bookings_user_id ON bookings(user_id);
CREATE INDEX idx_bookings_status ON bookings(status);
CREATE INDEX idx_bookings_created_at ON bookings(created_at);
```

### Booking Statuses
- **PENDING**: Initial state after booking creation
- **CONFIRMED**: Booking confirmed after successful payment
- **CANCELLED**: Booking cancelled by user or system

### Event-Driven Communication

#### Published Events
**BookingCreated** - Published when a new booking is created
```json
{
  "eventId": "guid",
  "eventName": "BookingCreated",
  "timestamp": "2025-11-03T10:00:00Z",
  "data": {
    "bookingId": "guid",
    "userId": "guid",
    "roomId": "ROOM-101",
    "amount": 500000,
    "status": "PENDING"
  }
}
```

#### Consumed Events
**PaymentSucceeded** - Consumed to update booking status to CONFIRMED
```json
{
  "eventId": "guid",
  "eventName": "PaymentSucceeded",
  "timestamp": "2025-11-03T10:00:30Z",
  "data": {
    "paymentId": "guid",
    "bookingId": "guid",
    "amount": 500000,
    "status": "SUCCESS"
  }
}
```

## API Endpoints

### Create Booking
```http
POST /api/bookings
Content-Type: application/json

{
  "userId": "a3bb189e-8bf9-3888-9912-ace4e6543002",
  "roomId": "ROOM-101",
  "amount": 500000
}
```

**Response**: 201 Created
```json
{
  "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "userId": "a3bb189e-8bf9-3888-9912-ace4e6543002",
  "roomId": "ROOM-101",
  "amount": 500000,
  "status": "PENDING",
  "createdAt": "2025-11-03T10:00:00Z",
  "updatedAt": null,
  "confirmedAt": null,
  "cancelledAt": null,
  "cancellationReason": null
}
```

### Get Booking by ID
```http
GET /api/bookings/{id}
```

**Response**: 200 OK (same structure as create response)

### Get Bookings by User ID
```http
GET /api/bookings/user/{userId}
```

**Response**: 200 OK (array of bookings)

### Update Booking Status
```http
PATCH /api/bookings/{id}/status
Content-Type: application/json

{
  "status": "CONFIRMED" | "CANCELLED",
  "cancellationReason": "Optional reason if cancelling"
}
```

**Response**: 200 OK (updated booking)

### Health Check
```http
GET /health
```

## Configuration

### Environment Variables
Required environment variables (loaded via appsettings.json):

```bash
# PostgreSQL Database
BOOKINGDB_HOST=localhost
BOOKINGDB_PORT=5432
BOOKINGDB_NAME=bookingdb
BOOKINGDB_USER=postgres
BOOKINGDB_PASSWORD=postgres

# RabbitMQ
RABBITMQ_HOST=localhost
RABBITMQ_PORT=5672
RABBITMQ_USER=guest
RABBITMQ_PASSWORD=guest
RABBITMQ_VHOST=/

# Seq Logging
SEQ_URL=http://localhost:5341
SEQ_API_KEY=
```

### appsettings.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=${BOOKINGDB_HOST};Port=${BOOKINGDB_PORT};Database=${BOOKINGDB_NAME};Username=${BOOKINGDB_USER};Password=${BOOKINGDB_PASSWORD}"
  },
  "RabbitMQ": {
    "HostName": "${RABBITMQ_HOST}",
    "Port": "${RABBITMQ_PORT}",
    "UserName": "${RABBITMQ_USER}",
    "Password": "${RABBITMQ_PASSWORD}",
    "VirtualHost": "${RABBITMQ_VHOST}",
    "Queues": {
      "BookingCreated": "booking_created",
      "PaymentSucceeded": "payment_succeeded"
    }
  },
  "Seq": {
    "ServerUrl": "${SEQ_URL}",
    "ApiKey": "${SEQ_API_KEY}"
  }
}
```

## Project Structure
```
BookingService/
├── Controllers/
│   └── BookingsController.cs          # API endpoints
├── Services/
│   └── BookingServiceImpl.cs          # Business logic
├── Data/
│   ├── BookingDbContext.cs            # EF Core context
│   └── BookingDbContextFactory.cs     # Design-time factory
├── Models/
│   └── Booking.cs                     # Entity model
├── DTOs/
│   ├── CreateBookingRequest.cs        # Request DTO
│   ├── BookingResponse.cs             # Response DTO
│   └── UpdateBookingStatusRequest.cs  # Status update DTO
├── EventBus/
│   └── RabbitMQEventBus.cs           # RabbitMQ publisher
├── Consumers/
│   └── PaymentSucceededConsumer.cs   # Event consumer
├── Migrations/
│   └── 20251103072504_InitialCreate.cs
├── Program.cs                         # Application entry point
├── appsettings.json                   # Configuration
└── BookingService.http               # HTTP test file
```

## Running the Service

### Local Development (No Docker)
```bash
# Navigate to project directory
cd src/BookingService

# Restore dependencies
dotnet restore

# Apply database migrations
dotnet ef database update

# Run the service
dotnet run
```

Service will be available at: `http://localhost:5023`

### Docker
```bash
# From solution root
docker-compose up -d bookingservice
```

## Database Migrations

### Create a new migration
```bash
dotnet ef migrations add <MigrationName>
```

### Apply migrations
```bash
dotnet ef database update
```

### Remove last migration
```bash
dotnet ef migrations remove
```

## Testing

### Using the HTTP file
Open `BookingService.http` in Visual Studio or VS Code with REST Client extension.

### Using curl
```bash
# Create a booking
curl -X POST http://localhost:5023/api/bookings \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "a3bb189e-8bf9-3888-9912-ace4e6543002",
    "roomId": "ROOM-101",
    "amount": 500000
  }'

# Get booking by ID
curl http://localhost:5023/api/bookings/{id}

# Health check
curl http://localhost:5023/health
```

## Event Flow

### Booking Creation Flow
1. Client calls `POST /api/bookings`
2. Service validates request
3. Creates booking in database with status "PENDING"
4. Publishes `BookingCreated` event to RabbitMQ
5. Returns booking details to client

### Payment Success Flow
1. PaymentService processes payment successfully
2. Publishes `PaymentSucceeded` event to RabbitMQ
3. BookingService consumes the event via `PaymentSucceededConsumer`
4. Updates booking status to "CONFIRMED"
5. Records confirmation timestamp

## Error Handling
- All endpoints return appropriate HTTP status codes
- Global exception handling via middleware (inherited from base setup)
- Failed event publications are logged but don't fail the booking creation
- Event processing includes retry logic (via RabbitMQ's requeue mechanism)

## Logging
All operations are logged with structured logging via Serilog:
- Information: Normal operations
- Warning: Business-level issues (e.g., booking not found)
- Error: Technical failures (e.g., database connection issues)

Logs are sent to:
- Console (for local development)
- Seq (for centralized log aggregation)

## Dependencies
- **Shared**: Common library with event contracts and EventBus interfaces
- **RabbitMQ**: Message broker for event-driven communication
- **PostgreSQL**: Relational database for booking data
- **Seq**: Centralized logging platform (optional)

## Future Enhancements
- [ ] Add booking validation rules (e.g., room availability)
- [ ] Implement booking expiration (auto-cancel after X minutes if not paid)
- [ ] Add pagination for user bookings endpoint
- [ ] Implement idempotency for booking creation
- [ ] Add saga pattern for complex workflows
- [ ] Implement unit and integration tests
- [ ] Add authentication/authorization
- [ ] Rate limiting for API endpoints
- [ ] Caching layer for frequently accessed bookings

## Troubleshooting

### Database connection fails
- Verify PostgreSQL is running: `docker ps | grep postgres`
- Check connection string in appsettings.json
- Ensure migrations are applied: `dotnet ef database update`

### RabbitMQ events not being published/consumed
- Verify RabbitMQ is running: `docker ps | grep rabbitmq`
- Check RabbitMQ management UI: `http://localhost:15672`
- Verify queue configuration in appsettings.json
- Check logs in Seq for error messages

### Service won't start
- Check if port 5023 is already in use
- Verify all environment variables are set correctly
- Check logs for detailed error messages

## License
MIT © 2025 - Educational purpose only
