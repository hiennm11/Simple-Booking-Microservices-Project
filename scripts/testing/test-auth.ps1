param(
    [string]$GatewayUrl = "http://localhost:5000"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Authentication & Authorization Tests" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Gateway URL: $GatewayUrl" -ForegroundColor White
Write-Host ""

$script:testResults = @{
    passed = 0
    failed = 0
    tests = @()
}

function Test-Endpoint {
    param(
        [string]$TestName,
        [string]$Uri,
        [string]$Method = "GET",
        [hashtable]$Headers = @{},
        [string]$Body = $null,
        [int]$ExpectedStatus = 200,
        [string]$Description = ""
    )
    
    Write-Host "`n[$TestName]" -ForegroundColor Yellow
    if ($Description) {
        Write-Host "  $Description" -ForegroundColor Gray
    }
    
    try {
        $requestParams = @{
            Uri = $Uri
            Method = $Method
            ContentType = "application/json"
            TimeoutSec = 10
            UseBasicParsing = $true
        }
        
        if ($Headers.Count -gt 0) {
            $requestParams['Headers'] = $Headers
        }
        
        if ($Body) {
            $requestParams['Body'] = $Body
        }
        
        # Use try-catch to handle HTTP errors instead of SkipHttpErrorCheck
        try {
            $response = Invoke-WebRequest @requestParams
        } catch {
            # If it's an HTTP error, extract the response
            if ($_.Exception.Response) {
                $response = $_.Exception.Response
                $statusCode = [int]$response.StatusCode
                
                # Create a mock response object
                $response = [PSCustomObject]@{
                    StatusCode = $statusCode
                    Content = ""
                }
            } else {
                throw
            }
        }
        
        if ($response.StatusCode -eq $ExpectedStatus) {
            Write-Host "  [PASS] Status: $($response.StatusCode)" -ForegroundColor Green
            $script:testResults.passed++
            $script:testResults.tests += @{
                name = $TestName
                status = "PASS"
                expectedStatus = $ExpectedStatus
                actualStatus = $response.StatusCode
            }
            return @{
                success = $true
                statusCode = $response.StatusCode
                content = $response.Content
            }
        } else {
            Write-Host "  [FAIL] Expected: $ExpectedStatus, Got: $($response.StatusCode)" -ForegroundColor Red
            $script:testResults.failed++
            $script:testResults.tests += @{
                name = $TestName
                status = "FAIL"
                expectedStatus = $ExpectedStatus
                actualStatus = $response.StatusCode
            }
            return @{
                success = $false
                statusCode = $response.StatusCode
                content = $response.Content
            }
        }
    } catch {
        Write-Host "  [FAIL] Error: $($_.Exception.Message)" -ForegroundColor Red
        $script:testResults.failed++
        $script:testResults.tests += @{
            name = $TestName
            status = "FAIL"
            expectedStatus = $ExpectedStatus
            actualStatus = "ERROR"
            error = $_.Exception.Message
        }
        return @{
            success = $false
            error = $_.Exception.Message
        }
    }
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Phase 1: User Registration" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$timestamp = Get-Date -Format "yyyyMMddHHmmss"
$testUsername = "testuser_$timestamp"
$testEmail = "test_${timestamp}@example.com"
$testPassword = "Test@2025!Pass"

$registerBody = @{
    Username = $testUsername
    Email = $testEmail
    Password = $testPassword
    FirstName = "Test"
    LastName = "User"
} | ConvertTo-Json

$registerResult = Test-Endpoint `
    -TestName "1.1 Register New User" `
    -Uri "$GatewayUrl/api/users/register" `
    -Method "POST" `
    -Body $registerBody `
    -ExpectedStatus 200 `
    -Description "Public endpoint - no authentication required"

if ($registerResult.success) {
    $userResponse = $registerResult.content | ConvertFrom-Json
    $userId = $userResponse.data.id
    Write-Host "  User ID: $userId" -ForegroundColor Gray
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "   Phase 2: User Login and Token Generation" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$loginBody = @{
    Username = $testUsername
    Password = $testPassword
} | ConvertTo-Json

$loginResult = Test-Endpoint `
    -TestName "2.1 Login with Valid Credentials" `
    -Uri "$GatewayUrl/api/users/login" `
    -Method "POST" `
    -Body $loginBody `
    -ExpectedStatus 200 `
    -Description "Should return JWT token"

$token = $null
if ($loginResult.success) {
    $loginResponse = $loginResult.content | ConvertFrom-Json
    $token = $loginResponse.data.token.Trim()
    Write-Host "  Token received: $($token.Substring(0, [Math]::Min(50, $token.Length)))..." -ForegroundColor Gray
    Write-Host "  Token length: $($token.Length) characters" -ForegroundColor Gray
    
    # Validate token format (should be three base64url parts separated by dots)
    $tokenParts = $token -split '\.'
    if ($tokenParts.Count -ne 3) {
        Write-Host "  [WARNING] Token format invalid - expected 3 parts, got $($tokenParts.Count)" -ForegroundColor Yellow
    } else {
        Write-Host "  Token format validated: header.payload.signature" -ForegroundColor Gray
    }
}

# Test invalid login
$invalidLoginBody = @{
    Username = $testUsername
    Password = "WrongPassword123!"
} | ConvertTo-Json

Test-Endpoint `
    -TestName "2.2 Login with Invalid Password" `
    -Uri "$GatewayUrl/api/users/login" `
    -Method "POST" `
    -Body $invalidLoginBody `
    -ExpectedStatus 401 `
    -Description "Should fail with 401 Unauthorized" | Out-Null

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "   Phase 3: Authorization Tests" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if ($token) {
    $authHeaders = @{
        "Authorization" = "Bearer $token"
    }
    
    # Test 3.1: Access protected endpoint with valid token
    Test-Endpoint `
        -TestName "3.1 Access Bookings with Valid Token" `
        -Uri "$GatewayUrl/api/bookings" `
        -Method "GET" `
        -Headers $authHeaders `
        -ExpectedStatus 200 `
        -Description "Should succeed with valid JWT token" | Out-Null
    
    # Test 3.2: Create booking with valid token
    $bookingBody = @{
        UserId = $userId
        RoomId = "ROOM-$(Get-Random -Minimum 100 -Maximum 999)"
        Amount = 500000
    } | ConvertTo-Json
    
    $createBookingResult = Test-Endpoint `
        -TestName "3.2 Create Booking with Valid Token" `
        -Uri "$GatewayUrl/api/bookings" `
        -Method "POST" `
        -Headers $authHeaders `
        -Body $bookingBody `
        -ExpectedStatus 201 `
        -Description "Should create booking successfully"
    
    $bookingId = $null
    if ($createBookingResult.success) {
        $bookingResponse = $createBookingResult.content | ConvertFrom-Json
        $bookingId = $bookingResponse.id
        Write-Host "  Booking ID: $bookingId" -ForegroundColor Gray
    }
    
    # Test 3.3: Access booking details
    if ($bookingId) {
        Test-Endpoint `
            -TestName "3.3 Get Booking Details with Valid Token" `
            -Uri "$GatewayUrl/api/bookings/$bookingId" `
            -Method "GET" `
            -Headers $authHeaders `
            -ExpectedStatus 200 `
            -Description "Should retrieve booking details" | Out-Null
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "   Phase 4: Unauthorized Access Tests" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Test 4.1: Access protected endpoint without token
Test-Endpoint `
    -TestName "4.1 Access Bookings WITHOUT Token" `
    -Uri "$GatewayUrl/api/bookings" `
    -Method "GET" `
    -ExpectedStatus 401 `
    -Description "Should fail with 401 Unauthorized" | Out-Null

# Test 4.2: Access with invalid token
$invalidHeaders = @{
    "Authorization" = "Bearer invalid.token.here"
}

Test-Endpoint `
    -TestName "4.2 Access Bookings with Invalid Token" `
    -Uri "$GatewayUrl/api/bookings" `
    -Method "GET" `
    -Headers $invalidHeaders `
    -ExpectedStatus 401 `
    -Description "Should fail with 401 Unauthorized" | Out-Null

# Test 4.3: Access with malformed Authorization header
$malformedHeaders = @{
    "Authorization" = "InvalidFormat $token"
}

Test-Endpoint `
    -TestName "4.3 Access Bookings with Malformed Header" `
    -Uri "$GatewayUrl/api/bookings" `
    -Method "GET" `
    -Headers $malformedHeaders `
    -ExpectedStatus 401 `
    -Description "Should fail with 401 Unauthorized" | Out-Null

# Test 4.4: Create booking without token
$unauthorizedBookingBody = @{
    UserId = $userId
    RoomId = "ROOM-999"
    Amount = 100000
} | ConvertTo-Json

Test-Endpoint `
    -TestName "4.4 Create Booking WITHOUT Token" `
    -Uri "$GatewayUrl/api/bookings" `
    -Method "POST" `
    -Body $unauthorizedBookingBody `
    -ExpectedStatus 401 `
    -Description "Should fail with 401 Unauthorized" | Out-Null

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "   Phase 5: Payment Authorization Tests" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if ($token -and $bookingId) {
    $authHeaders = @{
        "Authorization" = "Bearer $token"
    }
    
    # Test 5.1: Process payment with valid token
    $paymentBody = @{
        BookingId = $bookingId
        Amount = 500000
    } | ConvertTo-Json
    
    Test-Endpoint `
        -TestName "5.1 Process Payment with Valid Token" `
        -Uri "$GatewayUrl/api/payment/pay" `
        -Method "POST" `
        -Headers $authHeaders `
        -Body $paymentBody `
        -ExpectedStatus 200 `
        -Description "Should process payment successfully" | Out-Null
    
    # Test 5.2: Process payment without token
    Test-Endpoint `
        -TestName "5.2 Process Payment WITHOUT Token" `
        -Uri "$GatewayUrl/api/payment/pay" `
        -Method "POST" `
        -Body $paymentBody `
        -ExpectedStatus 401 `
        -Description "Should fail with 401 Unauthorized" | Out-Null
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "   Phase 6: Token Validation Tests" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Test 6.1: Multiple concurrent requests with same token
if ($token) {
    Write-Host "`n[6.1 Concurrent Requests with Same Token]" -ForegroundColor Yellow
    Write-Host "  Testing token reusability" -ForegroundColor Gray
    
    $authHeaders = @{
        "Authorization" = "Bearer $token"
    }
    
    $concurrentTests = 1..5 | ForEach-Object {
        Start-Job -ScriptBlock {
            param($url, $headers)
            try {
                $response = Invoke-WebRequest -Uri $url -Method GET -Headers $headers -TimeoutSec 10 -SkipHttpErrorCheck
                return $response.StatusCode
            } catch {
                return 0
            }
        } -ArgumentList "$GatewayUrl/api/bookings", $authHeaders
    }
    
    $results = $concurrentTests | Wait-Job | Receive-Job
    $concurrentTests | Remove-Job
    
    $successCount = ($results | Where-Object { $_ -eq 200 }).Count
    if ($successCount -eq 5) {
        Write-Host "  [PASS] All 5 concurrent requests succeeded" -ForegroundColor Green
        $script:testResults.passed++
    } else {
        Write-Host "  [FAIL] Only $successCount/5 requests succeeded" -ForegroundColor Red
        $script:testResults.failed++
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "   Test Results Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$total = $script:testResults.passed + $script:testResults.failed
$successRate = if ($total -gt 0) { [math]::Round(($script:testResults.passed / $total) * 100, 2) } else { 0 }

Write-Host "Total Tests:       $total"
Write-Host "Passed:            $($script:testResults.passed)" -ForegroundColor Green
Write-Host "Failed:            $($script:testResults.failed)" -ForegroundColor $(if ($script:testResults.failed -gt 0) { "Red" } else { "Green" })
Write-Host "Success Rate:      $successRate%" -ForegroundColor $(if ($successRate -eq 100) { "Green" } elseif ($successRate -ge 80) { "Yellow" } else { "Red" })
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Test Details" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

foreach ($test in $script:testResults.tests) {
    $statusColor = if ($test.status -eq "PASS") { "Green" } else { "Red" }
    $statusSymbol = if ($test.status -eq "PASS") { "[PASS]" } else { "[FAIL]" }
    Write-Host "$statusSymbol $($test.name) - $($test.status)" -ForegroundColor $statusColor
    if ($test.error) {
        Write-Host "    Error: $($test.error)" -ForegroundColor Gray
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "   Overall Assessment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if ($successRate -eq 100) {
    Write-Host "[EXCELLENT] All authentication and authorization tests passed!" -ForegroundColor Green
} elseif ($successRate -ge 90) {
    Write-Host "[GOOD] Most tests passed with minor issues" -ForegroundColor Yellow
} elseif ($successRate -ge 70) {
    Write-Host "[ACCEPTABLE] Some tests failed - review failures" -ForegroundColor Yellow
} else {
    Write-Host "[CRITICAL] Many tests failed - authentication system needs attention" -ForegroundColor Red
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Next Steps" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "1. Check Seq logs for authentication events:  http://localhost:5341" -ForegroundColor Gray
Write-Host "2. Decode JWT token:                           https://jwt.io" -ForegroundColor Gray
Write-Host "3. Review API Gateway logs:                    docker logs apigateway" -ForegroundColor Gray
Write-Host "4. Run E2E tests with authentication:          .\test-e2e-auth.ps1" -ForegroundColor Gray
Write-Host ""

# Exit with appropriate code
if ($script:testResults.failed -eq 0) {
    exit 0
} else {
    exit 1
}
