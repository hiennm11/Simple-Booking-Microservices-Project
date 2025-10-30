@echo off
setlocal enabledelayedexpansion

REM Script to substitute environment variables in appsettings.json files

echo ========================================
echo Configuring appsettings.json files
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

echo [SUCCESS] Environment variables loaded
echo.

echo [INFO] Updating UserService appsettings.json...
(
echo {
echo   "Logging": {
echo     "LogLevel": {
echo       "Default": "Information",
echo       "Microsoft.AspNetCore": "Warning"
echo     }
echo   },
echo   "AllowedHosts": "*",
echo   "ConnectionStrings": {
echo     "DefaultConnection": "Host=%USERDB_HOST%;Port=%USERDB_PORT%;Database=%USERDB_NAME%;Username=%USERDB_USER%;Password=%USERDB_PASSWORD%"
echo   },
echo   "Jwt": {
echo     "Key": "%JWT_SECRET_KEY%",
echo     "Issuer": "%JWT_ISSUER%",
echo     "Audience": "%JWT_AUDIENCE%",
echo     "ExpiryInMinutes": %JWT_EXPIRY_MINUTES%
echo   }
echo }
) > src\UserService\appsettings.Development.json

echo [SUCCESS] UserService configured
echo.

echo [INFO] Updating BookingService appsettings.json...
(
echo {
echo   "Logging": {
echo     "LogLevel": {
echo       "Default": "Information",
echo       "Microsoft.AspNetCore": "Warning"
echo     }
echo   },
echo   "AllowedHosts": "*",
echo   "ConnectionStrings": {
echo     "DefaultConnection": "Host=%BOOKINGDB_HOST%;Port=%BOOKINGDB_PORT%;Database=%BOOKINGDB_NAME%;Username=%BOOKINGDB_USER%;Password=%BOOKINGDB_PASSWORD%"
echo   },
echo   "RabbitMQ": {
echo     "HostName": "%RABBITMQ_HOST%",
echo     "Port": %RABBITMQ_PORT%,
echo     "UserName": "%RABBITMQ_USER%",
echo     "Password": "%RABBITMQ_PASSWORD%",
echo     "VirtualHost": "%RABBITMQ_VHOST%",
echo     "Queues": {
echo       "BookingCreated": "booking_created",
echo       "PaymentSucceeded": "payment_succeeded"
echo     }
echo   }
echo }
) > src\BookingService\appsettings.Development.json

echo [SUCCESS] BookingService configured
echo.

echo [INFO] Updating PaymentService appsettings.json...
(
echo {
echo   "Logging": {
echo     "LogLevel": {
echo       "Default": "Information",
echo       "Microsoft.AspNetCore": "Warning"
echo     }
echo   },
echo   "AllowedHosts": "*",
echo   "MongoDB": {
echo     "ConnectionString": "mongodb://%PAYMENTDB_USER%:%PAYMENTDB_PASSWORD%@%PAYMENTDB_HOST%:%PAYMENTDB_PORT%/%PAYMENTDB_NAME%?authSource=admin",
echo     "DatabaseName": "%PAYMENTDB_NAME%",
echo     "Collections": {
echo       "Payments": "payments"
echo     }
echo   },
echo   "RabbitMQ": {
echo     "HostName": "%RABBITMQ_HOST%",
echo     "Port": %RABBITMQ_PORT%,
echo     "UserName": "%RABBITMQ_USER%",
echo     "Password": "%RABBITMQ_PASSWORD%",
echo     "VirtualHost": "%RABBITMQ_VHOST%",
echo     "Queues": {
echo       "BookingCreated": "booking_created",
echo       "PaymentSucceeded": "payment_succeeded",
echo       "PaymentFailed": "payment_failed"
echo     }
echo   }
echo }
) > src\PaymentService\appsettings.Development.json

echo [SUCCESS] PaymentService configured
echo.

echo ========================================
echo Configuration Complete!
echo ========================================
echo.
echo All appsettings.Development.json files have been created
echo with values from your .env file.
echo.
echo You can now run your services with:
echo   dotnet run --project src/UserService/UserService.csproj
echo   dotnet run --project src/BookingService/BookingService.csproj
echo   dotnet run --project src/PaymentService/PaymentService.csproj
echo.

pause
