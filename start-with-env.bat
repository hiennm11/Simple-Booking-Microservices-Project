@echo off
REM Script to load environment variables and start infrastructure

echo ========================================
echo Loading Environment Variables
echo ========================================
echo.

REM Check if .env file exists
if not exist .env (
    echo [ERROR] .env file not found!
    echo [INFO] Creating .env from .env.example...
    copy .env.example .env
    echo.
    echo [WARNING] Please update .env file with your credentials
    echo [INFO] Then run this script again
    pause
    exit /b 1
)

echo [INFO] Loading .env file...
for /f "usebackq tokens=1,* delims==" %%a in (.env) do (
    set "line=%%a"
    if not "!line:~0,1!"=="#" (
        set "%%a=%%b"
    )
)

echo [SUCCESS] Environment variables loaded
echo.

REM Display loaded variables (sanitized)
echo Loaded Configuration:
echo   - UserDB Port: %USERDB_PORT%
echo   - BookingDB Port: %BOOKINGDB_PORT%
echo   - PaymentDB Port: %PAYMENTDB_PORT%
echo   - RabbitMQ Port: %RABBITMQ_PORT%
echo   - RabbitMQ User: %RABBITMQ_USER%
echo.

echo ========================================
echo Starting Docker Infrastructure
echo ========================================
echo.

docker-compose up -d

if errorlevel 1 (
    echo [ERROR] Failed to start containers
    pause
    exit /b 1
)

echo.
echo [SUCCESS] Infrastructure started!
echo.
docker-compose ps
echo.

pause
