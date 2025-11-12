# 🏗️ Simple Booking Microservices Project

A lightweight microservices-based demo built with **ASP.NET Core**, **RabbitMQ**, and **Ocelot API Gateway** to demonstrate core microservices concepts and communication patterns.

---

# Focus Strategy: Microservices vs Computer Science Foundations

## 1. Your Current Context

You are a **.NET developer with 4 years of experience**, mostly doing outsource work.  
Your goals include:

1. Advancing to **Senior / Architect** level.
2. Preparing to **change jobs in 2025**.
3. Planning to build a **microservice pet project**.
4. Wanting to gain a strong understanding of **Computer Science (CS)** without studying everything blindly.

---

## 2. Summary Decision

→ **Prioritize Microservices first, and learn CS selectively in parallel to support microservice development.**

This decision is based on three key reasons:

---

## 3. Why Prioritize Microservices Now

### 3.1 Microservices are Your Practical Goal

Working with microservices enables you to:

- **Advance professionally**, because senior .NET engineers must understand modularization, API communication, queues, caching, etc.
- **Prepare for job changes**, since many job descriptions require architecture and system design experience.
- **Apply what you learn immediately**, preventing knowledge from becoming theoretical and disconnected.

➡ Therefore, **focus on building a real microservice pet project** in the next **6–8 weeks**.

---

### 3.2 Computer Science Is the Foundation Behind Microservices

CS is still important — but you should learn it **to support what you are currently building**, rather than studying the entire curriculum.

| CS Area | What to Learn (for Microservices) | Real System Application |
|--------|----------------------------------|------------------------|
| **Data Structures & Algorithms** | hash tables, trees, queues, heaps, graphs | caching, routing, scheduling, queue processing |
| **Networking** | TCP, HTTP, sockets, DNS, load balancing | API design, reverse proxying, scaling |
| **Database Systems** | indexing, normalization, ACID, transactions | transactional consistency, event-driven data |
| **Operating Systems** | threads, processes, concurrency | async/await behavior, background workers, scaling |
| **Distributed Systems** | CAP theorem, consistency models, event sourcing | microservice communication & messaging design |

🎯 **Learning Method:**  
When building your microservice project:  
→ **Encounter a problem → learn the CS concept related to it.**

Examples:
- Need to scale an API → learn **load balancing & caching**
- Need background messaging → learn **concurrency & distributed messaging principles**

---

### 3.3 Balanced Strategy for the Next 3–6 Months

| Time Period | Primary Focus | Parallel CS Study |
|------------|--------------|------------------|
| **Month 1–2** | Build a basic microservice project (auth, product, order services) | Study foundational CS (HTTP, DB basics, basic DSA) |
| **Month 3–4** | Add queue, caching, logging, monitoring | Study system design & distributed systems |
| **Month 5–6** | Refactor, deploy, optimize performance | Study deeper CS (OS concurrency, networking inside) |

By the end of Month 6:
- You have a **real portfolio project** to show.
- You have **solid CS fundamentals** to understand deeper architecture.

---

## 4. Final Decision Summary

| Option | Priority | Reason |
|--------|----------|--------|
| **Microservices** | **Primary Focus (70%)** | Direct career impact + practical portfolio |
| **Computer Science** | **Study in Parallel (30%)** | Supports deeper understanding + long-term growth |

---

## 🚀 Quick Start

```cmd
REM 1. Start all services
docker-compose up -d

REM 2. Run complete system test
test-system.bat

REM 3. View logs and monitoring
REM - Seq Logs: http://localhost:5341
REM - RabbitMQ: http://localhost:15672
```

**For detailed testing and load testing**, see:
- [Testing Quick Start Guide](TESTING_QUICK_START.md) - Quick commands for testing
- [End-to-End Testing Guide](docs/E2E_TESTING_GUIDE.md) - Complete testing documentation

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

### Phase 1: Foundation Setup ✅ COMPLETED
- [x] Create solution structure with 4 projects (UserService, BookingService, PaymentService, ApiGateway)
- [x] Setup Docker Compose with RabbitMQ, PostgreSQL, MongoDB, Seq
- [x] Create Shared library project for:
  - Event contracts (DTOs)
  - RabbitMQ wrapper/helper classes
  - Common utilities and base classes
- [x] Verify all containers running and accessible
- [x] **Docker containerization of all 4 microservices**
- [x] **Multi-stage Dockerfiles for optimized builds**
- [x] **Environment configuration with .env file**
- [x] **Infrastructure services running and healthy**
- [x] Basic health check endpoints for each service

### Phase 2: Core Services Implementation ✅ COMPLETED
- [x] **UserService:**
  - [x] Database context and User entity (EF Core + PostgreSQL)
  - [x] Register endpoint (POST /api/users/register)
  - [x] Login endpoint with JWT generation (POST /api/users/login)
  - [x] Password hashing (BCrypt)
  - [x] Database seeding with test data
  - [x] EF Core migrations
- [x] **BookingService:**
  - [x] Database context and Booking entity (EF Core + PostgreSQL)
  - [x] Create booking endpoint (POST /api/bookings)
  - [x] Get booking by ID (GET /api/bookings/{id})
  - [x] List all bookings endpoint (GET /api/bookings)
  - [x] RabbitMQ publisher setup with Polly retry logic
  - [x] EF Core migrations
- [x] **PaymentService:**
  - [x] MongoDB setup and Payment model
  - [x] Process payment endpoint (POST /api/payments)
  - [x] Get payment by ID (GET /api/payments/{id})
  - [x] List all payments endpoint (GET /api/payments)
  - [x] RabbitMQ publisher for PaymentSucceeded event with Polly retry
  - [x] Mock payment processing logic (auto-success for demo)

### Phase 3: Event-Driven Integration ✅ COMPLETED
- [x] Implement RabbitMQ consumer in BookingService
- [x] Listen for PaymentSucceeded events
- [x] Update booking status to CONFIRMED when payment succeeds
- [x] Test event flow: Create Booking → Process Payment → Update Booking
- [x] **Add retry logic with Polly for failed events**
- [x] **Event publishing retry** (3 attempts with exponential backoff)
- [x] **Event consumption retry** (3 internal + 3 requeue = 9 total attempts)
- [x] **Connection resilience** (10 retry attempts for RabbitMQ connection)
- [x] **Dead Letter Queue (DLQ)** support for exhausted retries
- [x] **Correlation ID tracking** across services

### Phase 4: API Gateway & Security ✅ COMPLETED
- [x] Setup YARP API Gateway
- [x] Configure routing rules for all services
- [x] Health checks for downstream services
- [x] Request/response logging
- [x] CORS policy configuration
- [x] Environment-specific configuration (dev/prod)
- [x] Active health monitoring (10-second intervals)
- [x] JWT authentication middleware
  - [x] API Gateway JWT validation
  - [x] BookingService JWT authentication
  - [x] PaymentService JWT authentication
  - [x] Protected endpoints with [Authorize] attributes
  - [x] User claims forwarding to downstream services
- [x] **Rate limiting with multiple policies**
  - [x] Global rate limiting (100 requests/min per IP)
  - [x] Auth rate limiting (5 attempts/5min - brute force protection)
  - [x] Booking rate limiting (Token Bucket: 50 capacity, +10/min)
  - [x] Payment rate limiting (Concurrency: max 10 concurrent)
  - [x] Read operation limits (200 requests/min per user)
  - [x] Premium tier support (500 requests/min)
  - [x] Configurable via appsettings.json
  - [x] Comprehensive logging and monitoring
  - [x] Informative 429 responses with retry guidance

### Phase 5: Observability & Monitoring ✅ COMPLETED
- [x] Integrate Serilog for structured logging in all services
- [x] Add Seq for centralized log aggregation
- [x] Health check endpoints (/health) for all services
- [x] Service-specific log enrichment
- [x] **Comprehensive Seq query library** (29 production-ready queries)
- [x] **Pre-configured dashboard templates** (6 dashboards)
- [x] **Alert signals and thresholds** (8 critical alerts)
- [x] **Retry monitoring and observability**
- [x] **Correlation ID support** for distributed tracing
- [x] Complete documentation suite (3,000+ lines)
- [x] OpenAPI/Swagger documentation for each service
- [x] **Global exception handling middleware** across all services
- [ ] Integration tests (future enhancement)

### Phase 6: Advanced Features (Future)
- [x] **Implement Outbox Pattern for reliable event publishing** ✅ (November 7, 2025)
  - [x] Transactional outbox in BookingService (PostgreSQL)
  - [x] Transactional outbox in PaymentService (MongoDB)
  - [x] Guaranteed event delivery with database persistence
  - [x] Background publisher service with retry logic
  - [x] Comprehensive documentation and testing guides
  - [x] Quick reference guides for both services
- [ ] Add Notification Service for emails
- [ ] Implement Saga Pattern for complex workflows
- [ ] Add Prometheus + Grafana for monitoring
- [ ] Circuit breaker patterns
- [ ] Distributed tracing (OpenTelemetry)

---

## 🎯 Key Features Implemented

### 📦 Outbox Pattern (Both Services) ✅

**Reliable Event Publishing with Transactional Outbox**

Both **BookingService** and **PaymentService** now implement the **Transactional Outbox Pattern** to guarantee that events are never lost, even if RabbitMQ is temporarily unavailable.

**How it works:**
1. Events are saved to an outbox storage (PostgreSQL table for BookingService, MongoDB collection for PaymentService)
2. A background worker polls the outbox every 10 seconds and publishes unpublished events
3. Events are automatically retried on failure with exponential backoff
4. Full audit trail of all events in the database

**Implementation Details:**

| Service | Storage | Atomicity | Status |
|---------|---------|-----------|--------|
| **BookingService** | PostgreSQL `outbox_messages` table | ✅ Database transactions | ✅ Implemented |
| **PaymentService** | MongoDB `outbox_messages` collection | ⚠️ Best-effort (optional transactions) | ✅ Implemented |

**Benefits:**
- ✅ **Guaranteed delivery** - events never lost
- ✅ **Atomicity** - business data and events saved together (BookingService)
- ✅ **Resilience** - automatic retries when RabbitMQ is down
- ✅ **Audit trail** - complete history in database
- ✅ **Horizontal scaling** - multiple service instances supported

**Documentation:**
- Full guide: [`/docs/phase6-advanced/OUTBOX_PATTERN_IMPLEMENTATION.md`](docs/phase6-advanced/OUTBOX_PATTERN_IMPLEMENTATION.md)
- BookingService quick reference: [`/src/BookingService/OUTBOX_PATTERN_QUICK_REFERENCE.md`](src/BookingService/OUTBOX_PATTERN_QUICK_REFERENCE.md)
- PaymentService quick reference: [`/src/PaymentService/OUTBOX_PATTERN_QUICK_REFERENCE.md`](src/PaymentService/OUTBOX_PATTERN_QUICK_REFERENCE.md)

---

## 🧱 Future Extensions

Add Notification Service (send email when PaymentSucceeded).

~~Implement Outbox Pattern in Booking Service to ensure events are not lost during DB rollback.~~ ✅ **COMPLETED** (November 7, 2025)

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
- JWT Bearer authentication on all protected endpoints
- API Gateway validates JWT tokens and forwards user claims
- BookingService and PaymentService require authentication
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

## 📚 Documentation & Learning Resources

### 📖 Complete System Documentation

**NEW: Complete Flow Documentation**:
- **[Complete System Flow](docs/COMPLETE_SYSTEM_FLOW.md)** - All scenarios, event flows, patterns
- **[Correlation ID Guide](docs/CORRELATION_ID_GUIDE.md)** - Testing and tracking with Seq

**Quickstart Guides**:
- **[Quick Start](docs/general/QUICKSTART.md)** - Get started in 5 minutes
- **[Testing Guide](docs/general/TESTING_QUICK_START.md)** - Run E2E tests
- **[Environment Configuration](docs/phase1-foundation/ENV_CONFIGURATION_COMPLETE.md)** - Setup .env files

**Phase Documentation**:
- **Phase 1**: Foundation & Docker setup
- **Phase 2**: Core services (Booking, Payment, User)
- **Phase 3**: Event integration & Saga pattern
- **Phase 4**: API Gateway & security
- **Phase 5**: Observability (Seq, correlation tracking)
- **Phase 6**: Advanced features (Outbox, DLQ, Inventory service)

### 🎓 Knowledge Brief - Comprehensive Learning Guide

A structured learning resource organized in the `/brief/` folder covering all concepts from this project:

**Quick Access**:
- **[Start Here: Quick Start Guide](brief/QUICK_START.md)** - How to use the brief
- **[8-Week Learning Roadmap](brief/LEARNING_ROADMAP.md)** - Complete study plan
- **[Architecture Patterns](brief/01-architecture-patterns/)** - Core concepts, patterns, trade-offs

**What's Included**:
- ✅ Microservices fundamentals with real project examples
- ✅ Event-driven architecture deep dive
- ✅ Outbox pattern implementation guide
- ✅ Saga pattern with compensating actions
- ✅ Correlation ID tracking and observability
- ✅ Interview preparation materials
- ✅ 8-week study plan (70% Microservices + 30% CS)

**Use Cases**:
1. **Learning from Scratch**: Follow the 8-week roadmap
2. **Interview Preparation**: 1-week focused review
3. **Concept Reference**: Quick lookup when needed
4. **Testing & Debugging**: Use correlation ID guide

**See**: `/brief/README.md` for full navigation guide

---

## 📚 Next Steps

### Phase 6 Continuation (Advanced Features)

- [x] ~~Implement Outbox Pattern for reliable event publishing~~ ✅ **COMPLETED** (November 7, 2025)
- [x] ~~Implement Saga Pattern with compensating actions~~ ✅ **COMPLETED** (November 12, 2025)
- [x] ~~Add Inventory Service for room availability management~~ ✅ **COMPLETED** (November 12, 2025)
- [x] ~~Implement Correlation ID tracking with Serilog enrichers~~ ✅ **COMPLETED** (November 12, 2025)
- [x] ~~Graceful error handling for business failures~~ ✅ **COMPLETED** (November 12, 2025)
- [ ] Add Notification Service for emails/SMS notifications
- [ ] Add Circuit Breaker pattern with Polly
- [ ] Implement Distributed Tracing with OpenTelemetry
- [ ] Add Prometheus + Grafana for advanced monitoring

### Deployment & Production

- [ ] Deploy to Azure/AWS cloud platform
- [ ] Set up CI/CD pipeline (GitHub Actions / Azure DevOps)
- [ ] Configure production environment
- [ ] Implement blue-green deployment
- [ ] Set up auto-scaling policies

### Learning Path

Follow the comprehensive learning guide in `/brief/`:
1. **Weeks 1-2**: Master architecture patterns
2. **Weeks 3-4**: Understand resilience & communication
3. **Weeks 5-6**: Security & API Gateway deep dive
4. **Week 7**: Observability & monitoring
5. **Week 8**: Interview preparation & portfolio polish

**Goal**: Job-ready in 8 weeks with senior-level understanding 🚀

---

## 📄 License

MIT © 2025 Hiền – Educational purpose only.