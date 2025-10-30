@echo off
REM Script to stop Docker infrastructure for Booking Microservices

echo ========================================
echo Stopping Booking System Infrastructure
echo ========================================
echo.

REM Check if Docker is running
docker info >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Docker is not running.
    pause
    exit /b 1
)

echo [INFO] Stopping all containers...
docker-compose down

if errorlevel 1 (
    echo [ERROR] Failed to stop containers
    pause
    exit /b 1
)

echo.
echo [SUCCESS] All containers stopped successfully!
echo.
echo To remove all data (volumes), run:
echo   docker-compose down -v
echo.

pause
