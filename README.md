# 🏗️ Simple Booking Microservices Project

A lightweight microservices-based demo built with **ASP.NET Core**, **RabbitMQ**, and **Ocelot API Gateway** to demonstrate core microservices concepts and communication patterns.

---

## 🧭 Overview

This project simulates a simple **Booking System** with the following services:

| Service | Responsibility | Communication | DB |
|----------|----------------|----------------|---|
| **UserService** | Manage users, register, login | REST (HTTP) | PostgreSQL
| **BookingService** | Create and manage bookings | REST API + Event Consumer | PostgreSQL
| **PaymentService** | Process payments and publish success events | REST API + RabbitMQ Producer | MongoDB
| **API Gateway** | Unified entry point for clients | HTTP reverse proxy via Ocelot | - 
| **RabbitMQ** |	Event bus for event PaymentSucceeded | AMQP protocol | -
---

## ⚙️  Technology Stack

| Component | Technology                 | Reason                            |
| --------- | -------------------------- | --------------------------------- |
| API       | ASP.NET Core 8 minimal API | Lightweight, easy to split        |
| Database  | PostgreSQL, MongoDB        | Learn Database per service pattern|
| Event Bus | RabbitMQ                   | Easy to learn, Docker-friendly    |
| Gateway   | YARP (or Ocelot)           | Reverse proxy routing             |
| Auth      | JWT Bearer                 | Simple for development            |
| Logging   | Serilog + Seq (optional)   | Monitor event flow                |

## 🧩 Main Components and Roles

| Component                         | Role                                                    | Technology                |
| --------------------------------- | ------------------------------------------------------- | ------------------------- |
| **API Gateway**                   | Single entry point for clients. Routing, auth, rate-limit | YARP / Ocelot          |
| **User Service**                  | Register, login, user management                        | ASP.NET Core + PostgreSQL |
| **Booking Service**               | CRUD Booking + Publish events                           | ASP.NET Core + PostgreSQL |
| **Payment Service**               | Consume events, process payments                        | ASP.NET Core + MongoDB    |
| **Event Bus**                     | Asynchronous message exchange                           | RabbitMQ                  |
| **Logger / Monitor** *(optional)* | Monitor event flow, health checks                       | Serilog + Seq             |

## 🧠 Patterns Demonstrated in This Architecture

| Pattern                        | Description                                                              |
| ------------------------------ | ------------------------------------------------------------------------ |
| **Event-driven Architecture**  | Services communicate via events (RabbitMQ).                              |
| **Database per Service**       | Each service has its own database for independent deployment.            |
| **API Gateway**                | Coordinating gateway and single entry point.                             |
| **Event Choreography**         | No "central orchestrator"; each service reacts to events independently.  |
| **Retry / Resilience Pattern** | Use Polly to retry when event sending fails or connection is lost.       |


## 💾 Database Assignments

| Service | Database | Reason |
|---------|----------|--------|
| UserService | PostgreSQL | Relational data, ACID compliance for user accounts |
| BookingService | PostgreSQL | Relational data, needs transactions and foreign keys |
| PaymentService | MongoDB | Flexible schema for payment logs and audit trails |

**Note:** Each service uses a separate database instance (Database per Service pattern)

---

## 📨 Event Catalog

### BookingCreated
**Publisher:** BookingService  
**Consumers:** PaymentService (future), NotificationService (future)  
**Queue:** `booking_created`

```json
{
  "eventId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "eventName": "BookingCreated",
  "timestamp": "2025-10-29T10:00:00Z",
  "data": {
    "bookingId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "userId": "a3bb189e-8bf9-3888-9912-ace4e6543002",
    "roomId": "ROOM-101",
    "amount": 500000,
    "status": "PENDING"
  }
}
```

### PaymentSucceeded
**Publisher:** PaymentService  
**Consumers:** BookingService  
**Queue:** `payment_succeeded`

```json
{
  "eventId": "8ea42f73-6821-4523-a3d1-1f74b8c9e123",
  "eventName": "PaymentSucceeded",
  "timestamp": "2025-10-29T10:00:30Z",
  "data": {
    "paymentId": "9fb52c81-7932-4534-b4e2-2g85c9d0f234",
    "bookingId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "amount": 500000,
    "status": "SUCCESS"
  }
}
```

### PaymentFailed (Future Enhancement)
**Publisher:** PaymentService  
**Consumers:** BookingService, NotificationService  
**Queue:** `payment_failed`

```json
{
  "eventId": "guid",
  "eventName": "PaymentFailed",
  "timestamp": "2025-10-29T10:00:30Z",
  "data": {
    "paymentId": "guid",
    "bookingId": "guid",
    "amount": 500000,
    "reason": "Insufficient funds",
    "status": "FAILED"
  }
}
```

---

## 🗓️ Development Phases

### Phase 1: Foundation Setup (Week 1-2)
- [x] Create solution structure with 4 projects (UserService, BookingService, PaymentService, ApiGateway)
- [ ] Setup Docker Compose with RabbitMQ, PostgreSQL, MongoDB
- [ ] Create Shared library project for:
  - Event contracts (DTOs)
  - RabbitMQ wrapper/helper classes
  - Common utilities and base classes
- [ ] Verify all containers running and accessible
- [ ] Basic health check endpoints for each service

### Phase 2: Core Services Implementation (Week 3-4)
- [ ] **UserService:**
  - Database context and User entity
  - Register endpoint (POST /api/users/register)
  - Login endpoint with JWT generation (POST /api/users/login)
  - Password hashing (bcrypt)
- [ ] **BookingService:**
  - Database context and Booking entity
  - Create booking endpoint (POST /api/bookings)
  - Get booking by ID (GET /api/bookings/{id})
  - RabbitMQ publisher setup
- [ ] **PaymentService:**
  - MongoDB setup and Payment model
  - Process payment endpoint (POST /api/payment/pay)
  - RabbitMQ publisher for PaymentSucceeded event
  - Mock payment processing logic

### Phase 3: Event-Driven Integration (Week 5)
- [ ] Implement RabbitMQ consumer in BookingService
- [ ] Listen for PaymentSucceeded events
- [ ] Update booking status to CONFIRMED when payment succeeds
- [ ] Test event flow: Create Booking → Process Payment → Update Booking
- [ ] Add retry logic with Polly for failed events

### Phase 4: API Gateway & Security (Week 6)
- [ ] Setup Ocelot/YARP API Gateway
- [ ] Configure routing rules for all services
- [ ] Implement JWT authentication middleware
- [ ] Add rate limiting (optional)
- [ ] Test all endpoints through gateway

### Phase 5: Observability & Polish (Week 7+)
- [ ] Integrate Serilog for structured logging
- [ ] Add Seq for log aggregation (optional)
- [ ] Implement global exception handling middleware
- [ ] Add Swagger/OpenAPI documentation for each service
- [ ] Create basic integration tests
- [ ] Document API endpoints with examples

### Phase 6: Advanced Features (Future)
- [ ] Implement Outbox Pattern for reliable event publishing
- [ ] Add Notification Service for emails
- [ ] Implement Saga Pattern for complex workflows
- [ ] Add Prometheus + Grafana for monitoring
- [ ] Circuit breaker patterns
- [ ] Distributed tracing (OpenTelemetry)

---

## 🧱 Future Extensions

Add Notification Service (send email when PaymentSucceeded).

Implement Outbox Pattern in Booking Service to ensure events are not lost during DB rollback.

Add Monitoring (Prometheus + Grafana) to view latency and throughput.

Add Saga Pattern for more complex workflow orchestration (e.g., refund when PaymentFail).

## 🎬 Scenario: User creates booking and system processes payment

```
Client          API Gateway        Booking Service       RabbitMQ        Payment Service
  │                   │                   │                  │                   │
  │  POST /booking    │                   │                  │                   │
  ├──────────────────>│                   │                  │                   │
  │                   │   Route request   │                  │                   │
  │                   ├──────────────────>│                  │                   │
  │                   │                   │  Save booking    │                   │
  │                   │                   ├─────────────────>│                   │
  │                   │                   │  (DB: BookingId=123, Status=Pending) │
  │                   │                   │                  │                   │
  │                   │                   │  Publish event:  │                   │
  │                   │                   ├─────────────────>│  "BookingCreated" │
  │                   │                   │                  │                   │
  │                   │                   │                  │   Deliver event   │
  │                   │                   │                  ├──────────────────>│
  │                   │                   │                  │                   │
  │                   │                   │                  │  Handle event     │
  │                   │                   │                  │  (process payment)│
  │                   │                   │                  │  Save Payment Info│
  │                   │                   │                  │  Emit: "PaymentSucceeded" │
  │                   │                   │                  │  <──────────────────┤
  │                   │                   │  Consume event: "PaymentSucceeded"    │
  │                   │                   │  Update booking: Status = Paid        │
  │                   │                   │  (DB update done)                     │
  │                   │                   │                  │                   │
  │  Response 201     │                   │                  │                   │
  │  Booking created  │<──────────────────┤                  │                   │

```

## 🧩 Architecture
```
                   ┌──────────────┐
                   │   ApiGateway │ (Ocelot)
                   └──────┬───────┘
                          │
     ┌────────────────────┼────────────────────┐
     │                    │                    │
┌────▼────┐         ┌─────▼────┐          ┌────▼────┐
│UserSvc  │         │BookingSvc│          │PaymentSvc│
│(Auth,DB)│         │(Consumer)│◄─────────│(Producer)│
└────┬────┘         └─────┬────┘          └────┬────┘
     │                    │                    │
     └─────────────┬──────┴────────────────────┘
                   │
              ┌────▼────┐
              │RabbitMQ │ (Event Bus)
              └─────────┘
```
## Data Model
```
User {
  Guid Id;
  string Name;
  string Email;
  string PasswordHash;
  DateTime CreatedAt;
}

Booking {
  Guid Id;
  string UserId;
  string RoomId;
  decimal Amount;
  string Status; // PENDING, CONFIRMED
  DateTime CreatedAt;
}

Payment {
  Guid Id;
  Guid BookingId;
  decimal Amount;
  string Status; // PENDING, SUCCESS
  DateTime CreatedAt;
}
```

## Business Flow
🧩 Flow: Booking → Payment → Update status

1. Client calls POST /booking/api/bookings through Gateway → BookingService creates order with Status=PENDING.

2. Client calls POST /payment/api/payment/pay → PaymentService processes successfully → publishes event:
```
{
  "eventName": "PaymentSucceeded",
  "data": {
    "bookingId": "xxx",
    "paymentId": "yyy",
    "amount": 500000
  }
}
```

3. RabbitMQ routes PaymentSucceeded event to payment_succeeded queue.

4. BookingService listens to this queue → receives event → updates Status = CONFIRMED.

## Communication Pattern
| Type             | Description                                           | Technology                |
| ---------------- | ----------------------------------------------------- | ------------------------- |
| **Synchronous**  | Client → API Gateway → REST API to services           | HTTP (YARP Reverse Proxy) |
| **Asynchronous** | Between BookingService and PaymentService             | RabbitMQ (event-driven)   |

## Project structure
```
API Gateway (Ocelot / YARP)
   ├── User Service          (ASP.NET Core + PostgreSQL)
   ├── Booking Service       (ASP.NET Core + PostgreSQL)
   ├── Payment Service       (ASP.NET Core + MongoDB)
   └── RabbitMQ (Event Bus)
```
```
BookingSystem/
│
├── src/
│   ├── UserService/
│   ├── BookingService/
│   ├── PaymentService/
│   ├── ApiGateway/
│   └── Shared/
│       ├── Contracts/        # Event DTOs
│       ├── EventBus/         # RabbitMQ wrapper
│       └── Common/           # Base classes, helpers
│
└── docker-compose.yml
```

---

## 📡 API Endpoints Reference

### User Service
**Base URL (via Gateway):** `/users/api`

| Method | Endpoint | Description | Request Body | Response |
|--------|----------|-------------|--------------|----------|
| POST | `/users/api/register` | Register new user | `{ "name": "string", "email": "string", "password": "string" }` | `201 Created` + User object |
| POST | `/users/api/login` | Login and get JWT | `{ "email": "string", "password": "string" }` | `200 OK` + `{ "token": "jwt-token" }` |
| GET | `/users/api/{id}` | Get user by ID | - | `200 OK` + User object |

### Booking Service
**Base URL (via Gateway):** `/booking/api`

| Method | Endpoint | Description | Request Body | Response |
|--------|----------|-------------|--------------|----------|
| POST | `/booking/api/bookings` | Create new booking | `{ "userId": "guid", "roomId": "string", "amount": 500000 }` | `201 Created` + Booking object |
| GET | `/booking/api/bookings/{id}` | Get booking by ID | - | `200 OK` + Booking object |
| GET | `/booking/api/bookings/user/{userId}` | Get all bookings for user | - | `200 OK` + Array of bookings |

### Payment Service
**Base URL (via Gateway):** `/payment/api`

| Method | Endpoint | Description | Request Body | Response |
|--------|----------|-------------|--------------|----------|
| POST | `/payment/api/payment/pay` | Process payment | `{ "bookingId": "guid", "amount": 500000 }` | `200 OK` + Payment object |
| GET | `/payment/api/payment/{id}` | Get payment by ID | - | `200 OK` + Payment object |

**Example Request/Response:**

**POST /booking/api/bookings**
```json
// Request
{
  "userId": "a3bb189e-8bf9-3888-9912-ace4e6543002",
  "roomId": "ROOM-101",
  "amount": 500000
}

// Response 201 Created
{
  "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "userId": "a3bb189e-8bf9-3888-9912-ace4e6543002",
  "roomId": "ROOM-101",
  "amount": 500000,
  "status": "PENDING",
  "createdAt": "2025-10-29T10:00:00Z"
}
```

---

## 📝 Technical Decisions Log

| Decision | Chosen | Alternatives Considered | Reason for Choice |
|----------|--------|------------------------|-------------------|
| **Event Bus** | RabbitMQ | Kafka, Azure Service Bus, Redis Streams | Easier learning curve, excellent Docker support, sufficient for demo scale |
| **API Gateway** | Ocelot/YARP | Kong, Nginx, Traefik | Native .NET integration, simpler configuration for .NET developers |
| **User Database** | PostgreSQL | SQL Server, MySQL | Free, production-ready, cross-platform |
| **Payment Database** | MongoDB | DynamoDB, Cassandra, CosmosDB | Learn polyglot persistence, flexible schema for payment logs |
| **Authentication** | JWT Bearer | OAuth2, IdentityServer, Auth0 | Simpler for learning project, less infrastructure needed |
| **Logging** | Serilog + Seq | ELK Stack, Splunk, Azure Monitor | Lightweight, easy setup, structured logging support |
| **Resilience** | Polly | Custom retry logic | Industry standard, well-tested, easy integration |

---

## 🎯 Non-Functional Requirements

### Performance
- API response time: < 500ms for synchronous calls
- Event processing: < 2 seconds end-to-end
- Gateway routing overhead: < 50ms

### Scalability
- Each service can run multiple instances (stateless)
- Horizontal scaling supported
- Database connection pooling enabled

### Security
- JWT tokens expire in 60 minutes
- Passwords hashed with bcrypt (cost factor: 10)
- HTTPS enforced on API Gateway
- Input validation on all endpoints
- SQL injection protection via EF Core parameterization

### Reliability
- Event retry policy: 3 attempts with exponential backoff
- Message durability: RabbitMQ persistent queues
- Database transactions for critical operations
- Graceful degradation if services are unavailable

### Observability
- Structured logging with correlation IDs
- Health check endpoints: `/health`
- Request/response logging in gateway
- Event tracking throughout the flow

**Note:** This is a learning project. Production systems would require more rigorous requirements around disaster recovery, SLAs, compliance, etc.

---


## 🧠 Key Concepts Learned

### Decoupling via Event-Driven Architecture
Services communicate asynchronously through messages.

### Service Autonomy
Each service has its own database and business logic.

### API Gateway Pattern
Centralized routing and security entry point.

### Fault Tolerance
If PaymentService fails, BookingService still runs independently.

## 📚 Next Steps

- Add UserService and InventoryService.

- Implement Saga Pattern for distributed transactions.

- Add Health Checks and Observability (Prometheus + Grafana).

## 📄 License

MIT © 2025 Hiền – Educational purpose only.