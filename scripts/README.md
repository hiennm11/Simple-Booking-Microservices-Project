# 🛠️ Scripts Directory

Organized collection of batch (.bat) and PowerShell (.ps1) scripts for managing, testing, and monitoring the Simple Booking Microservices Project.

---

## 📁 Directory Structure

```
scripts/
├── infrastructure/          # Docker and service lifecycle management
├── configuration/          # Environment and settings configuration
├── testing/                # End-to-end and load testing scripts
└── monitoring/             # Health checks and monitoring
```

---

## 🏗️ Infrastructure Scripts

**Location:** `scripts/infrastructure/`

Manage Docker containers and microservices infrastructure.

| Script | Description | Usage |
|--------|-------------|-------|
| **start-infrastructure.bat** | Start all Docker services (PostgreSQL, MongoDB, RabbitMQ, Seq) | `.\scripts\infrastructure\start-infrastructure.bat` |
| **stop-infrastructure.bat** | Stop all Docker services gracefully | `.\scripts\infrastructure\stop-infrastructure.bat` |
| **reset-infrastructure.bat** | Stop services, remove volumes, and perform clean restart | `.\scripts\infrastructure\reset-infrastructure.bat` |
| **start-with-env.bat** | Start infrastructure with environment variable validation | `.\scripts\infrastructure\start-with-env.bat` |

### Quick Start Examples

```cmd
REM Start all infrastructure services
.\scripts\infrastructure\start-infrastructure.bat

REM Stop all services
.\scripts\infrastructure\stop-infrastructure.bat

REM Complete reset (removes data volumes)
.\scripts\infrastructure\reset-infrastructure.bat
```

**⚠️ Warning:** `reset-infrastructure.bat` will delete all database data!

---

## ⚙️ Configuration Scripts

**Location:** `scripts/configuration/`

Manage application settings and environment configuration.

| Script | Description | Usage |
|--------|-------------|-------|
| **configure-appsettings.bat** | Configure appsettings.json files for all services | `.\scripts\configuration\configure-appsettings.bat` |
| **show-config.bat** | Display current configuration settings | `.\scripts\configuration\show-config.bat` |

### Quick Start Examples

```cmd
REM Configure all appsettings files
.\scripts\configuration\configure-appsettings.bat

REM View current configuration
.\scripts\configuration\show-config.bat
```

---

## 🧪 Testing Scripts

**Location:** `scripts/testing/`

Comprehensive testing suite including unit, integration, E2E, and load tests.

### Batch Scripts (.bat)

| Script | Description | Usage |
|--------|-------------|-------|
| **test-system.bat** | Complete system test (infrastructure + E2E) | `.\scripts\testing\test-system.bat` |
| **test-health.bat** | Quick health check for all services | `.\scripts\testing\test-health.bat` |

### PowerShell Scripts (.ps1)

| Script | Type | Description | Usage |
|--------|------|-------------|-------|
| **test-gateway.ps1** | API | Test API Gateway endpoints | `.\scripts\testing\test-gateway.ps1` |
| **test-gateway-full.ps1** | API | Comprehensive Gateway testing with auth | `.\scripts\testing\test-gateway-full.ps1` |
| **test-auth.ps1** | Security | Authentication & authorization tests | `.\scripts\testing\test-auth.ps1` |
| **test-e2e-simple.ps1** | E2E | Simple end-to-end workflow test | `.\scripts\testing\test-e2e-simple.ps1` |
| **test-e2e-auth.ps1** | E2E + Auth | E2E test with JWT authentication | `.\scripts\testing\test-e2e-auth.ps1` |
| **test-e2e-load.ps1** | E2E + Load | End-to-end test with load testing | `.\scripts\testing\test-e2e-load.ps1` |
| **test-load.ps1** | Load | Load testing with concurrent requests | `.\scripts\testing\test-load.ps1` |
| **test-load-simple.ps1** | Load | Simple load test with basic metrics | `.\scripts\testing\test-load-simple.ps1` |

### Testing Hierarchy

```
test-system.bat              # ← Start here (complete test)
    ↓
test-health.bat              # Health checks
    ↓
test-gateway.ps1             # API Gateway tests
    ↓
test-auth.ps1                # ← Authentication & authorization tests
    ↓
test-e2e-simple.ps1          # Basic workflow
    ↓
test-e2e-auth.ps1            # ← E2E with JWT authentication
    ↓
test-e2e-load.ps1            # E2E with load
    ↓
test-load.ps1                # Full load testing
```

### Quick Start Examples

```cmd
REM Complete system test
.\scripts\testing\test-system.bat

REM Quick health check
.\scripts\testing\test-health.bat
```

```powershell
# API Gateway testing
.\scripts\testing\test-gateway.ps1

# Authentication & Authorization testing
.\scripts\testing\test-auth.ps1

# Simple E2E test (no auth)
.\scripts\testing\test-e2e-simple.ps1

# E2E test with JWT authentication
.\scripts\testing\test-e2e-auth.ps1

# Load testing
.\scripts\testing\test-load.ps1
```

### Authentication & Authorization Testing

The project includes comprehensive authentication and authorization testing:

**test-auth.ps1** - Comprehensive security testing:
- ✓ User registration (public endpoint)
- ✓ User login and JWT token generation
- ✓ Authorized access with valid tokens
- ✓ Unauthorized access without tokens (401 expected)
- ✓ Invalid token rejection
- ✓ Malformed authorization headers
- ✓ Payment authorization tests
- ✓ Token reusability across concurrent requests

**test-e2e-auth.ps1** - End-to-end authenticated flows:
- Registers users and obtains JWT tokens
- Creates bookings with authentication
- Processes payments with authentication
- Verifies booking status with authentication
- Measures authentication overhead
- Tests concurrent authenticated flows

**test-gateway-full.ps1** - API Gateway with authentication:
- User registration and login flow
- JWT token generation and validation
- Protected endpoint access tests
- Invalid token rejection
- Unauthorized access tests

**Usage Examples:**

```powershell
# Run comprehensive auth tests
.\scripts\testing\test-auth.ps1

# Run E2E with authentication (10 flows)
.\scripts\testing\test-e2e-auth.ps1

# Run E2E with custom parameters
.\scripts\testing\test-e2e-auth.ps1 -NumberOfFlows 50 -ConcurrentFlows 5

# Run load tests with authentication enabled
.\scripts\testing\test-e2e-load.ps1 -UseAuthentication -NumberOfFlows 100
```

---

## 📊 Monitoring Scripts

**Location:** `scripts/monitoring/`

Monitor service health and system status.

| Script | Type | Description | Usage |
|--------|------|-------------|-------|
| **health-check.bat** | Batch | One-time health check for all services | `.\scripts\monitoring\health-check.bat` |
| **monitor-health.ps1** | PowerShell | Continuous health monitoring with auto-refresh | `.\scripts\monitoring\monitor-health.ps1` |

### Quick Start Examples

```cmd
REM Single health check
.\scripts\monitoring\health-check.bat
```

```powershell
# Continuous monitoring
.\scripts\monitoring\monitor-health.ps1
```

---

## 🚀 Common Workflows

### First Time Setup

```cmd
# 1. Start infrastructure
.\scripts\infrastructure\start-infrastructure.bat

# 2. Configure settings
.\scripts\configuration\configure-appsettings.bat

# 3. Verify health
.\scripts\monitoring\health-check.bat

# 4. Run system test
.\scripts\testing\test-system.bat
```

### Daily Development

```cmd
# Start services
.\scripts\infrastructure\start-infrastructure.bat

# Quick health check
.\scripts\testing\test-health.bat

# Work on code...

# Run tests
.\scripts\testing\test-e2e-simple.ps1

# Stop services
.\scripts\infrastructure\stop-infrastructure.bat
```

### Testing Workflow

```cmd
# 1. Health check
.\scripts\testing\test-health.bat

# 2. API tests
.\scripts\testing\test-gateway.ps1

# 3. E2E tests
.\scripts\testing\test-e2e-simple.ps1

# 4. Load tests (optional)
.\scripts\testing\test-load-simple.ps1
```

### Troubleshooting Workflow

```cmd
# 1. Check current config
.\scripts\configuration\show-config.bat

# 2. Reset infrastructure
.\scripts\infrastructure\reset-infrastructure.bat

# 3. Reconfigure
.\scripts\configuration\configure-appsettings.bat

# 4. Restart with validation
.\scripts\infrastructure\start-with-env.bat

# 5. Verify health
.\scripts\monitoring\health-check.bat
```

---

## 💡 Script Types Reference

### Batch Scripts (.bat)
- **Platform:** Windows only
- **Shell:** CMD.exe
- **Use Cases:** Simple commands, infrastructure management
- **Execution:** Double-click or `script-name.bat`

### PowerShell Scripts (.ps1)
- **Platform:** Cross-platform (Windows/Linux/macOS with PowerShell Core)
- **Shell:** PowerShell 5.1+ or PowerShell Core 6+
- **Use Cases:** Complex testing, API calls, data processing
- **Execution:** `.\script-name.ps1` or `pwsh script-name.ps1`

---

## ⚙️ Prerequisites

### Required Software

- **Docker Desktop** - For infrastructure services
- **PowerShell** - Version 5.1+ (Windows) or PowerShell Core 6+ (Cross-platform)
- **.NET 8 SDK** - For running microservices
- **Git Bash** (optional) - For Unix-like shell experience on Windows

### Environment Variables

Ensure `.env` file exists in project root with required variables:
- `POSTGRES_USER`
- `POSTGRES_PASSWORD`
- `MONGO_INITDB_ROOT_USERNAME`
- `MONGO_INITDB_ROOT_PASSWORD`
- `RABBITMQ_DEFAULT_USER`
- `RABBITMQ_DEFAULT_PASS`

See `.env.example` for complete list.

---

## 🔒 Security Notes

- **Never commit** `.env` files with real credentials
- Use `.env.local.example` as a template for local development
- Rotate passwords regularly in production environments
- Keep scripts with restricted permissions in production

---

## 📝 Script Naming Conventions

### Batch Files
- **Pattern:** `verb-noun.bat`
- **Examples:** `start-infrastructure.bat`, `test-system.bat`

### PowerShell Files
- **Pattern:** `verb-noun-detail.ps1`
- **Examples:** `test-e2e-load.ps1`, `monitor-health.ps1`

### Categories
- `start-*` / `stop-*` - Service lifecycle
- `test-*` - Testing scripts
- `configure-*` - Configuration management
- `monitor-*` / `*-health` - Monitoring and health checks
- `reset-*` - Destructive operations (use with caution)

---

## 🐛 Troubleshooting

### Scripts Not Running

**Issue:** PowerShell execution policy blocks scripts

```powershell
# Check current policy
Get-ExecutionPolicy

# Set policy (run as Administrator)
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

**Issue:** Batch scripts not found

```cmd
# Ensure you're in the project root
cd d:\repos\Simple-Booking-Microservices-Project

# Run with relative path
.\scripts\infrastructure\start-infrastructure.bat
```

### Docker Issues

**Issue:** Docker services won't start

```cmd
# Reset infrastructure completely
.\scripts\infrastructure\reset-infrastructure.bat

# Check Docker is running
docker ps

# View logs
docker-compose logs
```

### Test Failures

**Issue:** Tests fail with connection errors

```cmd
# Verify services are healthy
.\scripts\monitoring\health-check.bat

# Check configuration
.\scripts\configuration\show-config.bat

# Restart with validation
.\scripts\infrastructure\start-with-env.bat
```

---

## 📚 Related Documentation

- [Main Project README](../README.md) - Project overview
- [Testing Quick Start](../TESTING_QUICK_START.md) - Testing guide
- [E2E Testing Guide](../docs/general/E2E_TESTING_GUIDE.md) - Detailed testing documentation
- [Docker Setup](../docs/phase1-foundation/DOCKER_SETUP.md) - Infrastructure setup
- [Environment Configuration](../docs/phase1-foundation/ENV_CONFIGURATION_COMPLETE.md) - Configuration guide

---

## 🔄 Migration Notes

### Previous Structure
All scripts were located in the project root directory, making it difficult to find and organize them.

### New Structure (Current)
Scripts are now organized into logical categories:
- Infrastructure management
- Configuration
- Testing
- Monitoring

### Breaking Changes
If you have shortcuts or CI/CD pipelines referencing old paths, update them:

**Old Path:**
```cmd
.\test-system.bat
```

**New Path:**
```cmd
.\scripts\testing\test-system.bat
```

---

## 📊 Script Statistics

| Category | Batch Scripts | PowerShell Scripts | Total |
|----------|---------------|-------------------|-------|
| Infrastructure | 4 | 0 | 4 |
| Configuration | 2 | 0 | 2 |
| Testing | 2 | 8 | 10 |
| Monitoring | 1 | 1 | 2 |
| **Total** | **9** | **9** | **18** |

---

**Last Updated:** November 5, 2025  
**Organization Date:** November 5, 2025  
**Total Scripts:** 18 (9 .bat + 9 .ps1)  
**Status:** ✅ Complete
