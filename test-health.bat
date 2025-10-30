@echo off
echo ========================================
echo Testing All Service Health Endpoints
echo ========================================
echo.

echo UserService (Port 5001):
curl -s http://localhost:5001/health
echo.
echo.

echo BookingService (Port 5002):
curl -s http://localhost:5002/health
echo.
echo.

echo PaymentService (Port 5003):
curl -s http://localhost:5003/health
echo.
echo.

echo ApiGateway (Port 5000):
curl -s http://localhost:5000/health
echo.
echo.

echo ========================================
echo All Tests Complete!
echo ========================================

docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"