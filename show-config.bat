@echo off
setlocal enabledelayedexpansion

REM Script to display current environment configuration (sanitized)

echo ========================================
echo Current Environment Configuration
echo ========================================
echo.

REM Check if .env file exists
if not exist .env (
    echo [ERROR] .env file not found!
    echo [INFO] Please create .env file from .env.example
    pause
    exit /b 1
)

echo [INFO] Loading environment variables from .env...
echo.

REM Load .env file
for /f "usebackq tokens=1,* delims==" %%a in (.env) do (
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
echo   Password: ********
echo.
echo BookingService PostgreSQL:
echo   Host: %BOOKINGDB_HOST%
echo   Port: %BOOKINGDB_PORT%
echo   Database: %BOOKINGDB_NAME%
echo   Username: %BOOKINGDB_USER%
echo   Password: ********
echo.
echo PaymentService MongoDB:
echo   Host: %PAYMENTDB_HOST%
echo   Port: %PAYMENTDB_PORT%
echo   Database: %PAYMENTDB_NAME%
echo   Username: %PAYMENTDB_USER%
echo   Password: ********
echo.

echo ========================================
echo Message Broker Configuration
echo ========================================
echo.
echo RabbitMQ:
echo   Host: %RABBITMQ_HOST%
echo   Port: %RABBITMQ_PORT%
echo   Username: %RABBITMQ_USER%
echo   Password: ********
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
echo   Secret Key: ******** (length: %JWT_SECRET_KEY:~0,2%...)
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
echo   Password: ********
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
echo   Host=%USERDB_HOST%;Port=%USERDB_PORT%;Database=%USERDB_NAME%;Username=%USERDB_USER%;Password=********
echo.
echo BookingService:
echo   Host=%BOOKINGDB_HOST%;Port=%BOOKINGDB_PORT%;Database=%BOOKINGDB_NAME%;Username=%BOOKINGDB_USER%;Password=********
echo.
echo PaymentService:
echo   mongodb://%PAYMENTDB_USER%:********@%PAYMENTDB_HOST%:%PAYMENTDB_PORT%/%PAYMENTDB_NAME%?authSource=admin
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

echo.
echo ========================================
echo Next Steps
echo ========================================
echo.
echo To apply configuration:
echo   1. Run: .\configure-appsettings.bat
echo   2. Start infrastructure: docker-compose up -d
echo   3. Start services with: dotnet run
echo.

pause
