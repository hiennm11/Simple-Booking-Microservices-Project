# 🏛️ Database Per Service Pattern

**Category**: Architecture Patterns  
**Difficulty**: Intermediate  
**Focus**: Service autonomy through independent data storage

---

## 📖 Overview

Database Per Service is a core microservices pattern where **each service owns its database exclusively**. No service can directly access another service's database—communication happens only through APIs or events.

---

## 🎯 Why Database Per Service?

### Traditional Monolith Problem

```text
❌ Shared Database Architecture (Monolith):

┌─────────────────────────────────────────┐
│         Application Layer               │
│  ┌─────────┐  ┌─────────┐  ┌─────────┐│
│  │ User    │  │ Booking │  │ Payment ││
│  │ Module  │  │ Module  │  │ Module  ││
│  └────┬────┘  └────┬────┘  └────┬────┘│
└───────┼────────────┼─────────────┼─────┘
        │            │             │
        └────────────┼─────────────┘
                     ↓
        ┌────────────────────────┐
        │   Shared Database      │
        │  ┌──────────────────┐  │
        │  │ Users Table      │  │
        │  │ Bookings Table   │  │
        │  │ Payments Table   │  │
        │  └──────────────────┘  │
        └────────────────────────┘

Problems:
- Tight coupling between modules
- Schema changes affect all modules
- Can't scale modules independently
- Can't use different database technologies
- Single point of failure
```

### Microservices Solution

```text
✅ Database Per Service (Microservices):

┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐
│  UserService     │  │ BookingService   │  │ PaymentService   │
│  Port: 5002      │  │ Port: 5003       │  │ Port: 5004       │
└────────┬─────────┘  └────────┬─────────┘  └────────┬─────────┘
         │                     │                      │
         ↓                     ↓                      ↓
┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
│ PostgreSQL      │  │ PostgreSQL      │  │ MongoDB         │
│ userdb:5432     │  │ bookingdb:5433  │  │ paymentdb:27017 │
│                 │  │                 │  │                 │
│ Users           │  │ Bookings        │  │ payments        │
│ Roles           │  │ OutboxMessages  │  │ outbox          │
└─────────────────┘  └─────────────────┘  └─────────────────┘

Benefits:
✅ Loose coupling: Services independent
✅ Independent scaling: Scale only what needs scaling
✅ Technology diversity: PostgreSQL AND MongoDB
✅ Isolated failures: One DB down ≠ all services down
✅ Team autonomy: Each team owns its database
```

---

## 🏗️ Implementation in Your Project

### Service-Database Mapping

| Service | Database | Port | Technology | Purpose |
|---------|----------|------|------------|---------|
| UserService | `userdb` | 5432 | PostgreSQL 15 | User accounts, authentication |
| BookingService | `bookingdb` | 5433 | PostgreSQL 15 | Booking management, outbox |
| PaymentService | `paymentdb` | 27017 | MongoDB 6.0 | Payment records, transactions |

### Docker Compose Configuration

**File**: `docker-compose.yml`

```yaml
services:
  # UserService Database
  userdb:
    image: postgres:15
    container_name: userdb
    environment:
      POSTGRES_USER: admin
      POSTGRES_PASSWORD: admin123
      POSTGRES_DB: userdb
    ports:
      - "5432:5432"
    volumes:
      - userdb-data:/var/lib/postgresql/data
    networks:
      - bookingsystem-network
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U admin -d userdb"]
      interval: 10s
      timeout: 5s
      retries: 5

  # BookingService Database (Different port!)
  bookingdb:
    image: postgres:15
    container_name: bookingdb
    environment:
      POSTGRES_USER: admin
      POSTGRES_PASSWORD: admin123
      POSTGRES_DB: bookingdb
    ports:
      - "5433:5432"  # ← Host:Container port mapping
    volumes:
      - bookingdb-data:/var/lib/postgresql/data
    networks:
      - bookingsystem-network
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U admin -d bookingdb"]
      interval: 10s
      timeout: 5s
      retries: 5

  # PaymentService Database (Different technology!)
  paymentdb:
    image: mongo:6.0
    container_name: paymentdb
    environment:
      MONGO_INITDB_ROOT_USERNAME: admin
      MONGO_INITDB_ROOT_PASSWORD: admin123
      MONGO_INITDB_DATABASE: paymentdb
    ports:
      - "27017:27017"
    volumes:
      - paymentdb-data:/data/db
    networks:
      - bookingsystem-network
    healthcheck:
      test: echo 'db.runCommand("ping").ok' | mongosh localhost:27017/test --quiet
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  userdb-data:
  bookingdb-data:
  paymentdb-data:

networks:
  bookingsystem-network:
    driver: bridge
```

**Key Points**:
- Three separate databases, three separate volumes
- Different ports prevent conflicts
- Health checks ensure database readiness
- Same network for service communication

### Connection Strings

**UserService** (`appsettings.json`):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=userdb;Port=5432;Database=userdb;Username=admin;Password=admin123"
  }
}
```

**BookingService** (`appsettings.json`):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=bookingdb;Port=5432;Database=bookingdb;Username=admin;Password=admin123"
  }
}
```

**PaymentService** (`appsettings.json`):

```json
{
  "MongoDbSettings": {
    "ConnectionString": "mongodb://admin:admin123@paymentdb:27017",
    "DatabaseName": "paymentdb"
  }
}
```

**Note**: Inside Docker network, containers use **internal ports** (5432, 27017), not host ports.

---

## 🔄 Data Access Patterns

### 1. Direct Database Access (Within Service)

**✅ Allowed**: Service accesses its own database

```csharp
// BookingService/Services/BookingServiceImpl.cs
public class BookingServiceImpl : IBookingService
{
    private readonly BookingDbContext _dbContext;
    
    public async Task<Booking> CreateBookingAsync(CreateBookingRequest request)
    {
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            EventName = request.EventName,
            TicketCount = request.TicketCount,
            Status = BookingStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        
        // Direct access to BookingService's own database
        await _dbContext.Bookings.AddAsync(booking);
        await _dbContext.SaveChangesAsync();
        
        return booking;
    }
}
```

### 2. Cross-Service Data Access (Via API)

**❌ Forbidden**: Direct database access across services

```csharp
// ❌ BAD: PaymentService accessing UserService database directly
public class BadPaymentService
{
    private readonly UserDbContext _userDb; // Wrong!
    
    public async Task ProcessPaymentAsync(Guid bookingId)
    {
        // This violates Database Per Service pattern
        var user = await _userDb.Users.FindAsync(userId);
    }
}
```

**✅ Correct**: Use HTTP API

```csharp
// ✅ GOOD: PaymentService calls UserService API
public class PaymentServiceImpl
{
    private readonly IHttpClientFactory _httpClientFactory;
    
    public async Task<PaymentDto> ProcessPaymentAsync(CreatePaymentRequest request)
    {
        // Get user details via UserService API
        var httpClient = _httpClientFactory.CreateClient("UserService");
        var response = await httpClient.GetAsync($"/api/users/{request.UserId}");
        
        if (!response.IsSuccessStatusCode)
        {
            throw new UserNotFoundException();
        }
        
        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        
        // Now process payment with user info
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = request.BookingId,
            UserId = request.UserId,
            UserEmail = user.Email, // Got from API, not direct DB access
            Amount = request.Amount,
            Status = PaymentStatus.Pending
        };
        
        await _dbContext.Payments.AddAsync(payment);
        await _dbContext.SaveChangesAsync();
        
        return MapToDto(payment);
    }
}
```

### 3. Cross-Service Data Access (Via Events)

**✅ Best for async operations**: Publish event, other service reacts

```csharp
// BookingService publishes event
public async Task<Booking> CreateBookingAsync(CreateBookingRequest request)
{
    var booking = new Booking { /* ... */ };
    await _dbContext.Bookings.AddAsync(booking);
    await _dbContext.SaveChangesAsync();
    
    // Publish event for other services
    await _eventBus.PublishAsync(new BookingCreatedEvent
    {
        BookingId = booking.Id,
        UserId = booking.UserId,
        TotalAmount = booking.TotalAmount,
        EventName = booking.EventName
    });
    
    return booking;
}

// PaymentService reacts to event
public class BookingCreatedEventHandler : IEventHandler<BookingCreatedEvent>
{
    public async Task HandleAsync(BookingCreatedEvent evt)
    {
        // Create payment in PaymentService's own database
        var payment = new Payment
        {
            BookingId = evt.BookingId,
            Amount = evt.TotalAmount,
            Status = PaymentStatus.Pending
        };
        
        await _dbContext.Payments.AddAsync(payment);
        await _dbContext.SaveChangesAsync();
    }
}
```

---

## 📊 Polyglot Persistence

**Definition**: Using different database technologies for different services based on their needs.

### Your Project Example

| Service | Database | Why This Choice? |
|---------|----------|------------------|
| **UserService** | PostgreSQL | • Strong ACID guarantees<br>• Relational data (users ↔ roles)<br>• Foreign keys enforce integrity<br>• Mature authentication patterns |
| **BookingService** | PostgreSQL | • Transactions critical (booking + outbox atomic)<br>• Complex queries (filter by status, date, user)<br>• Relational (booking → user, booking → event)<br>• EF Core migrations easy |
| **PaymentService** | MongoDB | • Flexible schema (payment methods vary)<br>• Document model fits payment data well<br>• High write throughput<br>• Horizontal scaling ready<br>• JSON-native (API responses fast) |

### PostgreSQL Schema (BookingService)

```sql
-- Bookings table
CREATE TABLE bookings (
    id UUID PRIMARY KEY,
    user_id VARCHAR(255) NOT NULL,
    event_name VARCHAR(255) NOT NULL,
    ticket_count INTEGER NOT NULL,
    total_amount DECIMAL(10, 2) NOT NULL,
    status VARCHAR(50) NOT NULL,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP,
    INDEX idx_user_id (user_id),
    INDEX idx_status (status),
    INDEX idx_created_at (created_at)
);

-- Outbox table (for guaranteed event delivery)
CREATE TABLE outbox_messages (
    id UUID PRIMARY KEY,
    event_type VARCHAR(255) NOT NULL,
    payload TEXT NOT NULL,
    created_at TIMESTAMP NOT NULL,
    processed_at TIMESTAMP,
    INDEX idx_processed (processed_at)
);
```

### MongoDB Schema (PaymentService)

```javascript
// payments collection
{
  "_id": ObjectId("507f1f77bcf86cd799439011"),
  "bookingId": "123e4567-e89b-12d3-a456-426614174000",
  "userId": "user-123",
  "amount": 150.00,
  "currency": "USD",
  "status": "Completed",
  "paymentMethod": {
    "type": "CreditCard",
    "last4": "4242",
    "brand": "Visa"
  },
  "transactionId": "txn_abc123",
  "createdAt": ISODate("2025-11-07T10:30:00Z"),
  "updatedAt": ISODate("2025-11-07T10:30:05Z"),
  "metadata": {
    "ipAddress": "192.168.1.1",
    "userAgent": "Mozilla/5.0..."
  }
}

// Flexible schema: Easy to add new fields without migration
// Embedded documents: paymentMethod is nested (no JOIN needed)
// Index on bookingId for fast lookups
db.payments.createIndex({ "bookingId": 1 })
```

---

## 🔄 Data Consistency Challenges

### Challenge 1: Foreign Key References Don't Exist Across Services

**Problem**: BookingService stores `UserId`, but can't use foreign key to UserService database.

**Traditional Solution** (Monolith):

```sql
-- In shared database
CREATE TABLE bookings (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL,
    FOREIGN KEY (user_id) REFERENCES users(id)
);
-- Database enforces: Can't create booking for non-existent user
```

**Microservices Solution**:

```csharp
// BookingService validates via UserService API
public async Task<Booking> CreateBookingAsync(CreateBookingRequest request)
{
    // 1. Validate user exists by calling UserService
    var userExists = await _userServiceClient.UserExistsAsync(request.UserId);
    if (!userExists)
    {
        throw new UserNotFoundException(request.UserId);
    }
    
    // 2. Create booking (no foreign key constraint in database)
    var booking = new Booking
    {
        UserId = request.UserId, // Stored as string, not FK
        // ...
    };
    
    await _dbContext.Bookings.AddAsync(booking);
    await _dbContext.SaveChangesAsync();
    
    return booking;
}

// UserServiceClient calls UserService API
public async Task<bool> UserExistsAsync(string userId)
{
    var httpClient = _httpClientFactory.CreateClient("UserService");
    var response = await httpClient.GetAsync($"/api/users/{userId}/exists");
    
    if (response.StatusCode == HttpStatusCode.NotFound)
        return false;
    
    return response.IsSuccessStatusCode;
}
```

**Trade-off**:
- ✅ Services independent (UserService down doesn't prevent booking creation)
- ❌ No database-level referential integrity
- ⚖️ Solution: Application-level validation + eventual consistency

### Challenge 2: Joins Across Services

**Problem**: Can't do SQL JOIN across different databases.

**Traditional Query** (Monolith):

```sql
-- Get booking with user details (single query, fast)
SELECT 
    b.id, b.event_name, b.total_amount,
    u.email, u.full_name
FROM bookings b
JOIN users u ON b.user_id = u.id
WHERE b.id = '123e4567-e89b-12d3-a456-426614174000';
```

**Microservices Solution 1: API Composition** (Synchronous)

```csharp
// BookingService endpoint
[HttpGet("{id}")]
public async Task<ActionResult<BookingWithUserDto>> GetBookingWithUserAsync(Guid id)
{
    // 1. Get booking from own database
    var booking = await _dbContext.Bookings.FindAsync(id);
    if (booking == null)
        return NotFound();
    
    // 2. Get user details from UserService (separate HTTP call)
    var user = await _userServiceClient.GetUserAsync(booking.UserId);
    
    // 3. Compose result
    return Ok(new BookingWithUserDto
    {
        BookingId = booking.Id,
        EventName = booking.EventName,
        TotalAmount = booking.TotalAmount,
        UserEmail = user.Email,
        UserFullName = user.FullName
    });
}
```

**Trade-offs**:
- ✅ Services independent
- ❌ Slower (2 network calls vs 1 database query)
- ❌ More failure points (UserService could be down)

**Microservices Solution 2: Data Duplication** (Eventual Consistency)

```csharp
// BookingService stores copy of user email
public class Booking
{
    public Guid Id { get; set; }
    public string UserId { get; set; }
    public string UserEmail { get; set; } // ← Duplicated from UserService
    public string EventName { get; set; }
    // ...
}

// When user email changes, UserService publishes event
public class UserEmailChangedEvent : IntegrationEvent
{
    public string UserId { get; set; }
    public string NewEmail { get; set; }
}

// BookingService updates its copy
public class UserEmailChangedEventHandler : IEventHandler<UserEmailChangedEvent>
{
    public async Task HandleAsync(UserEmailChangedEvent evt)
    {
        var bookings = await _dbContext.Bookings
            .Where(b => b.UserId == evt.UserId)
            .ToListAsync();
        
        foreach (var booking in bookings)
        {
            booking.UserEmail = evt.NewEmail;
        }
        
        await _dbContext.SaveChangesAsync();
    }
}
```

**Trade-offs**:
- ✅ Fast reads (single database query)
- ✅ BookingService independent (doesn't call UserService)
- ❌ Data duplication
- ❌ Eventual consistency (email may be stale for a moment)

---

## ⚙️ Database Migrations

Each service manages its own database schema independently.

### UserService Migration

```bash
# Create migration
cd src/UserService
dotnet ef migrations add InitialCreate --context UserDbContext

# Apply migration
dotnet ef database update --context UserDbContext
```

**Migration File**: `Migrations/20251107_InitialCreate.cs`

```csharp
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<string>(nullable: false),
                Email = table.Column<string>(maxLength: 255, nullable: false),
                FullName = table.Column<string>(maxLength: 255, nullable: false),
                PasswordHash = table.Column<string>(nullable: false),
                CreatedAt = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Users", x => x.Id);
            });
        
        migrationBuilder.CreateIndex(
            name: "IX_Users_Email",
            table: "Users",
            column: "Email",
            unique: true);
    }
}
```

### BookingService Migration

```bash
cd src/BookingService
dotnet ef migrations add InitialCreate --context BookingDbContext
dotnet ef database update --context BookingDbContext
```

**Key Point**: UserService and BookingService migrations are **completely independent**. No coordination needed.

---

## 🎯 Benefits of Database Per Service

### 1. Technology Independence

```text
Need full-text search? → Use Elasticsearch for ProductService
Need graph relationships? → Use Neo4j for RecommendationService
Need high write throughput? → Use Cassandra for AnalyticsService
Need strong consistency? → Use PostgreSQL for PaymentService

In your project:
- PostgreSQL for UserService and BookingService (relational, ACID)
- MongoDB for PaymentService (flexible schema, document model)
```

### 2. Independent Scaling

```yaml
# Scale BookingService database independently
services:
  bookingdb:
    deploy:
      replicas: 3  # Master + 2 read replicas
  
  userdb:
    # Still single instance (user reads not high)
```

### 3. Isolated Failures

```text
Scenario: PaymentService database crashes

Without Database Per Service:
❌ Entire system down (shared database)

With Database Per Service:
✅ UserService still works
✅ BookingService still works
❌ PaymentService down (only affected service)
```

### 4. Team Autonomy

```text
BookingService Team:
- Owns bookingdb schema
- Can change schema without coordinating with other teams
- Can optimize indexes for their queries
- Can backup/restore independently

UserService Team:
- Owns userdb schema
- Complete control over data model
- No conflicts with BookingService team
```

---

## ⚠️ Challenges and Solutions

### Challenge 1: Distributed Transactions

**Problem**: Atomic operations across services.

**Example**: Create booking AND charge payment atomically.

**Solution**: Saga Pattern (see `/brief/01-architecture-patterns/event-driven-architecture.md`)

```text
1. BookingService creates booking (local transaction)
2. Publishes BookingCreatedEvent
3. PaymentService processes payment (separate transaction)
4. If payment fails, publishes PaymentFailedEvent
5. BookingService cancels booking (compensating action)
```

### Challenge 2: Querying Across Services

**Problem**: Generate report with data from multiple services.

**Solutions**:

1. **API Composition**: Gateway aggregates data from multiple services
2. **CQRS**: Separate read model aggregates data from events
3. **Dedicated Reporting Database**: Replicate data to reporting DB

### Challenge 3: Data Duplication

**Problem**: Same data stored in multiple services (e.g., user email).

**Solution**: Eventual consistency with events

```text
UserService: Email changes → Publishes UserEmailChangedEvent
BookingService: Updates cached email in bookings
PaymentService: Updates cached email in payments

Temporarily inconsistent, eventually consistent
```

---

## 🎓 Key Takeaways

### Design Principles

1. **One Database Per Service**: Each service exclusively owns its database
2. **No Shared Databases**: Services communicate via APIs or events, never direct DB access
3. **Polyglot Persistence**: Choose best database for each service's needs
4. **Eventual Consistency**: Accept temporary inconsistency for autonomy
5. **Service Boundaries Define Database Boundaries**: If shared database needed, services are too tightly coupled

### In Your Project

| Aspect | Implementation |
|--------|---------------|
| **UserService** | PostgreSQL (userdb:5432) - Relational, ACID |
| **BookingService** | PostgreSQL (bookingdb:5433) - Transactions, Outbox |
| **PaymentService** | MongoDB (paymentdb:27017) - Flexible, Document |
| **Cross-Service Data** | HTTP APIs + Event Bus |
| **Consistency** | Eventual consistency with Saga pattern |
| **Migrations** | Independent per service (EF Core Migrations) |

### When to Use Database Per Service

✅ **Use when**:
- Building true microservices
- Need independent deployments
- Different data models per service
- Teams work independently
- High scalability required

❌ **Avoid when**:
- Small monolith is sufficient
- Strong consistency across all data required
- Team too small to manage multiple databases
- Queries across services are primary use case

---

## 📚 Further Study

### Related Documents
- [Microservices Fundamentals](./microservices-fundamentals.md)
- [Event-Driven Architecture](./event-driven-architecture.md)
- [Outbox Pattern](./outbox-pattern.md)
- [Distributed Systems Theory](../07-computer-science/distributed-systems-theory.md)

### Project Documentation
- `/docs/phase1-foundation/PROJECT_STRUCTURE.md`
- `/docs/phase1-foundation/DOCKER_SETUP.md`
- `docker-compose.yml`

### Books
- "Building Microservices" by Sam Newman - Chapter on Data Management
- "Designing Data-Intensive Applications" by Martin Kleppmann - Distributed Data

---

**Last Updated**: November 7, 2025  
**Status**: ✅ Fully implemented in your project  
**Next**: [API Gateway Pattern](./api-gateway-pattern.md)
