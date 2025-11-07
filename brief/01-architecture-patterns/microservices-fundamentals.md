# 🏗️ Microservices Fundamentals

**Category**: Architecture Patterns  
**Difficulty**: Beginner to Intermediate  
**Implementation Status**: ✅ Complete in this project

---

## 📖 What Are Microservices?

**Microservices** is an architectural style where an application is composed of small, independent services that:
- Run in their own process
- Communicate through well-defined APIs
- Are organized around business capabilities
- Can be deployed independently
- Are owned by small teams

### Traditional Monolith vs Microservices

```
MONOLITH                          MICROSERVICES
┌─────────────────┐              ┌────────┐  ┌────────┐  ┌────────┐
│                 │              │ User   │  │Booking │  │Payment │
│  Single App     │              │Service │  │Service │  │Service │
│  - User         │              └────┬───┘  └───┬────┘  └───┬────┘
│  - Booking      │                   │          │           │
│  - Payment      │              ┌────┴──────────┴───────────┴────┐
│                 │              │        Message Bus / API        │
│  Single DB      │              └─────────────────────────────────┘
└────────┬────────┘              ┌────────┐  ┌────────┐  ┌────────┐
         │                       │ UserDB │  │BookingDB│ │PaymentDB│
    ┌────┴────┐                  └────────┘  └────────┘  └────────┘
    │    DB   │
    └─────────┘
```

---

## 🎯 Why Microservices?

### Benefits

| Benefit | Description | Example in This Project |
|---------|-------------|------------------------|
| **Independent Deployment** | Deploy services without affecting others | Update PaymentService without redeploying BookingService |
| **Technology Diversity** | Use different tech stacks per service | PostgreSQL for booking, MongoDB for payments |
| **Scalability** | Scale services independently | Scale PaymentService 3x during high payment volume |
| **Team Autonomy** | Small teams own complete services | Payment team owns entire payment lifecycle |
| **Resilience** | Failure in one service doesn't crash system | If PaymentService fails, BookingService still accepts bookings |
| **Faster Development** | Parallel development by multiple teams | 3 teams work on User, Booking, Payment simultaneously |

### Challenges

| Challenge | Description | How We Handle It |
|-----------|-------------|------------------|
| **Distributed System Complexity** | More moving parts to manage | Docker Compose, structured logging |
| **Network Latency** | Service-to-service calls over network | Async messaging, caching strategies |
| **Data Consistency** | No single database transaction | Outbox pattern, eventual consistency |
| **Testing Complexity** | Need integration tests across services | E2E test scripts, contract testing |
| **Operational Overhead** | More services to monitor | Centralized logging (Seq), health checks |
| **Deployment Complexity** | Coordinating multiple deployments | Container orchestration (Docker) |

---

## 🏛️ Key Principles (Design Guidelines)

### 1. Single Responsibility Principle
**Each service does ONE thing well**

✅ **Good Example (Our Project)**:
- `UserService` → Only handles user authentication & management
- `BookingService` → Only handles booking CRUD operations
- `PaymentService` → Only processes payments

❌ **Bad Example**:
- `UserBookingPaymentService` → Does everything (becomes a distributed monolith)

### 2. Loose Coupling
**Services are independent and know little about each other**

✅ **Good Example**:
- BookingService publishes `BookingCreated` event
- PaymentService subscribes to events (doesn't call BookingService directly)
- Services communicate through events, not direct HTTP calls

❌ **Bad Example**:
- PaymentService directly calls BookingService's internal database
- Services share the same database

### 3. High Cohesion
**Related functionality stays together**

✅ **Good Example**:
- All booking-related logic (create, update, list) in BookingService
- All payment processing in PaymentService

❌ **Bad Example**:
- Payment validation logic split between PaymentService and BookingService

### 4. Autonomous Services
**Services can be deployed and scaled independently**

✅ **Good Example**:
- Each service has its own database
- Services can be deployed without coordination
- Each service has its own Dockerfile

### 5. Observable Services
**Easy to monitor and debug**

✅ **Good Example in Our Project**:
- Structured logging with Serilog
- Correlation IDs track requests across services
- Health check endpoints (`/health`)
- Centralized log aggregation (Seq)

---

## 🔍 In This Project: Implementation Details

### Our Microservices Architecture

```
Client
  │
  ▼
┌─────────────────┐
│  API Gateway    │  ◄── Single entry point (Port 5000)
│   (Ocelot/YARP) │
└────────┬────────┘
         │
    ┌────┼────┬──────────┐
    │    │    │          │
    ▼    ▼    ▼          ▼
┌──────┐ ┌──────┐ ┌──────┐
│User  │ │Booking│ │Payment│
│Svc   │ │Svc   │ │Svc   │
│5001  │ │5002  │ │5003  │
└──┬───┘ └──┬───┘ └──┬───┘
   │        │        │
   ▼        ▼        ▼
┌──────┐ ┌──────┐ ┌──────┐
│PgSQL │ │PgSQL │ │Mongo │
│userdb│ │bookdb│ │paydb │
└──────┘ └──────┘ └──────┘
         │
         ▼
    ┌─────────┐
    │RabbitMQ │  ◄── Event bus for async communication
    └─────────┘
```

### Service Breakdown

#### 1. UserService (Port 5001)
**Responsibility**: User authentication and management

**API Endpoints**:
- `POST /api/users/register` - Register new user
- `POST /api/users/login` - Login and get JWT token
- `GET /api/users/{id}` - Get user details

**Database**: PostgreSQL (`userdb`)

**Technology Stack**:
- ASP.NET Core 8
- Entity Framework Core
- BCrypt for password hashing
- JWT for authentication

#### 2. BookingService (Port 5002)
**Responsibility**: Booking management and workflow

**API Endpoints**:
- `POST /api/bookings` - Create booking
- `GET /api/bookings/{id}` - Get booking by ID
- `GET /api/bookings/user/{userId}` - List user's bookings
- `PATCH /api/bookings/{id}/status` - Update booking status

**Database**: PostgreSQL (`bookingdb`)

**Event Publishing**: `BookingCreated` → RabbitMQ
**Event Consuming**: `PaymentSucceeded` from RabbitMQ → Update booking status

**Technology Stack**:
- ASP.NET Core 8
- Entity Framework Core
- RabbitMQ Client
- Polly for resilience

#### 3. PaymentService (Port 5003)
**Responsibility**: Payment processing

**API Endpoints**:
- `POST /api/payment/pay` - Process payment
- `GET /api/payment/{id}` - Get payment by ID
- `GET /api/payment/booking/{bookingId}` - Get payment for booking

**Database**: MongoDB (`paymentdb`)

**Event Consuming**: `BookingCreated` from RabbitMQ → Process payment
**Event Publishing**: `PaymentSucceeded` → RabbitMQ

**Technology Stack**:
- ASP.NET Core 8
- MongoDB.Driver
- RabbitMQ Client
- Polly for resilience

#### 4. API Gateway (Port 5000)
**Responsibility**: Single entry point, routing, authentication

**Features**:
- Routes requests to appropriate services
- JWT validation
- Rate limiting
- CORS handling
- Health check aggregation

**Technology Stack**:
- ASP.NET Core 8
- YARP (Yet Another Reverse Proxy)
- JWT Bearer authentication

---

## 🔄 Communication Patterns

### Synchronous Communication (Request/Response)
**Used for**: Direct client requests

```
Client → API Gateway → UserService → PostgreSQL
                    ↓
                Response (JWT token)
```

**Example**: User login
```bash
POST http://localhost:5000/users/api/login
{
  "email": "user@example.com",
  "password": "password123"
}

Response:
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

### Asynchronous Communication (Event-Driven)
**Used for**: Service-to-service communication

```
BookingService → RabbitMQ → PaymentService
(Publish event)  (Queue)    (Consume event)
```

**Example**: Booking created flow
```
1. Client creates booking
   POST /api/bookings → BookingService

2. BookingService:
   - Saves booking to DB (Status: PENDING)
   - Publishes BookingCreated event to RabbitMQ

3. PaymentService:
   - Consumes BookingCreated event
   - Processes payment
   - Publishes PaymentSucceeded event

4. BookingService:
   - Consumes PaymentSucceeded event
   - Updates booking status to CONFIRMED
```

---

## 💾 Database Per Service Pattern

**Rule**: Each microservice owns its database exclusively.

### In This Project

| Service | Database | Port | Reason |
|---------|----------|------|--------|
| UserService | PostgreSQL (userdb) | 5432 | ACID transactions for user accounts |
| BookingService | PostgreSQL (bookingdb) | 5433 | Relational data with foreign keys |
| PaymentService | MongoDB (paymentdb) | 27017 | Flexible schema for payment logs |

### Benefits
- ✅ Independent schema changes
- ✅ Technology diversity (polyglot persistence)
- ✅ No database-level coupling
- ✅ Scales databases independently

### Challenges
- ❌ No cross-service joins
- ❌ Distributed transactions required
- ❌ Data duplication needed
- ❌ Eventual consistency

**How we handle distributed transactions**: Outbox Pattern (Phase 6)

---

## 📊 Real-World Applications

### When to Use Microservices

✅ **Good Fit**:
- Large, complex applications
- Multiple teams working on different features
- Need to scale different parts independently
- Long-term project with evolving requirements
- Different technologies needed for different features

❌ **Not a Good Fit**:
- Small applications with simple workflows
- Single team of 2-3 developers
- Tight coupling between features
- Simple CRUD operations
- Limited operational capabilities

### Companies Using Microservices
- **Netflix**: 700+ microservices
- **Amazon**: Service-oriented architecture since 2001
- **Uber**: Scales services independently per region
- **Spotify**: Squad-based microservices
- **Airbnb**: Monolith → microservices migration

---

## 🎓 Key Takeaways

### Core Concepts
1. ✅ Microservices = small, independent, business-focused services
2. ✅ Each service has its own database (database per service)
3. ✅ Services communicate via APIs and events
4. ✅ Independent deployment and scaling
5. ✅ Increased complexity requires strong DevOps practices

### Trade-offs
| Monolith | Microservices |
|----------|---------------|
| Simple to develop initially | Complex distributed system |
| Easy to test | Complex integration testing |
| Easy to deploy | Requires orchestration |
| Scales as a whole | Scales parts independently |
| Single tech stack | Technology diversity |
| Consistent data | Eventual consistency |

### When Microservices Make Sense
- ✅ Application grows beyond single team
- ✅ Different scaling requirements for features
- ✅ Need to deploy features independently
- ✅ Organization supports DevOps culture
- ✅ Have infrastructure for monitoring/orchestration

---

## 🧪 Hands-On Exercise

### Test Your Understanding

1. **Identify Service Boundaries**
   - List all endpoints in your project
   - Group them by business capability
   - Verify each service has a single responsibility

2. **Trace a Request**
   - Follow the flow: Create Booking → Process Payment → Confirm Booking
   - Identify synchronous vs asynchronous steps
   - Note where failures could occur

3. **Analyze Independence**
   - Can you deploy BookingService without deploying PaymentService?
   - Can PaymentService scale to 3 instances while BookingService has 1?
   - What happens if RabbitMQ goes down?

---

## 📚 Further Reading

### Books
- **"Building Microservices"** by Sam Newman (2nd Edition)
  - Chapter 1: What are microservices?
  - Chapter 2: How to model services
  
- **"Microservices Patterns"** by Chris Richardson
  - Chapter 1: Escaping monolithic hell
  - Chapter 2: Decomposition strategies

### Online Resources
- [Martin Fowler - Microservices Guide](https://martinfowler.com/microservices/)
- [Microsoft - .NET Microservices Architecture](https://docs.microsoft.com/en-us/dotnet/architecture/microservices/)
- [Microservices.io](https://microservices.io/) - Pattern catalog by Chris Richardson

### Videos
- [What are Microservices? - AWS](https://www.youtube.com/watch?v=CZ3wIuvmHeM)
- [Microservices Explained - TechWorld with Nana](https://www.youtube.com/watch?v=rv4LlmLmVWk)

---

## ❓ Interview Questions

### Junior Level
1. What is a microservice?
2. What's the difference between monolith and microservices?
3. Why does each service have its own database?

### Mid Level
4. How do microservices communicate with each other?
5. What are the benefits and challenges of microservices?
6. Explain the API Gateway pattern.
7. How do you handle distributed transactions?

### Senior Level
8. How would you decompose a monolith into microservices?
9. Explain eventual consistency and when it's acceptable.
10. How do you ensure data consistency across services without distributed transactions?
11. What strategies would you use to migrate from monolith to microservices?

### Answers Reference
See: `/brief/01-architecture-patterns/interview-answers.md` (create this for interview prep)

---

## ✅ Implementation Checklist

Use this when building your own microservices:

- [ ] Define clear service boundaries (business capabilities)
- [ ] Each service has its own database
- [ ] Services communicate via APIs or events
- [ ] Implement API Gateway for client entry point
- [ ] Add authentication and authorization
- [ ] Implement health checks for each service
- [ ] Set up centralized logging
- [ ] Add correlation IDs for request tracing
- [ ] Implement retry and circuit breaker patterns
- [ ] Container all services (Docker)
- [ ] Document API contracts (OpenAPI/Swagger)
- [ ] Create deployment pipeline (CI/CD)

---

**Last Updated**: November 7, 2025  
**Project Reference**: `/src/` folder - All microservices  
**Related Documents**:
- [Event-Driven Architecture](./event-driven-architecture.md)
- [Database Per Service](./database-per-service.md)
- [API Gateway Pattern](./api-gateway-pattern.md)
