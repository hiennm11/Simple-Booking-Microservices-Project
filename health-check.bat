@echo off
REM Health check script for Booking Microservices Infrastructure

echo ========================================
echo Booking System Health Check
echo ========================================
echo.

REM Check Docker
echo [1/5] Checking Docker...
docker info >nul 2>&1
if errorlevel 1 (
    echo   [FAIL] Docker is not running
    set docker_ok=0
) else (
    echo   [PASS] Docker is running
    set docker_ok=1
)
echo.

REM Check Docker Compose
echo [2/5] Checking Docker Compose...
docker-compose version >nul 2>&1
if errorlevel 1 (
    echo   [FAIL] Docker Compose not found
    set compose_ok=0
) else (
    echo   [PASS] Docker Compose is available
    set compose_ok=1
)
echo.

REM Check .NET SDK
echo [3/5] Checking .NET SDK...
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo   [FAIL] .NET SDK not found
    set dotnet_ok=0
) else (
    for /f "tokens=*" %%i in ('dotnet --version') do set dotnet_version=%%i
    echo   [PASS] .NET SDK version !dotnet_version! is installed
    set dotnet_ok=1
)
echo.

REM Check if containers are running
echo [4/5] Checking Docker containers...
docker-compose ps >nul 2>&1
if errorlevel 1 (
    echo   [INFO] No containers running yet
    echo   [INFO] Run: start-infrastructure.bat
    set containers_ok=0
) else (
    echo   Container Status:
    docker-compose ps
    set containers_ok=1
)
echo.

REM Check if solution builds
echo [5/5] Checking solution build...
dotnet build BookingSystem.sln --nologo --verbosity quiet >nul 2>&1
if errorlevel 1 (
    echo   [FAIL] Solution does not build
    echo   [INFO] Run: dotnet build BookingSystem.sln
    set build_ok=0
) else (
    echo   [PASS] Solution builds successfully
    set build_ok=1
)
echo.

REM Summary
echo ========================================
echo Health Check Summary
echo ========================================
if "%docker_ok%"=="1" (echo ✓ Docker) else (echo ✗ Docker)
if "%compose_ok%"=="1" (echo ✓ Docker Compose) else (echo ✗ Docker Compose)
if "%dotnet_ok%"=="1" (echo ✓ .NET SDK) else (echo ✗ .NET SDK)
if "%containers_ok%"=="1" (echo ✓ Containers Running) else (echo ℹ Containers Not Started)
if "%build_ok%"=="1" (echo ✓ Solution Builds) else (echo ✗ Solution Build Failed)
echo.

REM Service URLs
if "%containers_ok%"=="1" (
    echo ========================================
    echo Service URLs
    echo ========================================
    echo RabbitMQ Management: http://localhost:15672
    echo Seq Logging: http://localhost:5341
    echo.
    echo Database Connections:
    echo   UserDB: localhost:5432
    echo   BookingDB: localhost:5433
    echo   PaymentDB: localhost:27017
    echo.
)

echo ========================================
echo Next Steps
echo ========================================
if "%containers_ok%"=="0" (
    echo 1. Start infrastructure: start-infrastructure.bat
)
if "%build_ok%"=="0" (
    echo 2. Build solution: dotnet build
)
echo.
echo For detailed guides, see:
echo   - QUICKSTART.md
echo   - DOCKER_SETUP.md
echo   - PROJECT_STRUCTURE.md
echo.

pause
