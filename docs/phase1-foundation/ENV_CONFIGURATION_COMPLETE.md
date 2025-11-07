# Environment Configuration Guide

## Overview

This project uses environment variables for configuration. There are two deployment scenarios:

1. **Docker Compose** - All services run in containers
2. **Local Development** - Services run locally, connecting to dockerized databases

## Environment Files

| File | Purpose | When to Use |
|------|---------|-------------|
| `.env.example` | Docker deployment template | Copy to `.env` for Docker Compose |
| `.env.local.example` | Local development template | Copy to `.env` for local development |
| `.env` | Active configuration | **Never commit to git** (in .gitignore) |

## Quick Setup

### For Docker Compose Deployment

```bash
# Copy the Docker template
cp .env.example .env

# Edit .env if needed (default values work)
# Then start all services
docker-compose up -d
```

### For Local Development

```bash
# 1. Start infrastructure only (databases, RabbitMQ, Seq)
docker-compose up -d userdb bookingdb paymentdb rabbitmq seq

# 2. Copy local development template
cp .env.local.example .env

# 3. Run services locally
dotnet run --project src/ApiGateway
dotnet run --project src/UserService
dotnet run --project src/BookingService
dotnet run --project src/PaymentService
```

## Environment Variables Reference

### Core Settings

| Variable | Description | Default (Docker) | Default (Local) |
|----------|-------------|------------------|-----------------|
| `ASPNETCORE_ENVIRONMENT` | ASP.NET Core environment | `Development` | `Development` |

### UserService Database (PostgreSQL)

| Variable | Description | Docker | Local |
|----------|-------------|--------|-------|
| `USERDB_HOST` | Database host | `userdb` (service name) | `localhost` |
| `USERDB_PORT` | Database port | `5432` | `5432` |
| `USERDB_NAME` | Database name | `userdb` | `userdb` |
| `USERDB_USER` | Database user | `userservice` | `userservice` |
| `USERDB_PASSWORD` | Database password | `userservice123` | `userservice123` |

**Connection String Format:**
```
Host={USERDB_HOST};Port=5432;Database={USERDB_NAME};Username={USERDB_USER};Password={USERDB_PASSWORD}
```

### BookingService Database (PostgreSQL)

| Variable | Description | Docker | Local |
|----------|-------------|--------|-------|
| `BOOKINGDB_HOST` | Database host | `bookingdb` (service name) | `localhost` |
| `BOOKINGDB_PORT` | Database port | `5433` | `5433` |
| `BOOKINGDB_NAME` | Database name | `bookingdb` | `bookingdb` |
| `BOOKINGDB_USER` | Database user | `bookingservice` | `bookingservice` |
| `BOOKINGDB_PASSWORD` | Database password | `bookingservice123` | `bookingservice123` |

**Note:** Different port (5433) to avoid conflict with UserService DB

### PaymentService Database (MongoDB)

| Variable | Description | Docker | Local |
|----------|-------------|--------|-------|
| `PAYMENTDB_HOST` | Database host | `paymentdb` (service name) | `localhost` |
| `PAYMENTDB_PORT` | Database port | `27017` | `27017` |
| `PAYMENTDB_NAME` | Database name | `paymentdb` | `paymentdb` |
| `PAYMENTDB_USER` | Database user | `paymentservice` | `paymentservice` |
| `PAYMENTDB_PASSWORD` | Database password | `paymentservice123` | `paymentservice123` |

**Connection String Format:**
```
mongodb://{PAYMENTDB_USER}:{PAYMENTDB_PASSWORD}@{PAYMENTDB_HOST}:27017/{PAYMENTDB_NAME}?authSource=admin
```

### RabbitMQ Message Broker

| Variable | Description | Docker | Local |
|----------|-------------|--------|-------|
| `RABBITMQ_HOST` | RabbitMQ host | `rabbitmq` (service name) | `localhost` |
| `RABBITMQ_PORT` | AMQP port | `5672` | `5672` |
| `RABBITMQ_USER` | RabbitMQ user | `guest` | `guest` |
| `RABBITMQ_PASSWORD` | RabbitMQ password | `guest` | `guest` |
| `RABBITMQ_VHOST` | Virtual host | `/` | `/` |

**Management UI:** http://localhost:15672 (guest/guest)

### JWT Authentication Settings

⚠️ **CRITICAL:** These values MUST be identical across UserService and ApiGateway!

| Variable | Description | Default | Production Notes |
|----------|-------------|---------|------------------|
| `JWT_SECRET_KEY` | Secret key for signing tokens | `YourSuperSecretKeyForJWTTokenGeneration123!` | ⚠️ Change in production! Min 32 chars |
| `JWT_ISSUER` | Token issuer | `BookingSystem.UserService` | Identity of token creator |
| `JWT_AUDIENCE` | Token audience | `BookingSystem.Clients` | Intended token recipients |
| `JWT_EXPIRY_MINUTES` | Token expiration | `60` | Consider shorter (15-30) in production |

**Usage:**
- **UserService** uses these to **generate** JWT tokens
- **ApiGateway** uses these to **validate** JWT tokens
- Must match exactly or tokens will be rejected

**Security Best Practices:**
```bash
# Development
JWT_SECRET_KEY=YourSuperSecretKeyForJWTTokenGeneration123!

# Production - Generate strong key:
JWT_SECRET_KEY=$(openssl rand -base64 48)
# Or use Azure Key Vault / AWS Secrets Manager
```

### Seq Logging (Optional)

| Variable | Description | Docker | Local |
|----------|-------------|--------|-------|
| `SEQ_URL` | Seq server URL | `http://seq:80` | `http://localhost:5341` |
| `SEQ_API_KEY` | API key for ingestion | (empty - not required) | (empty) |
| `SEQ_ADMIN_PASSWORD` | Admin password | `Admin@2025!SeqPass` | `Admin@2025!SeqPass` |

**Seq UI:** http://localhost:5341

### Service Ports

| Variable | Description | Default |
|----------|-------------|---------|
| `USERSERVICE_PORT` | UserService external port | `5001` |
| `BOOKINGSERVICE_PORT` | BookingService external port | `5002` |
| `PAYMENTSERVICE_PORT` | PaymentService external port | `5003` |
| `APIGATEWAY_PORT` | API Gateway external port | `5000` |

**Note:** In Docker, these map to external ports. Internal port is always `80`.

## Configuration in appsettings.json

ASP.NET Core reads environment variables using configuration providers. Variables are mapped using:

### Syntax

```bash
# Nested JSON structure uses double underscore
Section__SubSection__Property=Value

# Example:
JwtSettings__SecretKey=MySecret
# Maps to:
{
  "JwtSettings": {
    "SecretKey": "MySecret"
  }
}
```

### UserService Configuration Mapping

**appsettings.json:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=${USERDB_HOST};Port=${USERDB_PORT};..."
  },
  "JwtSettings": {
    "SecretKey": "${JWT_SECRET_KEY}",
    "Issuer": "${JWT_ISSUER}",
    "Audience": "${JWT_AUDIENCE}",
    "ExpirationMinutes": "${JWT_EXPIRY_MINUTES}"
  }
}
```

**Environment Variables:**
```bash
ConnectionStrings__DefaultConnection=Host=userdb;Port=5432;...
JwtSettings__SecretKey=YourSecret
JwtSettings__Issuer=BookingSystem.UserService
JwtSettings__Audience=BookingSystem.Clients
JwtSettings__ExpirationMinutes=60
```

### API Gateway Configuration Mapping

**appsettings.json:**
```json
{
  "JwtSettings": {
    "SecretKey": "${JWT_SECRET_KEY}",
    "Issuer": "${JWT_ISSUER}",
    "Audience": "${JWT_AUDIENCE}"
  }
}
```

**Environment Variables:**
```bash
JwtSettings__SecretKey=YourSecret
JwtSettings__Issuer=BookingSystem.UserService
JwtSettings__Audience=BookingSystem.Clients
```

## Docker Compose Environment Variables

### How Docker Compose Uses .env

Docker Compose automatically loads `.env` file from the project root. Variables are substituted in `docker-compose.yml`:

```yaml
services:
  userservice:
    environment:
      JwtSettings__SecretKey: "${JWT_SECRET_KEY}"
      JwtSettings__Issuer: "${JWT_ISSUER}"
```

### Service-Specific Overrides

You can override per service in `docker-compose.override.yml`:

```yaml
services:
  userservice:
    environment:
      JwtSettings__ExpirationMinutes: "30"  # Override from 60 to 30
```

## Environment-Specific Configurations

### Development

```bash
ASPNETCORE_ENVIRONMENT=Development
JWT_EXPIRY_MINUTES=60
SEQ_URL=http://localhost:5341
```

**Features:**
- Longer token expiration (60 min)
- Detailed logging
- Exception details in responses
- Swagger UI enabled

### Staging

```bash
ASPNETCORE_ENVIRONMENT=Staging
JWT_EXPIRY_MINUTES=30
JWT_SECRET_KEY=<staging-specific-key>
```

**Features:**
- Moderate token expiration (30 min)
- Staging database connections
- Limited error details

### Production

```bash
ASPNETCORE_ENVIRONMENT=Production
JWT_EXPIRY_MINUTES=15
JWT_SECRET_KEY=<from-key-vault>
ASPNETCORE_URLS=https://+:443;http://+:80
```

**Features:**
- Short token expiration (15 min)
- Strong secret keys from vault
- HTTPS enforced
- Minimal error details
- Performance optimizations

## Security Best Practices

### ✅ DO

1. **Never commit `.env` to git**
   ```bash
   # Already in .gitignore
   echo ".env" >> .gitignore
   ```

2. **Use strong secret keys in production**
   ```bash
   # Generate strong key
   openssl rand -base64 48
   ```

3. **Different keys per environment**
   ```
   Development: Dev-specific key
   Staging: Staging-specific key
   Production: Production key from vault
   ```

4. **Use secret management in production**
   - Azure Key Vault
   - AWS Secrets Manager
   - HashiCorp Vault

5. **Rotate secrets regularly**
   - JWT keys every 90 days
   - Database passwords every 90 days

### ❌ DON'T

1. **Don't hardcode secrets in code**
2. **Don't commit `.env` to version control**
3. **Don't use default passwords in production**
4. **Don't share secrets via email/chat**
5. **Don't use same secret across environments**

## Troubleshooting

### JWT Token Validation Fails

**Error:** 401 Unauthorized

**Cause:** Secret key mismatch between UserService and ApiGateway

**Solution:**
```bash
# Check both services have same JWT settings
docker exec userservice env | grep JWT
docker exec apigateway env | grep JWT

# Should output identical values
```

### Database Connection Fails

**Error:** "Could not connect to database"

**Cause 1:** Wrong host name

```bash
# For Docker, use service name:
USERDB_HOST=userdb  # ✅ Correct

# Not localhost:
USERDB_HOST=localhost  # ❌ Wrong in Docker
```

**Cause 2:** Database not ready

```bash
# Check database health
docker ps
# Look for "healthy" status

# Check logs
docker logs userdb
```

### Service Can't Find Environment Variable

**Error:** "Configuration value not found"

**Solution:**
```bash
# 1. Check variable is in .env
cat .env | grep JWT_SECRET_KEY

# 2. Restart Docker Compose to reload .env
docker-compose down
docker-compose up -d

# 3. Verify variable is set in container
docker exec userservice env | grep JWT_SECRET_KEY
```

## Examples

### Full .env for Docker

```bash
# .env
ASPNETCORE_ENVIRONMENT=Development

USERDB_HOST=userdb
USERDB_PORT=5432
USERDB_NAME=userdb
USERDB_USER=userservice
USERDB_PASSWORD=userservice123

BOOKINGDB_HOST=bookingdb
BOOKINGDB_PORT=5433
BOOKINGDB_NAME=bookingdb
BOOKINGDB_USER=bookingservice
BOOKINGDB_PASSWORD=bookingservice123

PAYMENTDB_HOST=paymentdb
PAYMENTDB_PORT=27017
PAYMENTDB_NAME=paymentdb
PAYMENTDB_USER=paymentservice
PAYMENTDB_PASSWORD=paymentservice123

RABBITMQ_HOST=rabbitmq
RABBITMQ_PORT=5672
RABBITMQ_USER=guest
RABBITMQ_PASSWORD=guest
RABBITMQ_VHOST=/

JWT_SECRET_KEY=YourSuperSecretKeyForJWTTokenGeneration123!
JWT_ISSUER=BookingSystem.UserService
JWT_AUDIENCE=BookingSystem.Clients
JWT_EXPIRY_MINUTES=60

SEQ_URL=http://seq:80
SEQ_API_KEY=
SEQ_ADMIN_PASSWORD=Admin@2025!SeqPass

USERSERVICE_PORT=5001
BOOKINGSERVICE_PORT=5002
PAYMENTSERVICE_PORT=5003
APIGATEWAY_PORT=5000
```

### Full .env for Local Development

```bash
# .env (for local development)
ASPNETCORE_ENVIRONMENT=Development

USERDB_HOST=localhost
USERDB_PORT=5432
USERDB_NAME=userdb
USERDB_USER=userservice
USERDB_PASSWORD=userservice123

BOOKINGDB_HOST=localhost
BOOKINGDB_PORT=5433
BOOKINGDB_NAME=bookingdb
BOOKINGDB_USER=bookingservice
BOOKINGDB_PASSWORD=bookingservice123

PAYMENTDB_HOST=localhost
PAYMENTDB_PORT=27017
PAYMENTDB_NAME=paymentdb
PAYMENTDB_USER=paymentservice
PAYMENTDB_PASSWORD=paymentservice123

RABBITMQ_HOST=localhost
RABBITMQ_PORT=5672
RABBITMQ_USER=guest
RABBITMQ_PASSWORD=guest
RABBITMQ_VHOST=/

JWT_SECRET_KEY=YourSuperSecretKeyForJWTTokenGeneration123!
JWT_ISSUER=BookingSystem.UserService
JWT_AUDIENCE=BookingSystem.Clients
JWT_EXPIRY_MINUTES=60

SEQ_URL=http://localhost:5341
SEQ_API_KEY=
SEQ_ADMIN_PASSWORD=Admin@2025!SeqPass

USERSERVICE_PORT=5001
BOOKINGSERVICE_PORT=5002
PAYMENTSERVICE_PORT=5003
APIGATEWAY_PORT=5000
```

## Verification

### Verify Configuration

```bash
# 1. Check .env exists
ls -la .env

# 2. Verify no syntax errors
docker-compose config

# 3. Check environment variables in running containers
docker exec userservice env | grep JWT
docker exec apigateway env | grep JWT

# 4. Test JWT token generation and validation
# See AUTHORIZATION_GUIDE.md for testing examples
```

## Additional Resources

- **[AUTHORIZATION_GUIDE.md](./AUTHORIZATION_GUIDE.md)** - JWT configuration details
- **[DOCKER_SETUP.md](./DOCKER_SETUP.md)** - Docker deployment guide
- **[QUICKSTART.md](./QUICKSTART.md)** - Quick setup instructions
