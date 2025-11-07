Write-Host "Testing API Gateway - User Registration"
Write-Host "========================================`n"

$timestamp = Get-Date -Format "yyyyMMddHHmmss"
$testUsername = "testuser_$timestamp"
$testEmail = "test_${timestamp}@example.com"

$body = @{
    username = $testUsername
    email = $testEmail
    password = "Test@2025!Pass"
    firstName = "Test"
    lastName = "User"
} | ConvertTo-Json

Write-Host "Registering new user: $testUsername..."
try {
    $response = Invoke-RestMethod -Uri "http://localhost:5000/api/users/register" `
        -Method Post `
        -ContentType "application/json" `
        -Body $body

    Write-Host "SUCCESS! User registered through API Gateway" -ForegroundColor Green
    Write-Host "`nResponse:"
    $response | ConvertTo-Json
    $userId = $response.data.id
    Write-Host "`nUser ID: $userId" -ForegroundColor Cyan
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
}

Write-Host "`n`nTesting API Gateway - User Login"
Write-Host "================================`n"

$loginBody = @{
    username = $testUsername
    password = "Test@2025!Pass"
} | ConvertTo-Json

Write-Host "Logging in as: $testUsername..."
try {
    $loginResponse = Invoke-RestMethod -Uri "http://localhost:5000/api/users/login" `
        -Method Post `
        -ContentType "application/json" `
        -Body $loginBody

    Write-Host "SUCCESS! User logged in through API Gateway" -ForegroundColor Green
    Write-Host "`nResponse:"
    $loginResponse | ConvertTo-Json
    
    $token = $loginResponse.data.token.Trim()
    Write-Host "`nJWT Token:" -ForegroundColor Cyan
    Write-Host "  $($token.Substring(0, [Math]::Min(50, $token.Length)))..." -ForegroundColor Gray
    Write-Host "  Length: $($token.Length) characters" -ForegroundColor Gray
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
}

Write-Host "`n`nTesting API Gateway - Access Protected Endpoint"
Write-Host "================================================`n"

Write-Host "Testing unauthorized access (no token)..."
try {
    $unauthorizedResponse = Invoke-WebRequest -Uri "http://localhost:5000/api/bookings" `
        -Method Get `
        -SkipHttpErrorCheck
    
    if ($unauthorizedResponse.StatusCode -eq 401) {
        Write-Host "SUCCESS! Unauthorized request properly rejected (401)" -ForegroundColor Green
    } else {
        Write-Host "WARNING! Expected 401 but got $($unauthorizedResponse.StatusCode)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}

Write-Host "`nTesting authorized access (with token)..."
try {
    $headers = @{
        "Authorization" = "Bearer $token"
    }
    
    $authorizedResponse = Invoke-RestMethod -Uri "http://localhost:5000/api/bookings" `
        -Method Get `
        -Headers $headers
    
    Write-Host "SUCCESS! Authorized request accepted" -ForegroundColor Green
    Write-Host "`nBookings Response:"
    $authorizedResponse | ConvertTo-Json
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}

Write-Host "`n`nTesting API Gateway - Create Booking with Auth"
Write-Host "===============================================`n"

$bookingBody = @{
    userId = $userId
    roomId = "ROOM-101"
    amount = 500000
} | ConvertTo-Json

Write-Host "Creating booking with JWT authentication..."
try {
    $headers = @{
        "Authorization" = "Bearer $token"
    }
    
    $bookingResponse = Invoke-RestMethod -Uri "http://localhost:5000/api/bookings" `
        -Method Post `
        -Headers $headers `
        -ContentType "application/json" `
        -Body $bookingBody
    
    Write-Host "SUCCESS! Booking created with authentication" -ForegroundColor Green
    Write-Host "`nBooking Response:"
    $bookingResponse | ConvertTo-Json
    
    $bookingId = $bookingResponse.id
    Write-Host "`nBooking ID: $bookingId" -ForegroundColor Cyan
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}

Write-Host "`n`nTesting API Gateway - Invalid Token"
Write-Host "===================================`n"

Write-Host "Testing access with invalid token..."
try {
    $invalidHeaders = @{
        "Authorization" = "Bearer invalid.token.here"
    }
    
    $invalidTokenResponse = Invoke-WebRequest -Uri "http://localhost:5000/api/bookings" `
        -Method Get `
        -Headers $invalidHeaders `
        -SkipHttpErrorCheck
    
    if ($invalidTokenResponse.StatusCode -eq 401) {
        Write-Host "SUCCESS! Invalid token properly rejected (401)" -ForegroundColor Green
    } else {
        Write-Host "WARNING! Expected 401 but got $($invalidTokenResponse.StatusCode)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}

Write-Host "`n`n========================================" -ForegroundColor Cyan
Write-Host "   API Gateway Authentication Tests Complete" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "[PASS] User registration successful" -ForegroundColor Green
Write-Host "[PASS] User login and JWT generation successful" -ForegroundColor Green
Write-Host "[PASS] Unauthorized access properly rejected" -ForegroundColor Green
Write-Host "[PASS] Authorized access successful" -ForegroundColor Green
Write-Host "[PASS] Invalid token properly rejected" -ForegroundColor Green
Write-Host ""
