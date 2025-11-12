# ⚡ Inventory Service - Quick Reference

**Port**: 5004 | **Database**: PostgreSQL (port 5435) | **Pattern**: Event Choreography

---

## 🚀 Quick Start

```bash
# Start all services
docker-compose up --build

# Check health
curl http://localhost:5004/health

# View logs
docker logs inventoryservice -f
```

---

## 📡 API Endpoints

### Browse Inventory (Public)

```bash
# Get all inventory
curl http://localhost:5000/api/inventory

# Get specific room
curl http://localhost:5000/api/inventory/ROOM-101

# Check availability
curl -X POST http://localhost:5000/api/inventory/check-availability \
  -H "Content-Type: application/json" \
  -d '{"itemId": "ROOM-101", "quantity": 1}'
```

### Reserve/Release (Protected - JWT Required)

```bash
# Reserve inventory
curl -X POST http://localhost:5000/api/inventory/reserve \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"bookingId": "...","itemId": "ROOM-101","quantity": 1}'

# Release inventory
curl -X POST http://localhost:5000/api/inventory/release \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"bookingId": "...","reason": "Payment failed"}'
```

---

## 🔄 Event Flow

### BookingCreatedEvent → Reserve Inventory

**Queue**: `booking_created`

**Handler**: `BookingCreatedEventHandler`

**Actions**:
1. Check existing reservation (idempotent)
2. Verify availability
3. Reserve inventory (RESERVED)
4. Publish `InventoryReservedEvent`

### PaymentFailedEvent → Release Inventory (Compensate)

**Queue**: `payment_failed`

**Handler**: `PaymentFailedEventHandler`

**Actions**:
1. Find reservation
2. Release inventory (RELEASED)
3. Restore quantities
4. Publish `InventoryReleasedEvent`

### PaymentSucceededEvent → Confirm Reservation

**Queue**: `payment_succeeded`

**Handler**: `PaymentSucceededEventHandler`

**Actions**:
1. Find reservation
2. Update status (CONFIRMED)

---

## 🗄️ Database

### Tables

**InventoryItems**: Tracks room availability
- ItemId (unique): "ROOM-101"
- TotalQuantity: 1
- AvailableQuantity: 1
- ReservedQuantity: 0

**InventoryReservations**: Tracks booking reservations
- BookingId (unique)
- Status: RESERVED, CONFIRMED, RELEASED
- ExpiresAt: 15 minutes

### Seeded Data

- ROOM-101: Deluxe Room 101
- ROOM-102: Standard Room 102
- ROOM-201: Suite Room 201
- ROOM-202: Standard Room 202

---

## 📊 Monitoring

### Seq Queries

```
# All inventory logs
Service = "InventoryService"

# Trace booking
CorrelationId = "abc-123" | order by @Timestamp

# Errors
Service = "InventoryService" and @Level = "Error"

# Reservations
@MessageTemplate contains "reserved inventory"
```

**Seq URL**: http://localhost:5341

---

## 🎓 Key Patterns

### Event Choreography
Services react independently to events without a central coordinator.

### Compensating Actions (Saga)
PaymentFailed → Release inventory (undo reservation).

### Idempotent Handlers
Check if reservation exists before creating (safe to retry).

### Eventual Consistency
System temporarily inconsistent, eventually consistent.

---

## 🛠️ Configuration

### Environment Variables (.env)

```bash
INVENTORYDB_HOST=inventorydb
INVENTORYDB_PORT=5435
INVENTORYDB_NAME=inventorydb
INVENTORYDB_USER=inventoryservice
INVENTORYDB_PASSWORD=InventorySvc@2025!SecurePass
INVENTORYSERVICE_PORT=5004
```

### Docker Compose Services

- `inventorydb`: PostgreSQL database
- `inventoryservice`: Inventory service container

---

## 🔍 Troubleshooting

### Service won't start

```bash
# Check database
docker logs inventorydb

# Check RabbitMQ
docker logs rabbitmq

# Check service logs
docker logs inventoryservice
```

### Events not processing

```bash
# Check RabbitMQ queues
open http://localhost:15672
# Login: bookinguser / RabbitMQ@2025!SecurePass
# Check booking_created queue

# Check Seq logs
open http://localhost:5341
```

### Inventory not releasing

Check PaymentFailedEventHandler logs in Seq:
```
Service = "InventoryService" and @MessageTemplate contains "PaymentFailed"
```

---

## 📚 Documentation

**Full Guide**: `/docs/phase3-event-integration/INVENTORY_SERVICE_IMPLEMENTATION.md`

**Event Choreography**: `/brief/02-communication/event-choreography.md`

**Service README**: `/src/InventoryService/README.md`

---

**Last Updated**: November 12, 2025 | **Status**: ✅ Production Ready
