# Project Structure Summary

## Solution: BookingSystem.sln

### Projects Created

1. **UserService** (ASP.NET Core Web API)
   - Location: `src/UserService/`
   - Purpose: Manage users, register, login
   - Database: PostgreSQL (to be configured)

2. **BookingService** (ASP.NET Core Web API)
   - Location: `src/BookingService/`
   - Purpose: Create and manage bookings
   - Database: PostgreSQL (to be configured)

3. **PaymentService** (ASP.NET Core Web API)
   - Location: `src/PaymentService/`
   - Purpose: Process payments and publish success events
   - Database: MongoDB (to be configured)

4. **ApiGateway** (ASP.NET Core Web API)
   - Location: `src/ApiGateway/`
   - Purpose: Unified entry point for clients
   - Technology: Ocelot/YARP (to be configured)

5. **Shared** (Class Library)
   - Location: `src/Shared/`
   - Purpose: Common code, event contracts, and utilities
   - Subdirectories:
     - `Contracts/` - Event DTOs (BookingCreatedEvent, PaymentSucceededEvent, PaymentFailedEvent)
     - `EventBus/` - RabbitMQ wrapper interfaces (IEventBus, IEventConsumer)
     - `Common/` - Base classes and helpers (ApiResponse, BaseEntity)

## Project Structure
```
Simple-Booking-Microservices-Project/
│
├── BookingSystem.sln
├── README.md
├── .gitignore
│
└── src/
    ├── UserService/
    │   ├── UserService.csproj
    │   ├── Program.cs
    │   ├── appsettings.json
    │   └── ... (default Web API files)
    │
    ├── BookingService/
    │   ├── BookingService.csproj
    │   ├── Program.cs
    │   ├── appsettings.json
    │   └── ... (default Web API files)
    │
    ├── PaymentService/
    │   ├── PaymentService.csproj
    │   ├── Program.cs
    │   ├── appsettings.json
    │   └── ... (default Web API files)
    │
    ├── ApiGateway/
    │   ├── ApiGateway.csproj
    │   ├── Program.cs
    │   ├── appsettings.json
    │   └── ... (default Web API files)
    │
    └── Shared/
        ├── Shared.csproj
        ├── Contracts/
        │   ├── BookingCreatedEvent.cs
        │   ├── PaymentSucceededEvent.cs
        │   └── PaymentFailedEvent.cs
        ├── EventBus/
        │   ├── IEventBus.cs
        │   └── IEventConsumer.cs
        └── Common/
            ├── ApiResponse.cs
            └── BaseEntity.cs
```

## Build Status
✅ All projects build successfully

## Next Steps (Phase 1 Remaining Tasks)
- [ ] Setup Docker Compose with RabbitMQ, PostgreSQL, MongoDB
- [ ] Implement RabbitMQ wrapper/helper classes in Shared/EventBus
- [ ] Verify all containers running and accessible
- [ ] Basic health check endpoints for each service

## Commands Reference

### Build the solution
```bash
dotnet build BookingSystem.sln
```

### Run a specific service
```bash
dotnet run --project src/UserService/UserService.csproj
dotnet run --project src/BookingService/BookingService.csproj
dotnet run --project src/PaymentService/PaymentService.csproj
dotnet run --project src/ApiGateway/ApiGateway.csproj
```

### Add a NuGet package to a project
```bash
dotnet add src/UserService/UserService.csproj package <PackageName>
```

### Add project reference (e.g., services referencing Shared)
```bash
dotnet add src/UserService/UserService.csproj reference src/Shared/Shared.csproj
dotnet add src/BookingService/BookingService.csproj reference src/Shared/Shared.csproj
dotnet add src/PaymentService/PaymentService.csproj reference src/Shared/Shared.csproj
```

## Notes
- Using .NET 10 preview SDK
- All services are ASP.NET Core Web API projects with minimal API structure
- Shared library contains event contracts and common utilities
- Ready for Phase 2: Core Services Implementation
