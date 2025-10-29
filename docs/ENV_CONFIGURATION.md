# Environment Configuration Guide

## Overview

This project uses environment variables to manage configuration across all services. This approach provides:
- ✅ Security: Sensitive data not hardcoded
- ✅ Flexibility: Easy to change per environment
- ✅ Consistency: Single source of truth

## Files Structure

```
.
├── .env                          # Your actual configuration (git-ignored)
├── .env.example                  # Template with example values
├── configure-appsettings.bat     # Script to generate appsettings files
├── docker-compose.yml            # Uses ${VARIABLE} syntax
└── src/
    ├── UserService/
    │   ├── appsettings.json      # Base configuration with ${VARIABLE} placeholders
    │   └── appsettings.Development.json  # Generated with actual values
    ├── BookingService/
    │   ├── appsettings.json
    │   └── appsettings.Development.json
    └── PaymentService/
        ├── appsettings.json
        └── appsettings.Development.json
```

## Setup Instructions

### 1. Create Your .env File

The `.env` file has been created with secure default values. You can customize it:

```bash
# Edit the .env file
notepad .env
```

### 2. Generate appsettings Files

Run the configuration script to generate `appsettings.Development.json` files:

```bash
.\configure-appsettings.bat
```

This will:
- Load variables from `.env`
- Generate `appsettings.Development.json` for each service
- Show you the configured values

### 3. Start Infrastructure

```bash
# Docker Compose will automatically load .env
docker-compose up -d
```

## Environment Variables Reference

### Database Configuration

#### UserService PostgreSQL
```env
USERDB_HOST=localhost
USERDB_PORT=5432
USERDB_NAME=userdb
USERDB_USER=userservice
USERDB_PASSWORD=UserSvc@2025!SecurePass
```

**Connection String Format:**
```
Host=${USERDB_HOST};Port=${USERDB_PORT};Database=${USERDB_NAME};Username=${USERDB_USER};Password=${USERDB_PASSWORD}
```

#### BookingService PostgreSQL
```env
BOOKINGDB_HOST=localhost
BOOKINGDB_PORT=5433
BOOKINGDB_NAME=bookingdb
BOOKINGDB_USER=bookingservice
BOOKINGDB_PASSWORD=BookingSvc@2025!SecurePass
```

#### PaymentService MongoDB
```env
PAYMENTDB_HOST=localhost
PAYMENTDB_PORT=27017
PAYMENTDB_NAME=paymentdb
PAYMENTDB_USER=paymentservice
PAYMENTDB_PASSWORD=PaymentSvc@2025!SecurePass
```

**Connection String Format:**
```
mongodb://${PAYMENTDB_USER}:${PAYMENTDB_PASSWORD}@${PAYMENTDB_HOST}:${PAYMENTDB_PORT}/${PAYMENTDB_NAME}?authSource=admin
```

### Message Broker Configuration

#### RabbitMQ
```env
RABBITMQ_HOST=localhost
RABBITMQ_PORT=5672
RABBITMQ_USER=bookinguser
RABBITMQ_PASSWORD=RabbitMQ@2025!SecurePass
RABBITMQ_VHOST=/
```

### Security Configuration

#### JWT Settings
```env
JWT_SECRET_KEY=B00k1ngSyst3m@S3cr3tK3y!2025#V3ryL0ngAndS3cur3P@ssw0rd
JWT_ISSUER=BookingSystem.UserService
JWT_AUDIENCE=BookingSystem.Clients
JWT_EXPIRY_MINUTES=60
```

⚠️ **IMPORTANT:** 
- JWT secret key should be at least 32 characters
- Use a strong, random key in production
- Never commit the actual key to version control

### Service Ports

```env
USERSERVICE_PORT=5001
BOOKINGSERVICE_PORT=5002
PAYMENTSERVICE_PORT=5003
APIGATEWAY_PORT=5000
```

### Logging Configuration

#### Seq (Optional)
```env
SEQ_URL=http://localhost:5341
SEQ_API_KEY=
```

### Environment Settings

```env
ASPNETCORE_ENVIRONMENT=Development
LOG_LEVEL=Information
```

## Current Configuration

Your `.env` file currently has these values:

| Variable | Value | Description |
|----------|-------|-------------|
| USERDB_PASSWORD | `UserSvc@2025!SecurePass` | UserService DB password |
| BOOKINGDB_PASSWORD | `BookingSvc@2025!SecurePass` | BookingService DB password |
| PAYMENTDB_PASSWORD | `PaymentSvc@2025!SecurePass` | PaymentService DB password |
| RABBITMQ_USER | `bookinguser` | RabbitMQ username |
| RABBITMQ_PASSWORD | `RabbitMQ@2025!SecurePass` | RabbitMQ password |
| JWT_SECRET_KEY | `B00k1ngSyst3m@S3cr3tK3y!...` | JWT signing key |

## How It Works

### Docker Compose
Docker Compose automatically loads `.env` file and substitutes variables:

```yaml
environment:
  POSTGRES_USER: ${USERDB_USER}
  POSTGRES_PASSWORD: ${USERDB_PASSWORD}
```

### ASP.NET Core Services
The `configure-appsettings.bat` script generates `appsettings.Development.json` with actual values:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=userdb;Username=userservice;Password=UserSvc@2025!SecurePass"
  }
}
```

## Best Practices

### Development
✅ Use `.env` file for local development
✅ Keep `.env` file git-ignored
✅ Share `.env.example` with the team
✅ Run `configure-appsettings.bat` after updating `.env`

### Production
✅ Use environment variables from hosting platform
✅ Use secrets management (Azure Key Vault, AWS Secrets Manager, etc.)
✅ Never commit production credentials
✅ Rotate credentials regularly
✅ Use different passwords for each service

## Updating Configuration

### Change a Password

1. Update `.env` file:
```env
USERDB_PASSWORD=NewSecurePassword123!
```

2. Regenerate appsettings:
```bash
.\configure-appsettings.bat
```

3. Restart infrastructure:
```bash
docker-compose down
docker-compose up -d
```

4. Restart services:
```bash
# Stop services (Ctrl+C)
# Start again
dotnet run --project src/UserService/UserService.csproj
```

### Change a Port

1. Update `.env` file:
```env
USERDB_PORT=5434
```

2. Regenerate appsettings:
```bash
.\configure-appsettings.bat
```

3. Restart infrastructure:
```bash
docker-compose down
docker-compose up -d
```

### Add New Variable

1. Add to `.env`:
```env
NEW_VARIABLE=value
```

2. Update `.env.example`:
```env
NEW_VARIABLE=example_value
```

3. Update relevant appsettings.json:
```json
{
  "NewSetting": "${NEW_VARIABLE}"
}
```

4. Update `configure-appsettings.bat` to include the new variable

5. Regenerate appsettings:
```bash
.\configure-appsettings.bat
```

## Troubleshooting

### Issue: Variables Not Loaded

**Symptoms:** Services fail to start, connection errors

**Solution:**
1. Verify `.env` file exists
2. Check variable names match exactly (case-sensitive)
3. Run `docker-compose config` to verify
4. Regenerate appsettings: `.\configure-appsettings.bat`

### Issue: Wrong Password in Database

**Symptoms:** Authentication failed, access denied

**Solution:**
1. Check `.env` file for correct password
2. Verify no extra spaces or quotes
3. Reset infrastructure: `.\reset-infrastructure.bat`
4. Regenerate appsettings: `.\configure-appsettings.bat`

### Issue: appsettings Not Updated

**Symptoms:** Old values still being used

**Solution:**
1. Run `.\configure-appsettings.bat` again
2. Check `appsettings.Development.json` files
3. Restart services
4. Verify `ASPNETCORE_ENVIRONMENT=Development`

## Security Checklist

- [ ] `.env` file is in `.gitignore`
- [ ] Strong passwords used (min 12 characters)
- [ ] Different passwords for each service
- [ ] JWT secret key is at least 32 characters
- [ ] No credentials in git history
- [ ] `.env.example` has no real credentials
- [ ] Team members have their own `.env` files

## Command Reference

```bash
# Create .env from template
copy .env.example .env

# Generate appsettings files
.\configure-appsettings.bat

# Validate Docker Compose config
docker-compose config

# Start infrastructure with env vars
docker-compose up -d

# View what Docker sees
docker-compose config | findstr PASSWORD

# Reset everything
.\reset-infrastructure.bat
.\configure-appsettings.bat
```

## Next Steps

1. ✅ Review your `.env` configuration
2. ✅ Run `.\configure-appsettings.bat`
3. ✅ Start infrastructure: `docker-compose up -d`
4. ✅ Verify services are healthy: `docker-compose ps`
5. ✅ Test connections to databases
6. ✅ Start your microservices

---

**Note:** This is a development configuration. For production, use proper secrets management solutions.
