# 📦 Inventory Service

**Service**: Inventory Management  
**Port**: 5004 (Docker: 80)  
**Database**: PostgreSQL (inventorydb)  
**Communication**: REST API + RabbitMQ Events

---

## 🎯 Purpose

The Inventory Service manages room/resource availability in the booking system. It:

- **Reserves inventory** when bookings are created
- **Releases inventory** when payments fail (compensating action)
- **Confirms reservations** when payments succeed
- **Tracks availability** in real-time
- **Prevents overbooking** with transaction-level locking

---

## 🏗️ Architecture

### Event Choreography Pattern

The Inventory Service implements the **event choreography pattern** by reacting independently to events:

```
BookingCreatedEvent → Reserve Inventory → InventoryReservedEvent
PaymentFailedEvent → Release Inventory → InventoryReleasedEvent (Compensating)
PaymentSucceededEvent → Confirm Reservation
```

### Domain Models

#### InventoryItem
- **ItemId**: Unique identifier (e.g., ROOM-101)
- **TotalQuantity**: Total available quantity
- **AvailableQuantity**: Currently available
- **ReservedQuantity**: Currently reserved

#### InventoryReservation
- **BookingId**: Reference to booking
- **Status**: RESERVED, CONFIRMED, RELEASED, EXPIRED
- **ExpiresAt**: Automatic expiration (15 minutes)
- **Quantity**: Reserved quantity

---

## 🚀 API Endpoints

### Public Endpoints (No Auth Required)

#### Get All Inventory
```http
GET /api/inventory
```

**Response**:
```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "itemId": "ROOM-101",
    "name": "Deluxe Room 101",
    "totalQuantity": 1,
    "availableQuantity": 1,
    "reservedQuantity": 0
  }
]
```

#### Get Inventory by ItemId
```http
GET /api/inventory/{itemId}
```

#### Check Availability
```http
POST /api/inventory/check-availability
Content-Type: application/json

{
  "itemId": "ROOM-101",
  "quantity": 1
}
```

**Response**:
```json
{
  "itemId": "ROOM-101",
  "name": "Deluxe Room 101",
  "totalQuantity": 1,
  "availableQuantity": 1,
  "reservedQuantity": 0,
  "isAvailable": true
}
```

### Protected Endpoints (JWT Required)

#### Reserve Inventory
```http
POST /api/inventory/reserve
Authorization: Bearer {token}
Content-Type: application/json

{
  "bookingId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "itemId": "ROOM-101",
  "quantity": 1
}
```

#### Release Inventory
```http
POST /api/inventory/release
Authorization: Bearer {token}
Content-Type: application/json

{
  "bookingId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "reason": "Payment failed"
}
```

---

## 📡 Event Handlers

### BookingCreatedEvent → Reserve Inventory

**Queue**: `booking_created`

**Action**:
1. Check if booking already has reservation (idempotent)
2. Find inventory item by ItemId
3. Verify availability
4. Create reservation (RESERVED status)
5. Update inventory quantities (atomic)
6. Publish `InventoryReservedEvent`

**Retry**: 3 attempts with exponential backoff

### PaymentFailedEvent → Release Inventory (Compensating)

**Queue**: `payment_failed`

**Action**:
1. Find active reservation for booking
2. Update status to RELEASED
3. Restore inventory quantities
4. Publish `InventoryReleasedEvent`

**Retry**: 3 attempts (critical compensating action)

### PaymentSucceededEvent → Confirm Reservation

**Queue**: `payment_succeeded`

**Action**:
1. Find reserved booking
2. Update status to CONFIRMED
3. Log confirmation

**Retry**: Non-critical, failures logged only

---

## 🔄 Event Flow Example

### Successful Booking Flow

```
t=0ms    BookingCreatedEvent published
t=10ms   InventoryService: Reserve ROOM-101
         - Available: 1 → 0
         - Reserved: 0 → 1
         - Status: RESERVED
t=15ms   InventoryReservedEvent published
t=5000ms PaymentSucceededEvent published
t=5005ms InventoryService: Confirm reservation
         - Status: RESERVED → CONFIRMED
```

### Failed Booking Flow (Compensating)

```
t=0ms    BookingCreatedEvent published
t=10ms   InventoryService: Reserve ROOM-101
         - Available: 1 → 0
         - Reserved: 0 → 1
t=5000ms PaymentFailedEvent published
t=5005ms InventoryService: Release ROOM-101 (COMPENSATE)
         - Available: 0 → 1
         - Reserved: 1 → 0
         - Status: RELEASED
t=5010ms InventoryReleasedEvent published
```

---

## 🗄️ Database Schema

### InventoryItems Table

| Column | Type | Description |
|--------|------|-------------|
| Id | UUID | Primary key |
| ItemId | VARCHAR(50) | Unique item identifier (indexed) |
| Name | VARCHAR(200) | Item name |
| TotalQuantity | INT | Total quantity |
| AvailableQuantity | INT | Currently available |
| ReservedQuantity | INT | Currently reserved |

### InventoryReservations Table

| Column | Type | Description |
|--------|------|-------------|
| Id | UUID | Primary key |
| BookingId | UUID | Reference to booking (indexed) |
| InventoryItemId | UUID | Foreign key to InventoryItems |
| Quantity | INT | Reserved quantity |
| Status | VARCHAR(20) | RESERVED, CONFIRMED, RELEASED |
| ExpiresAt | TIMESTAMP | Reservation expiration |

---

## 🔐 Security

- **JWT Authentication**: Required for reserve/release operations
- **Public Access**: Inventory browsing and availability checks
- **Transaction Safety**: Database transactions for inventory updates

---

## 🛠️ Development

### Local Development

```bash
# Run with local database
dotnet run --project src/InventoryService

# Connection string (appsettings.Development.json)
Host=localhost;Port=5435;Database=inventorydb;Username=inventory_user;Password=inventory_pass
```

### Docker Development

```bash
# Build and run all services
docker-compose up --build

# Run only InventoryService
docker-compose up inventorydb rabbitmq inventoryservice
```

### Database Migrations

```bash
# Create migration
dotnet ef migrations add InitialCreate --project src/InventoryService

# Apply migrations (automatic on startup)
# Or manually:
dotnet ef database update --project src/InventoryService
```

---

## 📊 Monitoring

### Health Check

```bash
curl http://localhost:5004/health
```

### Logs (Seq)

Navigate to http://localhost:5341 and filter by:
- Service: `InventoryService`
- CorrelationId: Track event flows

---

## 🧪 Testing

### Manual Testing

1. **Check Availability**:
```bash
curl -X POST http://localhost:5004/api/inventory/check-availability \
  -H "Content-Type: application/json" \
  -d '{"itemId": "ROOM-101", "quantity": 1}'
```

2. **Get All Inventory**:
```bash
curl http://localhost:5004/api/inventory
```

---

## 🎓 Key Concepts Demonstrated

### Event Choreography
- Services react independently to events
- No central orchestrator
- Loose coupling between services

### Compensating Actions (Saga Pattern)
- PaymentFailed → Release inventory (undo reservation)
- Eventual consistency
- Idempotent event handlers

### Concurrency Control
- Database transactions for atomic updates
- Optimistic concurrency (UpdatedAt timestamp)
- Race condition prevention

### Resilience
- Exponential backoff retry
- Dead letter queue handling
- Connection recovery for RabbitMQ

---

## 📚 Related Documentation

- **Event Choreography**: `/brief/02-communication/event-choreography.md`
- **Saga Pattern**: `/docs/phase6-advanced/`
- **Distributed Systems**: `/brief/07-computer-science/distributed-systems-theory.md`

---

**Status**: ✅ Fully Implemented  
**Last Updated**: November 12, 2025
