# 🚀 Quick Start Guide - Booking Microservices

## Prerequisites
- Docker Desktop installed and running
- .NET 10 SDK installed
- Git

## Getting Started

### 1. Start Infrastructure & Services
```bash
# Start all containers (infrastructure + microservices)
docker-compose up -d

# Check status
docker-compose ps

# View logs
docker-compose logs -f
```

### 2. Access Services

#### Microservices
- **API Gateway**: http://localhost:5000
- **UserService**: http://localhost:5001
- **BookingService**: http://localhost:5002
- **PaymentService**: http://localhost:5003

#### Infrastructure
- **RabbitMQ Management**: http://localhost:15672
  - Username: `guest`
  - Password: `guest`
  
- **Seq (Logging)**: http://localhost:5341
  - Username: `admin`
  - Password: `Admin@2025!SeqPass`

#### Databases
- **PostgreSQL (userdb)**: `localhost:5432`
  - Database: `userdb`
  - Username: `userservice`
  - Password: `UserSvc@2025SecurePass`

- **PostgreSQL (bookingdb)**: `localhost:5433`
  - Database: `bookingdb`
  - Username: `bookingservice`
  - Password: `BookingSvc@2025SecurePass`

- **MongoDB (paymentdb)**: `localhost:27017`
  - Database: `paymentdb`
  - Username: `paymentservice`
  - Password: `PaymentSvc@2025SecurePass`

### 3. Rebuild Services
```bash
# Rebuild all microservices
docker-compose build --no-cache userservice bookingservice paymentservice apigateway

# Rebuild and restart
docker-compose up -d --build
```

### 4. Stop Everything
```bash
# Stop all containers (keep data)
docker-compose down

# Stop and remove volumes (clean slate)
docker-compose down -v
```

## Development Workflow

### Run Service Locally (Outside Docker)
```bash
# UserService example
cd src/UserService
dotnet run

# Now accessible at http://localhost:5001
```

### Run Tests
```bash
# Run all tests
dotnet test

# Run tests for specific project
dotnet test src/UserService.Tests
```

### Database Migrations (EF Core)
```bash
cd src/UserService

# Add migration
dotnet ef migrations add InitialCreate

# Update database
dotnet ef database update
```

## Useful Commands

### Docker
```bash
# View service logs
docker logs userservice -f
docker logs bookingservice -f
docker logs paymentservice -f

# Execute command in container
docker exec -it userdb psql -U userservice -d userdb

# View container resource usage
docker stats

# Remove all stopped containers
docker container prune

# Remove all unused images
docker image prune -a
```

### Database
```bash
# Connect to PostgreSQL (userdb)
docker exec -it userdb psql -U userservice -d userdb

# Connect to PostgreSQL (bookingdb)
docker exec -it bookingdb psql -U bookingservice -d bookingdb

# Connect to MongoDB
docker exec -it paymentdb mongosh -u paymentservice -p PaymentSvc@2025SecurePass

# Check PostgreSQL status
docker exec userdb pg_isready -U userservice
```

### RabbitMQ
```bash
# List queues
docker exec rabbitmq rabbitmqctl list_queues

# List exchanges
docker exec rabbitmq rabbitmqctl list_exchanges

# List bindings
docker exec rabbitmq rabbitmqctl list_bindings
```

## Environment Variables

Edit `.env` file to change configuration:
```bash
# Database passwords
USERSERVICE_DB_PASSWORD=UserSvc@2025SecurePass
BOOKINGSERVICE_DB_PASSWORD=BookingSvc@2025SecurePass
PAYMENTSERVICE_DB_PASSWORD=PaymentSvc@2025SecurePass

# RabbitMQ
RABBITMQ_PASSWORD=RabbitMQ@2025SecurePass

# JWT
JWT_SECRET=YourSuperSecretJWTKeyForBookingSystemWithAtLeast64Characters!!!

# Seq
SEQ_ADMIN_PASSWORD=Admin@2025!SeqPass
```

After changing `.env`:
```bash
# Regenerate appsettings.Development.json files
configure-appsettings.bat

# Restart services
docker-compose down
docker-compose up -d
```

## Troubleshooting

### Services showing as unhealthy
This is expected! Health endpoints aren't implemented yet. Services are running correctly if logs show "Now listening on: http://[::]:80"

### Port conflicts
If ports are already in use, edit `docker-compose.yml` and `docker-compose.override.yml` to change port mappings.

### Database connection issues
Check that database containers are healthy:
```bash
docker-compose ps
```

Test connection:
```bash
docker exec userdb pg_isready -U userservice
```

### RabbitMQ connection refused
Wait ~30 seconds after starting - RabbitMQ takes time to initialize:
```bash
docker logs rabbitmq -f
```

### Seq not accessible
Check Seq logs for errors:
```bash
docker logs seq
```

### Build failures
Clean and rebuild:
```bash
# Clean solution
dotnet clean

# Remove obj/bin folders
Remove-Item -Recurse -Force src/*/obj, src/*/bin

# Rebuild Docker images
docker-compose build --no-cache
```

## Project Structure

```
Simple-Booking-Microservices-Project/
├── .env                          # Environment variables (NOT in git)
├── .gitignore
├── docker-compose.yml            # Main compose configuration
├── docker-compose.override.yml   # Development overrides
├── BookingSystem.sln             # Solution file
├── src/
│   ├── UserService/
│   │   ├── Dockerfile            # Multi-stage build
│   │   ├── .dockerignore
│   │   ├── nuget.config
│   │   ├── appsettings.json
│   │   └── appsettings.Development.json
│   ├── BookingService/           # Same structure
│   ├── PaymentService/           # Same structure
│   ├── ApiGateway/               # Same structure
│   └── Shared/
│       ├── Contracts/            # Event DTOs
│       ├── EventBus/             # RabbitMQ wrappers
│       └── Common/               # Shared utilities
├── scripts/
│   ├── start-infrastructure.bat
│   ├── stop-infrastructure.bat
│   ├── reset-infrastructure.bat
│   └── configure-appsettings.bat
└── docs/
    ├── DOCKER_SETUP.md
    ├── ENV_CONFIGURATION.md
    ├── PROJECT_STRUCTURE.md
    └── DOCKER_SERVICES_COMPLETE.md
```

## Next Development Steps

1. **Implement Health Checks**
   - Add `/health` endpoints to each service
   - Configure health checks with database connectivity

2. **Add Database Contexts**
   - UserService: Create `UserDbContext` and `User` entity
   - BookingService: Create `BookingDbContext` and `Booking` entity
   - PaymentService: Configure MongoDB with `Payment` model

3. **Implement Authentication**
   - JWT token generation in UserService
   - Token validation middleware in API Gateway

4. **Add API Endpoints**
   - User registration and login
   - Booking creation and retrieval
   - Payment processing

5. **Event Integration**
   - Connect RabbitMQ publishers and consumers
   - Test end-to-end event flow

## Resources

- **Project Documentation**: See `docs/` folder
- **API Endpoints**: See README.md "API Endpoints Reference" section
- **Event Catalog**: See README.md "Event Catalog" section
- **Architecture Diagram**: See README.md "Architecture" section

---

**Status**: ✅ Phase 1 Complete - Infrastructure and containerization ready!
**Current Phase**: Phase 2 - Core Services Implementation

For detailed setup information, see `DOCKER_SERVICES_COMPLETE.md`
