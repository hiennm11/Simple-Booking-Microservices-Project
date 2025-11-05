# Docker Compose Environment Variables Update - Summary

## ✅ What Was Updated

### 1. **docker-compose.yml**
Updated environment variables for services to use proper ASP.NET Core configuration format:

#### UserService
```yaml
environment:
  JwtSettings__SecretKey: "${JWT_SECRET_KEY}"
  JwtSettings__Issuer: "${JWT_ISSUER}"
  JwtSettings__Audience: "${JWT_AUDIENCE}"
  JwtSettings__ExpirationMinutes: "${JWT_EXPIRY_MINUTES}"
```

**Changed from:** `Jwt__Key` → **To:** `JwtSettings__SecretKey` ✅

#### ApiGateway
```yaml
environment:
  JwtSettings__SecretKey: "${JWT_SECRET_KEY}"
  JwtSettings__Issuer: "${JWT_ISSUER}"
  JwtSettings__Audience: "${JWT_AUDIENCE}"
```

**Added:** All JWT configuration environment variables ✅

### 2. **.env.example (Docker Deployment)**
Updated for Docker Compose deployment:

**Key Changes:**
- Added `ASPNETCORE_ENVIRONMENT=Development`
- Updated hosts to use Docker service names:
  - `USERDB_HOST=userdb` (was `localhost`)
  - `BOOKINGDB_HOST=bookingdb` (was `localhost`)
  - `PAYMENTDB_HOST=paymentdb` (was `localhost`)
  - `RABBITMQ_HOST=rabbitmq` (was `localhost`)
  - `SEQ_URL=http://seq:80` (was `http://localhost:5341`)
- Added comments explaining Docker vs Local usage

### 3. **.env.local.example (Local Development)** - NEW
Created separate template for local development:

**Purpose:** When running services locally (not in Docker)
- Uses `localhost` for all database hosts
- Uses `http://localhost:5341` for Seq
- Same JWT configuration (must match Docker)

### 4. **ENV_CONFIGURATION_COMPLETE.md** - NEW
Created comprehensive environment configuration guide:

**Contents:**
- Complete variable reference
- Docker vs Local deployment instructions
- Configuration mapping examples
- Security best practices
- Troubleshooting guide

## 📋 Environment Variables Mapping

### How It Works

ASP.NET Core uses **double underscore (`__`)** to represent nested JSON configuration:

```bash
# Environment Variable
JwtSettings__SecretKey=MySecret

# Maps to appsettings.json
{
  "JwtSettings": {
    "SecretKey": "MySecret"
  }
}
```

### Complete Mapping for JWT

| Environment Variable | appsettings.json Path |
|---------------------|----------------------|
| `JwtSettings__SecretKey` | `JwtSettings.SecretKey` |
| `JwtSettings__Issuer` | `JwtSettings.Issuer` |
| `JwtSettings__Audience` | `JwtSettings.Audience` |
| `JwtSettings__ExpirationMinutes` | `JwtSettings.ExpirationMinutes` |

## 🚀 Usage

### For Docker Compose

```bash
# 1. Copy template
cp .env.example .env

# 2. (Optional) Edit .env with your values
# Default values work out of the box

# 3. Start all services
docker-compose up -d

# 4. Verify JWT settings
docker exec userservice env | grep JWT
docker exec apigateway env | grep JWT
```

### For Local Development

```bash
# 1. Start infrastructure only
docker-compose up -d userdb bookingdb paymentdb rabbitmq seq

# 2. Copy local template
cp .env.local.example .env

# 3. Run services locally
dotnet run --project src/ApiGateway
dotnet run --project src/UserService
dotnet run --project src/BookingService
dotnet run --project src/PaymentService
```

## 🔧 Key Environment Variables

### Critical for Authorization

These **MUST** be identical across UserService and ApiGateway:

```bash
JWT_SECRET_KEY=YourSuperSecretKeyForJWTTokenGeneration123!
JWT_ISSUER=BookingSystem.UserService
JWT_AUDIENCE=BookingSystem.Clients
JWT_EXPIRY_MINUTES=60
```

⚠️ **If these don't match:** Token validation will fail with 401 Unauthorized

### Database Connections

#### Docker (service names)
```bash
USERDB_HOST=userdb
BOOKINGDB_HOST=bookingdb
PAYMENTDB_HOST=paymentdb
RABBITMQ_HOST=rabbitmq
```

#### Local (localhost)
```bash
USERDB_HOST=localhost
BOOKINGDB_HOST=localhost
PAYMENTDB_HOST=localhost
RABBITMQ_HOST=localhost
```

## ✅ Verification

### Check Configuration Syntax
```bash
docker-compose config --quiet
# No output = valid configuration ✅
```

### Check Environment in Running Containers
```bash
# UserService JWT settings
docker exec userservice env | grep JwtSettings

# ApiGateway JWT settings
docker exec apigateway env | grep JwtSettings

# Should show identical values
```

### Test JWT Flow
```bash
# 1. Register user
curl -X POST http://localhost:5000/api/users/register \
  -H "Content-Type: application/json" \
  -d '{"username":"test","email":"test@test.com","password":"Test123!","firstName":"Test","lastName":"User"}'

# 2. Login and get token
curl -X POST http://localhost:5000/api/users/login \
  -H "Content-Type: application/json" \
  -d '{"username":"test","password":"Test123!"}'

# 3. Use token (once BookingService endpoints are implemented)
curl -X GET http://localhost:5000/api/bookings \
  -H "Authorization: Bearer <TOKEN_FROM_STEP_2>"
```

## 📁 Files Modified/Created

| File | Status | Purpose |
|------|--------|---------|
| `docker-compose.yml` | Modified | Added JWT env vars for UserService & ApiGateway |
| `.env.example` | Modified | Updated for Docker deployment with service names |
| `.env.local.example` | Created | Template for local development |
| `docs/ENV_CONFIGURATION_COMPLETE.md` | Created | Comprehensive env variable guide |

## 🔄 Next Steps

1. **Copy environment file**
   ```bash
   cp .env.example .env
   ```

2. **Start services**
   ```bash
   docker-compose up -d
   ```

3. **Verify configuration**
   ```bash
   docker exec userservice env | grep JwtSettings
   docker exec apigateway env | grep JwtSettings
   ```

4. **Test authentication**
   - See AUTHORIZATION_GUIDE.md for testing examples

## 🔒 Security Reminders

- ✅ `.env` is already in `.gitignore` - never commit it
- ⚠️ Change `JWT_SECRET_KEY` in production (min 32 chars)
- ⚠️ Use different keys for Dev/Staging/Production
- ⚠️ Consider shorter token expiry in production (15-30 min)
- ⚠️ Use Azure Key Vault / AWS Secrets Manager in production

## 📚 Related Documentation

- **[ENV_CONFIGURATION_COMPLETE.md](./ENV_CONFIGURATION_COMPLETE.md)** - Full environment guide
- **[AUTHORIZATION_GUIDE.md](./AUTHORIZATION_GUIDE.md)** - JWT configuration details
- **[DOCKER_SETUP.md](./DOCKER_SETUP.md)** - Docker deployment guide
- **[AUTHORIZATION_QUICK_REFERENCE.md](./AUTHORIZATION_QUICK_REFERENCE.md)** - Quick reference

## Summary

All environment variables are now properly configured for both Docker and local development:

✅ JWT settings added to UserService  
✅ JWT settings added to ApiGateway  
✅ Configuration format matches appsettings.json  
✅ Separate templates for Docker and local deployment  
✅ Docker Compose configuration validated  
✅ Comprehensive documentation created  

The authorization system is now fully configured and ready to use! 🎉
