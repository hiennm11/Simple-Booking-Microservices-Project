@echo off
REM Script to reset Docker infrastructure (stop, remove volumes, and restart)

echo ========================================
echo Resetting Booking System Infrastructure
echo ========================================
echo.
echo WARNING: This will delete all data in the databases!
echo.
set /p confirm="Are you sure you want to continue? (Y/N): "

if /i not "%confirm%"=="Y" (
    echo [INFO] Reset cancelled.
    pause
    exit /b 0
)

echo.
echo [INFO] Stopping and removing containers and volumes...
docker-compose down -v

if errorlevel 1 (
    echo [ERROR] Failed to stop and remove containers
    pause
    exit /b 1
)

echo.
echo [INFO] Starting fresh containers...
docker-compose up -d

if errorlevel 1 (
    echo [ERROR] Failed to start containers
    pause
    exit /b 1
)

echo.
echo [SUCCESS] Infrastructure reset complete!
echo [INFO] Waiting for services to be healthy...
timeout /t 10 /nobreak >nul

echo.
docker-compose ps
echo.
echo [INFO] All services are ready!
echo.

pause
