# 🚀 Quick Start Guide

This guide will help you get the Booking Microservices System up and running in minutes.

## Prerequisites

Before you begin, ensure you have the following installed:

- ✅ [.NET 8 SDK or later](https://dotnet.microsoft.com/download)
- ✅ [Docker Desktop](https://www.docker.com/products/docker-desktop)
- ✅ [Git](https://git-scm.com/)
- ✅ IDE: [Visual Studio 2022](https://visualstudio.microsoft.com/) or [VS Code](https://code.visualstudio.com/)

## Step 1: Clone the Repository

```bash
git clone https://github.com/hiennm11/Simple-Booking-Microservices-Project.git
cd Simple-Booking-Microservices-Project
```

## Step 2: Start Infrastructure Services

### Option A: Using the Batch Script (Windows)
```cmd
start-infrastructure.bat
```

### Option B: Using Docker Compose Directly
```bash
docker-compose up -d
```

### Verify Services are Running
```bash
docker-compose ps
```

All services should show status as "healthy" or "running".

## Step 3: Access Service Dashboards

### RabbitMQ Management UI
- **URL:** http://localhost:15672
- **Username:** guest
- **Password:** guest
- **What to check:** Verify queues are created

### Seq Logging UI (Optional)
- **URL:** http://localhost:5341
- **What to check:** View structured logs from services

## Step 4: Build the Solution

```bash
dotnet build BookingSystem.sln
```

Expected output: `Build succeeded`

## Step 5: Run Database Migrations

### UserService
```bash
cd src/UserService
dotnet ef database update
cd ../..
```

### BookingService
```bash
cd src/BookingService
dotnet ef database update
cd ../..
```

*Note: MongoDB (PaymentService) doesn't require migrations*

## Step 6: Start the Services

Open **4 separate terminal windows** and run each service:

### Terminal 1: UserService
```bash
cd src/UserService
dotnet run
```
Default URL: http://localhost:5001

### Terminal 2: BookingService
```bash
cd src/BookingService
dotnet run
```
Default URL: http://localhost:5002

### Terminal 3: PaymentService
```bash
cd src/PaymentService
dotnet run
```
Default URL: http://localhost:5003

### Terminal 4: ApiGateway
```bash
cd src/ApiGateway
dotnet run
```
Default URL: http://localhost:5000

## Step 7: Test the System

### Access Swagger UI
- **UserService:** http://localhost:5001/swagger
- **BookingService:** http://localhost:5002/swagger
- **PaymentService:** http://localhost:5003/swagger
- **ApiGateway:** http://localhost:5000/swagger

### Test API Endpoints

#### 1. Register a User
```bash
curl -X POST http://localhost:5001/api/users/register \
  -H "Content-Type: application/json" \
  -d "{\"name\":\"John Doe\",\"email\":\"john@example.com\",\"password\":\"Password123!\"}"
```

#### 2. Login
```bash
curl -X POST http://localhost:5001/api/users/login \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"john@example.com\",\"password\":\"Password123!\"}"
```

Save the JWT token from the response.

#### 3. Create a Booking
```bash
curl -X POST http://localhost:5002/api/bookings \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -d "{\"userId\":\"USER_GUID\",\"roomId\":\"ROOM-101\",\"amount\":500000}"
```

#### 4. Process Payment
```bash
curl -X POST http://localhost:5003/api/payment/pay \
  -H "Content-Type: application/json" \
  -d "{\"bookingId\":\"BOOKING_GUID\",\"amount\":500000}"
```

#### 5. Verify Booking Updated
```bash
curl -X GET http://localhost:5002/api/bookings/BOOKING_GUID \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

Check that the booking status is now "CONFIRMED".

## Step 8: Monitor Event Flow

### Check RabbitMQ Queues
1. Go to http://localhost:15672
2. Navigate to **Queues** tab
3. You should see:
   - `booking_created` queue
   - `payment_succeeded` queue

### Check Logs in Seq (Optional)
1. Go to http://localhost:5341
2. View logs from all services
3. Search for event names like "BookingCreated" or "PaymentSucceeded"

## Troubleshooting

### Issue: Port Already in Use
**Solution:** Stop other applications using the ports or change ports in `appsettings.json`

### Issue: Database Connection Failed
**Solution:** 
1. Verify Docker containers are running: `docker-compose ps`
2. Check container health: `docker-compose logs [service-name]`
3. Restart infrastructure: `reset-infrastructure.bat`

### Issue: RabbitMQ Connection Refused
**Solution:**
1. Wait 30 seconds for RabbitMQ to fully start
2. Check RabbitMQ logs: `docker-compose logs rabbitmq`
3. Verify management UI is accessible: http://localhost:15672

### Issue: Services Can't Find Each Other
**Solution:** Ensure all services are on the same Docker network (booking-network)

## Stopping Everything

### Stop Services (Ctrl+C in each terminal)

### Stop Infrastructure
```bash
# Option A: Using batch script
stop-infrastructure.bat

# Option B: Using Docker Compose
docker-compose down

# Option C: Stop and remove all data
docker-compose down -v
```

## Next Steps

Now that your system is running:

1. ✅ Explore the Swagger UI for each service
2. ✅ Monitor events in RabbitMQ Management UI
3. ✅ View logs in Seq
4. ✅ Try the end-to-end flow: Register → Login → Create Booking → Process Payment
5. ✅ Review the code structure in your IDE
6. ✅ Read [DOCKER_SETUP.md](DOCKER_SETUP.md) for detailed Docker documentation
7. ✅ Read [PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md) for architecture details

## Quick Commands Reference

```bash
# Infrastructure
docker-compose up -d              # Start all infrastructure
docker-compose down               # Stop infrastructure
docker-compose down -v            # Stop and remove data
docker-compose ps                 # Check status
docker-compose logs -f [service]  # View logs

# Build
dotnet build                      # Build solution
dotnet clean                      # Clean build artifacts

# Run services
dotnet run --project src/UserService/UserService.csproj
dotnet run --project src/BookingService/BookingService.csproj
dotnet run --project src/PaymentService/PaymentService.csproj
dotnet run --project src/ApiGateway/ApiGateway.csproj

# Database
dotnet ef database update         # Run migrations
dotnet ef migrations add [name]   # Create migration
```

## Getting Help

- 📚 Read the [README.md](README.md) for detailed project information
- 🐳 Check [DOCKER_SETUP.md](DOCKER_SETUP.md) for Docker troubleshooting
- 🏗️ Review [PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md) for architecture
- 📝 See API documentation at `/swagger` endpoints
- 🐛 Check GitHub Issues for known problems

## Success Criteria

You'll know everything is working when:

1. ✅ All Docker containers show "healthy" status
2. ✅ All 4 services start without errors
3. ✅ Swagger UI loads for each service
4. ✅ You can register and login a user
5. ✅ You can create a booking
6. ✅ Payment processing triggers booking status update
7. ✅ Events appear in RabbitMQ queues
8. ✅ Logs appear in console and Seq

Congratulations! Your Booking Microservices System is now running! 🎉
