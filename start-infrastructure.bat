@echo off
REM Script to start Docker infrastructure for Booking Microservices

echo ========================================
echo Starting Booking System Infrastructure
echo ========================================
echo.

REM Check if Docker is running
docker info >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Docker is not running. Please start Docker Desktop first.
    pause
    exit /b 1
)

echo [INFO] Docker is running...
echo.

REM Start all containers
echo [INFO] Starting containers...
docker-compose up -d

if errorlevel 1 (
    echo [ERROR] Failed to start containers
    pause
    exit /b 1
)

echo.
echo [SUCCESS] Containers started successfully!
echo.

REM Wait for services to be healthy
echo [INFO] Waiting for services to be healthy...
timeout /t 10 /nobreak >nul

echo.
echo ========================================
echo Service Status
echo ========================================
docker-compose ps
echo.

echo ========================================
echo Service Access Information
echo ========================================
echo.
echo UserService PostgreSQL:
echo   Host: localhost:5432
echo   Database: userdb
echo   Username: userservice
echo   Password: userservice123
echo.
echo BookingService PostgreSQL:
echo   Host: localhost:5433
echo   Database: bookingdb
echo   Username: bookingservice
echo   Password: bookingservice123
echo.
echo PaymentService MongoDB:
echo   Host: localhost:27017
echo   Database: paymentdb
echo   Username: paymentservice
echo   Password: paymentservice123
echo.
echo RabbitMQ:
echo   AMQP: localhost:5672
echo   Management UI: http://localhost:15672
echo   Username: guest
echo   Password: guest
echo.
echo Seq (Optional):
echo   UI: http://localhost:5341
echo.
echo ========================================
echo.
echo [INFO] Infrastructure is ready!
echo [INFO] You can now start your microservices.
echo.

pause
