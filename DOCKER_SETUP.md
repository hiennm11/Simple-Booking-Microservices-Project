# Docker Infrastructure Setup Guide

## 📦 Services Overview

This Docker Compose configuration sets up all required infrastructure services for the Booking Microservices System.

### Services Included

| Service | Image | Ports | Purpose |
|---------|-------|-------|---------|
| **userdb** | postgres:16-alpine | 5432 | PostgreSQL database for UserService |
| **bookingdb** | postgres:16-alpine | 5433 | PostgreSQL database for BookingService |
| **paymentdb** | mongo:7.0 | 27017 | MongoDB database for PaymentService |
| **rabbitmq** | rabbitmq:3.12-management | 5672, 15672 | Message broker for event-driven communication |
| **seq** | datalust/seq:latest | 5341, 5342 | Structured logging and monitoring (optional) |

## 🚀 Quick Start

### Start All Services
```bash
docker-compose up -d
```

### Stop All Services
```bash
docker-compose down
```

### Stop and Remove Volumes (Clean Reset)
```bash
docker-compose down -v
```

### View Logs
```bash
# All services
docker-compose logs -f

# Specific service
docker-compose logs -f rabbitmq
docker-compose logs -f userdb
```

### Check Service Status
```bash
docker-compose ps
```

## 🔌 Connection Strings

### UserService - PostgreSQL
```
Host=localhost;Port=5432;Database=userdb;Username=userservice;Password=userservice123
```

**appsettings.json format:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=userdb;Username=userservice;Password=userservice123"
  }
}
```

### BookingService - PostgreSQL
```
Host=localhost;Port=5433;Database=bookingdb;Username=bookingservice;Password=bookingservice123
```

**appsettings.json format:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5433;Database=bookingdb;Username=bookingservice;Password=bookingservice123"
  }
}
```

### PaymentService - MongoDB
```
mongodb://paymentservice:paymentservice123@localhost:27017/paymentdb?authSource=admin
```

**appsettings.json format:**
```json
{
  "MongoDB": {
    "ConnectionString": "mongodb://paymentservice:paymentservice123@localhost:27017/paymentdb?authSource=admin",
    "DatabaseName": "paymentdb"
  }
}
```

### RabbitMQ
```
Host: localhost
Port: 5672
Username: guest
Password: guest
Management UI: http://localhost:15672
```

**appsettings.json format:**
```json
{
  "RabbitMQ": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest",
    "VirtualHost": "/"
  }
}
```

### Seq (Optional)
```
UI: http://localhost:5341
Ingestion: http://localhost:5342
```

## 🔍 Accessing Services

### RabbitMQ Management UI
- **URL:** http://localhost:15672
- **Username:** guest
- **Password:** guest
- **Features:**
  - View queues and exchanges
  - Monitor message rates
  - Manage connections
  - View queue bindings

### Seq Logging UI
- **URL:** http://localhost:5341
- **Features:**
  - Structured log viewing
  - Log filtering and searching
  - Real-time log streaming
  - Log retention policies

### PostgreSQL (UserDB)
```bash
# Using psql
psql -h localhost -p 5432 -U userservice -d userdb

# Using Docker exec
docker exec -it userdb psql -U userservice -d userdb
```

### PostgreSQL (BookingDB)
```bash
# Using psql
psql -h localhost -p 5433 -U bookingservice -d bookingdb

# Using Docker exec
docker exec -it bookingdb psql -U bookingservice -d bookingdb
```

### MongoDB (PaymentDB)
```bash
# Using mongosh
mongosh "mongodb://paymentservice:paymentservice123@localhost:27017/paymentdb?authSource=admin"

# Using Docker exec
docker exec -it paymentdb mongosh -u paymentservice -p paymentservice123 --authenticationDatabase admin
```

## 🏥 Health Checks

All services include health checks to ensure they're running properly.

### Check Health Status
```bash
docker-compose ps
```

Look for "healthy" status for each service.

### Individual Health Checks

**PostgreSQL:**
```bash
docker exec userdb pg_isready -U userservice -d userdb
docker exec bookingdb pg_isready -U bookingservice -d bookingdb
```

**MongoDB:**
```bash
docker exec paymentdb mongosh --eval "db.adminCommand('ping')"
```

**RabbitMQ:**
```bash
docker exec rabbitmq rabbitmq-diagnostics ping
```

## 📊 Monitoring Commands

### View Resource Usage
```bash
docker stats
```

### View Container Details
```bash
docker-compose ps -a
```

### View Networks
```bash
docker network ls
docker network inspect simple-booking-microservices-project_booking-network
```

### View Volumes
```bash
docker volume ls
```

## 🔧 Troubleshooting

### Service Won't Start
1. Check logs: `docker-compose logs [service-name]`
2. Verify ports aren't already in use
3. Ensure Docker daemon is running
4. Check Docker resource limits (memory, CPU)

### Connection Refused Errors
1. Verify service is healthy: `docker-compose ps`
2. Check if ports are exposed correctly
3. Ensure firewall isn't blocking ports
4. Wait for health checks to pass

### Database Connection Issues
1. Verify credentials in connection string
2. Check if database is initialized
3. Ensure database service is healthy
4. Try connecting with CLI tools first

### RabbitMQ Issues
1. Check management UI is accessible
2. Verify guest/guest credentials
3. Check queue and exchange configuration
4. Review RabbitMQ logs: `docker-compose logs rabbitmq`

### Reset Everything
```bash
# Stop and remove all containers, networks, and volumes
docker-compose down -v

# Remove dangling images
docker image prune -f

# Start fresh
docker-compose up -d
```

## 🔐 Security Notes

⚠️ **IMPORTANT:** These configurations are for **DEVELOPMENT ONLY**

For production, you should:
- Use strong, unique passwords
- Enable SSL/TLS for all connections
- Use secrets management (Docker secrets, Azure Key Vault, etc.)
- Restrict network access
- Enable authentication and authorization
- Use read-only volumes where possible
- Implement backup strategies
- Enable audit logging

## 🎯 Next Steps

After starting the infrastructure:

1. ✅ Verify all services are healthy
2. ✅ Test database connections
3. ✅ Access RabbitMQ Management UI
4. ✅ Configure application connection strings
5. ✅ Run database migrations
6. ✅ Test event publishing/consuming

## 📝 Common Operations

### Recreate a Single Service
```bash
docker-compose up -d --force-recreate rabbitmq
```

### Update Service Image
```bash
docker-compose pull
docker-compose up -d
```

### Backup Databases

**PostgreSQL:**
```bash
docker exec userdb pg_dump -U userservice userdb > backup_userdb.sql
docker exec bookingdb pg_dump -U bookingservice bookingdb > backup_bookingdb.sql
```

**MongoDB:**
```bash
docker exec paymentdb mongodump --username paymentservice --password paymentservice123 --authenticationDatabase admin --out /backup
```

### Restore Databases

**PostgreSQL:**
```bash
docker exec -i userdb psql -U userservice userdb < backup_userdb.sql
docker exec -i bookingdb psql -U bookingservice bookingdb < backup_bookingdb.sql
```

**MongoDB:**
```bash
docker exec paymentdb mongorestore --username paymentservice --password paymentservice123 --authenticationDatabase admin /backup
```

## 🌐 Network Configuration

All services are connected via the `booking-network` bridge network, allowing them to communicate using service names as hostnames.

**Example:** BookingService can connect to RabbitMQ using `rabbitmq` as the hostname instead of `localhost`.

For containerized services, update connection strings:
- Database Host: Use service name (e.g., `userdb`, `bookingdb`, `paymentdb`)
- RabbitMQ Host: Use `rabbitmq`

## 📦 Volume Management

Persistent data is stored in named volumes:
- `userdb_data` - UserService database
- `bookingdb_data` - BookingService database
- `paymentdb_data` - PaymentService database
- `rabbitmq_data` - RabbitMQ data
- `seq_data` - Seq logs

### List Volumes
```bash
docker volume ls
```

### Inspect Volume
```bash
docker volume inspect simple-booking-microservices-project_userdb_data
```

### Remove Unused Volumes
```bash
docker volume prune
```
