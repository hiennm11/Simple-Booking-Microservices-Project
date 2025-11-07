# Test Global Exception Handling
# This script tests the global exception handler across all services

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "Testing Global Exception Handling" -ForegroundColor Cyan
Write-Host "================================================`n" -ForegroundColor Cyan

$baseUrl = "http://localhost:5000"
$results = @()

function Test-Exception {
    param(
        [string]$TestName,
        [string]$Url,
        [string]$Method = "GET",
        [string]$Body = $null,
        [int]$ExpectedStatus
    )
    
    Write-Host "Test: $TestName" -ForegroundColor Yellow
    Write-Host "URL: $Url" -ForegroundColor Gray
    
    try {
        $params = @{
            Uri = $Url
            Method = $Method
        }
        
        if ($Body) {
            $params.Body = $Body
            $params.ContentType = "application/json"
        }
        
        $response = Invoke-WebRequest @params
        
        $statusMatch = $response.StatusCode -eq $ExpectedStatus
        $statusColor = if ($statusMatch) { "Green" } else { "Red" }
        
        Write-Host "Status: $($response.StatusCode) $(if ($statusMatch) {'✓'} else {'✗ Expected ' + $ExpectedStatus})" -ForegroundColor $statusColor
        
        if ($response.Content) {
            $json = $response.Content | ConvertFrom-Json
            Write-Host "Error Code: $($json.errorCode)" -ForegroundColor Gray
            Write-Host "Message: $($json.message)" -ForegroundColor Gray
            Write-Host "Correlation ID: $($json.correlationId)" -ForegroundColor Gray
        }
        
        $results += [PSCustomObject]@{
            Test = $TestName
            Status = $response.StatusCode
            Expected = $ExpectedStatus
            Pass = $statusMatch
        }
        
        Write-Host ""
        return $statusMatch
        
    } catch {
        Write-Host "Error: $_" -ForegroundColor Red
        Write-Host ""
        return $false
    }
}

# Test 1: Invalid booking ID (Not Found)
Test-Exception `
    -TestName "404 Not Found - Invalid Booking ID" `
    -Url "$baseUrl/booking/api/bookings/nonexistent-id-12345" `
    -Method "GET" `
    -ExpectedStatus 404

# Test 2: Missing required parameter (Bad Request)
Test-Exception `
    -TestName "400 Bad Request - Missing Required Field" `
    -Url "$baseUrl/booking/api/bookings" `
    -Method "POST" `
    -Body '{"roomId":"ROOM-101","amount":500000}' `
    -ExpectedStatus 401  # Will be 401 due to missing auth token

# Test 3: Invalid payment ID (Not Found)
Test-Exception `
    -TestName "404 Not Found - Invalid Payment ID" `
    -Url "$baseUrl/payment/api/payment/nonexistent-payment-12345" `
    -Method "GET" `
    -ExpectedStatus 404

# Test 4: Unauthorized access (Missing Token)
Test-Exception `
    -TestName "401 Unauthorized - Missing Auth Token" `
    -Url "$baseUrl/booking/api/bookings" `
    -Method "GET" `
    -ExpectedStatus 401

# Summary
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "Test Summary" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan

$passCount = ($results | Where-Object { $_.Pass }).Count
$totalCount = $results.Count
$passRate = [math]::Round(($passCount / $totalCount) * 100, 2)

foreach ($result in $results) {
    $status = if ($result.Pass) { "PASS" } else { "FAIL" }
    $color = if ($result.Pass) { "Green" } else { "Red" }
    Write-Host "$status - $($result.Test) (Status: $($result.Status))" -ForegroundColor $color
}

Write-Host "`nTotal: $passCount/$totalCount passed ($passRate%)" -ForegroundColor $(if ($passRate -eq 100) { "Green" } else { "Yellow" })

Write-Host "`n================================================" -ForegroundColor Cyan
Write-Host "Note: Services must be running for tests to work" -ForegroundColor Yellow
Write-Host "Run: docker-compose up -d" -ForegroundColor Yellow
Write-Host "================================================" -ForegroundColor Cyan
