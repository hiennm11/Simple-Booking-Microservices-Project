@echo off
setlocal enabledelayedexpansion

REM Script to display current environment configuration (sanitized)

echo ========================================
echo Current Environment Configuration
echo ========================================
echo.

REM Auto-locate project root (2 levels up from scripts/configuration/)
set "SCRIPT_DIR=%~dp0"
cd /d "%SCRIPT_DIR%\..\..\"
set "PROJECT_ROOT=%CD%"
set "ENV_FILE=%PROJECT_ROOT%\.env"

echo [INFO] Project Root: %PROJECT_ROOT%
echo [INFO] Looking for .env file...
echo.

REM Check if .env file exists
if not exist "%ENV_FILE%" (
    echo [ERROR] .env file not found at: %ENV_FILE%
    echo [INFO] Please create .env file from .env.example
    echo.
    if exist "%PROJECT_ROOT%\.env.example" (
        echo [HINT] Found .env.example - Copy it to .env and configure
        echo        copy "%PROJECT_ROOT%\.env.example" "%PROJECT_ROOT%\.env"
    )
    pause
    cd /d "%SCRIPT_DIR%"
    exit /b 1
)

echo [INFO] Loading environment variables from: %ENV_FILE%
echo.

REM Load .env file
for /f "usebackq tokens=1,* delims==" %%a in ("%ENV_FILE%") do (
    set "line=%%a"
    REM Skip comments and empty lines
    if not "!line:~0,1!"=="#" (
        if not "%%a"=="" (
            set "%%a=%%b"
        )
    )
)

echo ========================================
echo Database Configuration
echo ========================================
echo.
echo UserService PostgreSQL:
echo   Host: %USERDB_HOST%
echo   Port: %USERDB_PORT%
echo   Database: %USERDB_NAME%
echo   Username: %USERDB_USER%
echo   Password: %USERDB_PASSWORD%
echo.
echo BookingService PostgreSQL:
echo   Host: %BOOKINGDB_HOST%
echo   Port: %BOOKINGDB_PORT%
echo   Database: %BOOKINGDB_NAME%
echo   Username: %BOOKINGDB_USER%
echo   Password: %BOOKINGDB_PASSWORD%
echo.
echo PaymentService MongoDB:
echo   Host: %PAYMENTDB_HOST%
echo   Port: %PAYMENTDB_PORT%
echo   Database: %PAYMENTDB_NAME%
echo   Username: %PAYMENTDB_USER%
echo   Password: %PAYMENTDB_PASSWORD%
echo.

echo ========================================
echo Message Broker Configuration
echo ========================================
echo.
echo RabbitMQ:
echo   Host: %RABBITMQ_HOST%
echo   Port: %RABBITMQ_PORT%
echo   Username: %RABBITMQ_USER%
echo   Password: %RABBITMQ_PASSWORD%
echo   VirtualHost: %RABBITMQ_VHOST%
echo   Management UI: http://localhost:15672
echo.

echo ========================================
echo Security Configuration
echo ========================================
echo.
echo JWT:
echo   Issuer: %JWT_ISSUER%
echo   Audience: %JWT_AUDIENCE%
echo   Expiry: %JWT_EXPIRY_MINUTES% minutes
echo   Secret Key: %JWT_SECRET_KEY%
echo.

echo ========================================
echo Service Ports
echo ========================================
echo.
echo   UserService:    http://localhost:%USERSERVICE_PORT%
echo   BookingService: http://localhost:%BOOKINGSERVICE_PORT%
echo   PaymentService: http://localhost:%PAYMENTSERVICE_PORT%
echo   ApiGateway:     http://localhost:%APIGATEWAY_PORT%
echo.

echo ========================================
echo Logging Configuration
echo ========================================
echo.
echo Seq:
echo   UI: %SEQ_URL%
echo   Username: admin
echo   Password: %SEQ_ADMIN_PASSWORD%
echo   API Key: %SEQ_API_KEY%
echo.
echo Environment: %ASPNETCORE_ENVIRONMENT%
echo Log Level: %LOG_LEVEL%
echo.

echo ========================================
echo Connection Strings
echo ========================================
echo.
echo UserService:
echo   Host=%USERDB_HOST%;Port=%USERDB_PORT%;Database=%USERDB_NAME%;Username=%USERDB_USER%;Password=%USERDB_PASSWORD%
echo.
echo BookingService:
echo   Host=%BOOKINGDB_HOST%;Port=%BOOKINGDB_PORT%;Database=%BOOKINGDB_NAME%;Username=%BOOKINGDB_USER%;Password=%BOOKINGDB_PASSWORD%
echo.
echo PaymentService:
echo   mongodb://%PAYMENTDB_USER%:%PAYMENTDB_PASSWORD%@%PAYMENTDB_HOST%:%PAYMENTDB_PORT%/%PAYMENTDB_NAME%?authSource=admin
echo.

echo ========================================
echo Service to Database Connections
echo ========================================
echo.
echo UserService (%USERSERVICE_PORT%)
echo   ^|
echo   +--^> PostgreSQL UserDB (%USERDB_HOST%:%USERDB_PORT%)
echo         Database: %USERDB_NAME%
echo         User: %USERDB_USER%
echo.
echo BookingService (%BOOKINGSERVICE_PORT%)
echo   ^|
echo   +--^> PostgreSQL BookingDB (%BOOKINGDB_HOST%:%BOOKINGDB_PORT%)
echo         Database: %BOOKINGDB_NAME%
echo         User: %BOOKINGDB_USER%
echo.
echo PaymentService (%PAYMENTSERVICE_PORT%)
echo   ^|
echo   +--^> MongoDB PaymentDB (%PAYMENTDB_HOST%:%PAYMENTDB_PORT%)
echo         Database: %PAYMENTDB_NAME%
echo         User: %PAYMENTDB_USER%
echo.

echo ========================================
echo Service Communication Architecture
echo ========================================
echo.
echo                    ApiGateway (:%APIGATEWAY_PORT%)
echo                          ^|
echo          +---------------+---------------+
echo          ^|               ^|               ^|
echo          v               v               v
echo    UserService    BookingService   PaymentService
echo      (:%USERSERVICE_PORT%)         (:%BOOKINGSERVICE_PORT%)         (:%PAYMENTSERVICE_PORT%)
echo          ^|               ^|               ^|
echo          v               v               v
echo    PostgreSQL       PostgreSQL        MongoDB
echo   UserDB:5432    BookingDB:5433   PaymentDB:27017
echo.
echo Event-Driven Communication:
echo   All Services ^<--^> RabbitMQ (:%RABBITMQ_PORT%)
echo.
echo Centralized Logging:
echo   All Services --^> Seq (%SEQ_URL%)
echo.

echo ========================================
echo Configuration Files Status
echo ========================================
echo.

if exist src\UserService\appsettings.Development.json (
    echo [OK] UserService appsettings.Development.json
) else (
    echo [MISSING] UserService appsettings.Development.json
)

if exist src\BookingService\appsettings.Development.json (
    echo [OK] BookingService appsettings.Development.json
) else (
    echo [MISSING] BookingService appsettings.Development.json
)

if exist src\PaymentService\appsettings.Development.json (
    echo [OK] PaymentService appsettings.Development.json
) else (
    echo [MISSING] PaymentService appsettings.Development.json
)

if exist src\ApiGateway\appsettings.Development.json (
    echo [OK] ApiGateway appsettings.Development.json
) else (
    echo [MISSING] ApiGateway appsettings.Development.json
)

echo.
echo ========================================
echo Connection Health Check URLs
echo ========================================
echo.
echo UserService:    http://localhost:%USERSERVICE_PORT%/health
echo BookingService: http://localhost:%BOOKINGSERVICE_PORT%/health
echo PaymentService: http://localhost:%PAYMENTSERVICE_PORT%/health
echo ApiGateway:     http://localhost:%APIGATEWAY_PORT%/health
echo.
echo RabbitMQ Management: http://localhost:15672
echo Seq Dashboard:       %SEQ_URL%
echo.

echo ========================================
echo Next Steps
echo ========================================
echo.
echo To apply configuration:
echo   1. Run: .\configure-appsettings.bat
echo   2. Start infrastructure: docker-compose up -d
echo   3. Start services with: dotnet run
echo   4. Check health: .\scripts\testing\test-health.bat
echo.

REM Return to original directory
cd /d "%SCRIPT_DIR%"

pause
