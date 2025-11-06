# Rate Limiting Test Suite
# Tests all rate limiting policies implemented in the API Gateway

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Rate Limiting Test Suite" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$gatewayUrl = "http://localhost:5000"
$testResults = @()

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host 'TEST 1: Global Rate Limit' -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

Write-Host "`nSending 105 requests to test global rate limit..." -ForegroundColor Yellow

$successCount = 0
$rateLimitedCount = 0

for ($i = 1; $i -le 105; $i++) {
    try {
        $response = Invoke-WebRequest -Uri "$gatewayUrl/health" -SkipHttpErrorCheck -TimeoutSec 5
        
        if ($response.StatusCode -eq 429) {
            $rateLimitedCount++
            if ($rateLimitedCount -eq 1) {
                Write-Host "`n✗ Rate limited at request #$i" -ForegroundColor Red
            }
        } elseif ($response.StatusCode -eq 200) {
            $successCount++
            if ($i % 20 -eq 0) {
                Write-Host "  Sent $i requests..." -ForegroundColor Gray
            }
        }
    }
    catch {
        Write-Host "  Error at request #$i : $($_.Exception.Message)" -ForegroundColor Red
    }
    
    Start-Sleep -Milliseconds 50
}

Write-Host "`nResults:" -ForegroundColor Cyan
Write-Host "  ✅ Successful: $successCount" -ForegroundColor Green
Write-Host "  ❌ Rate Limited: $rateLimitedCount" -ForegroundColor Red

if ($rateLimitedCount -gt 0) {
    Write-Host "  ✓ Global rate limit is working!" -ForegroundColor Green
    $testResults += [PSCustomObject]@{
        TestName = "Global Rate Limit"
        Status = "PASSED"
        Success = $successCount
        RateLimited = $rateLimitedCount
    }
} else {
    Write-Host "  ✗ No rate limiting detected" -ForegroundColor Red
    $testResults += [PSCustomObject]@{
        TestName = "Global Rate Limit"
        Status = "FAILED"
        Success = $successCount
        RateLimited = $rateLimitedCount
    }
}

# Wait for rate limit to reset
Write-Host "`nWaiting 65 seconds for rate limit to reset..." -ForegroundColor Yellow
Start-Sleep -Seconds 65

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host 'TEST 2: Auth Rate Limit' -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

Write-Host "`nSending 7 login attempts..." -ForegroundColor Yellow

$authSuccessCount = 0
$authBlockedCount = 0

for ($i = 1; $i -le 7; $i++) {
    try {
        $body = @{
            email = "test@test.com"
            password = "wrongpassword123"
        } | ConvertTo-Json
        
        $response = Invoke-WebRequest `
            -Uri "$gatewayUrl/api/users/login" `
            -Method POST `
            -Body $body `
            -ContentType "application/json" `
            -SkipHttpErrorCheck `
            -TimeoutSec 5
        
        if ($response.StatusCode -eq 429) {
            $authBlockedCount++
            Write-Host "  Attempt #$i : 429 RATE LIMITED" -ForegroundColor Red
        } elseif ($response.StatusCode -eq 401) {
            $authSuccessCount++
            Write-Host "  Attempt #$i : 401 UNAUTHORIZED (not rate limited)" -ForegroundColor Yellow
        } else {
            Write-Host "  Attempt #$i : $($response.StatusCode)" -ForegroundColor Gray
        }
    }
    catch {
        Write-Host "  Attempt #$i : ERROR - $($_.Exception.Message)" -ForegroundColor Red
    }
    
    Start-Sleep -Seconds 1
}

Write-Host "`nResults:" -ForegroundColor Cyan
Write-Host "  ✅ Attempts before limit: $authSuccessCount" -ForegroundColor Green
Write-Host "  ❌ Attempts blocked: $authBlockedCount" -ForegroundColor Red

if ($authBlockedCount -gt 0) {
    Write-Host "  ✓ Auth rate limit is working!" -ForegroundColor Green
    $testResults += [PSCustomObject]@{
        TestName = "Auth Rate Limit"
        Status = "PASSED"
        Success = $authSuccessCount
        RateLimited = $authBlockedCount
    }
} else {
    Write-Host "  ✗ No auth rate limiting detected" -ForegroundColor Red
    $testResults += [PSCustomObject]@{
        TestName = "Auth Rate Limit"
        Status = "FAILED"
        Success = $authSuccessCount
        RateLimited = $authBlockedCount
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "TEST 3: Rate Limit Headers" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

try {
    $response = Invoke-WebRequest -Uri "$gatewayUrl/health" -SkipHttpErrorCheck
    
    Write-Host "`nChecking response headers:" -ForegroundColor Yellow
    
    $headersPassed = $true
    $expectedHeaders = @("X-RateLimit-Remaining")
    
    foreach ($header in $expectedHeaders) {
        $value = $response.Headers[$header]
        if ($value) {
            Write-Host "  ✓ $header : $value" -ForegroundColor Green
        } else {
            Write-Host "  ✗ $header : Not found" -ForegroundColor Red
            $headersPassed = $false
        }
    }
    
    if ($headersPassed) {
        Write-Host "`n  ✓ Rate limit headers present" -ForegroundColor Green
        $testResults += [PSCustomObject]@{
            TestName = "Rate Limit Headers"
            Status = "PASSED"
            Success = 1
            RateLimited = 0
        }
    } else {
        Write-Host "`n  ✗ Some headers missing" -ForegroundColor Red
        $testResults += [PSCustomObject]@{
            TestName = "Rate Limit Headers"
            Status = "FAILED"
            Success = 0
            RateLimited = 0
        }
    }
}
catch {
    Write-Host "  ✗ Error checking headers: $($_.Exception.Message)" -ForegroundColor Red
    $testResults += [PSCustomObject]@{
        TestName = "Rate Limit Headers"
        Status = "ERROR"
        Success = 0
        RateLimited = 0
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "   TEST SUMMARY" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$testResults | Format-Table -Property TestName, Status, Success, RateLimited -AutoSize

$passedCount = ($testResults | Where-Object { $_.Status -eq "PASSED" }).Count
$totalCount = $testResults.Count

Write-Host "`nOverall Results:" -ForegroundColor Cyan
Write-Host "  Total Tests: $totalCount" -ForegroundColor White
Write-Host "  Passed: $passedCount" -ForegroundColor Green
Write-Host "  Failed: $($totalCount - $passedCount)" -ForegroundColor Red

if ($passedCount -eq $totalCount) {
    Write-Host "`n✓ ALL TESTS PASSED!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "`n✗ SOME TESTS FAILED" -ForegroundColor Red
    exit 1
}
