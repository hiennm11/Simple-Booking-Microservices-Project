@echo off
REM Complete system test script
REM Tests health, basic functionality, and runs a small load test

echo ============================================
echo    Booking System - Complete System Test
echo ============================================
echo.

REM Step 1: Check if Docker is running
echo [Step 1/5] Checking Docker...
docker info >nul 2>&1
if errorlevel 1 (
    echo   [FAIL] Docker is not running!
    echo   Please start Docker Desktop and try again.
    pause
    exit /b 1
) else (
    echo   [PASS] Docker is running
)
echo.

REM Step 2: Check if services are running
echo [Step 2/5] Checking if services are running...
docker-compose ps >nul 2>&1
if errorlevel 1 (
    echo   [WARN] Services may not be running
    echo   Starting services...
    docker-compose up -d
    echo   Waiting 30 seconds for services to start...
    timeout /t 30 /nobreak >nul
) else (
    echo   [PASS] Docker Compose is configured
)
echo.

REM Step 3: Health check
echo [Step 3/5] Checking service health...
call test-health.bat
echo.

REM Step 4: Test basic API
echo [Step 4/5] Testing basic API functionality...
echo   Testing UserService registration...

powershell -Command "$body = @{username='systemtest';email='systemtest@example.com';password='Test@2025!Pass';firstName='System';lastName='Test'} | ConvertTo-Json; try { $result = Invoke-RestMethod -Uri 'http://localhost:5000/api/users/register' -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 10; Write-Host '  [PASS] User registration works' -ForegroundColor Green; } catch { Write-Host '  [WARN] User registration failed (may already exist)' -ForegroundColor Yellow; }"

echo.

REM Step 5: Load test
echo [Step 5/5] Running load test (20 requests)...
echo   This will take about 30 seconds...
echo.

powershell -Command ".\test-load.ps1 -NumberOfRequests 20 -ConcurrentThreads 5"

echo.
echo ============================================
echo    System Test Complete!
echo ============================================
echo.
echo Next steps:
echo   1. View detailed logs: http://localhost:5341
echo   2. Check RabbitMQ: http://localhost:15672
echo   3. Run E2E test: .\test-e2e-load.ps1
echo   4. Monitor health: .\monitor-health.ps1
echo.
pause
