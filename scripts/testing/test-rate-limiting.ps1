# Rate Limiting Test Suite - Simplified Version# Rate Limiting Test Suite

Write-Host "Rate Limiting Test Suite" -ForegroundColor Cyan# Tests all rate limiting policies implemented in the API Gateway

Write-Host "=========================" -ForegroundColor Cyan

Write-Host "========================================" -ForegroundColor Cyan

$gatewayUrl = "http://localhost:5000"Write-Host "   Rate Limiting Test Suite" -ForegroundColor Cyan

$results = @()Write-Host "========================================" -ForegroundColor Cyan

Write-Host ""

# Test 1: Global Rate Limit

Write-Host "`nTest 1: Sending 105 requests to health endpoint..." -ForegroundColor Yellow$gatewayUrl = "http://localhost:5000"

$success = 0$testResults = @()

$limited = 0

# Helper function to test rate limit

for ($i = 1; $i -le 105; $i++) {function Test-RateLimit {

    $resp = Invoke-WebRequest -Uri "$gatewayUrl/health" -SkipHttpErrorCheck -TimeoutSec 5    param(

    if ($resp.StatusCode -eq 200) { $success++ }        [string]$TestName,

    if ($resp.StatusCode -eq 429) { $limited++; if ($limited -eq 1) { Write-Host "Rate limited at request $i" -ForegroundColor Red } }        [string]$Url,

    Start-Sleep -Milliseconds 30        [string]$Method = "GET",

}        [object]$Body = $null,

        [hashtable]$Headers = @{},

Write-Host "  Success: $success, Rate Limited: $limited" -ForegroundColor $(if ($limited -gt 0) {"Green"} else {"Red"})        [int]$RequestCount = 10,

$results += [PSCustomObject]@{Test="Global Limit"; Status=if ($limited -gt 0) {"PASS"} else {"FAIL"}}        [int]$ExpectedLimit = 0

    )

# Test 2: Wait and reset    

Write-Host "`nWaiting 65 seconds for rate limit reset..." -ForegroundColor Yellow    Write-Host "`n[TEST] $TestName" -ForegroundColor Yellow

Start-Sleep -Seconds 65    Write-Host "URL: $Url" -ForegroundColor Gray

    Write-Host "Method: $Method | Requests: $RequestCount | Expected Limit: $ExpectedLimit" -ForegroundColor Gray

# Test 3: Rate limit headers    Write-Host "----------------------------------------" -ForegroundColor Gray

Write-Host "`nTest 2: Checking rate limit headers..." -ForegroundColor Yellow    

$resp = Invoke-WebRequest -Uri "$gatewayUrl/health" -SkipHttpErrorCheck    $successCount = 0

$hasHeaders = $resp.Headers["X-RateLimit-Remaining"] -ne $null    $rateLimitedCount = 0

    $firstRateLimitAt = 0

Write-Host "  Has X-RateLimit-Remaining: $hasHeaders" -ForegroundColor $(if ($hasHeaders) {"Green"} else {"Red"})    $rateLimitHeaders = @{}

$results += [PSCustomObject]@{Test="Rate Limit Headers"; Status=if ($hasHeaders) {"PASS"} else {"FAIL"}}    

    for ($i = 1; $i -le $RequestCount; $i++) {

# Summary        try {

Write-Host "`nTest Summary:" -ForegroundColor Cyan            $params = @{

$results | Format-Table                Uri = $Url

                Method = $Method

$passed = ($results | Where-Object {$_.Status -eq "PASS"}).Count                SkipHttpErrorCheck = $true

Write-Host "Passed: $passed/$($results.Count)" -ForegroundColor $(if ($passed -eq $results.Count) {"Green"} else {"Red"})                TimeoutSec = 5

            }

if ($passed -eq $results.Count) { exit 0 } else { exit 1 }            

            if ($Headers.Count -gt 0) {
                $params.Headers = $Headers
            }
            
            if ($Body -ne $null) {
                $params.Body = ($Body | ConvertTo-Json)
                $params.ContentType = "application/json"
            }
            
            $response = Invoke-WebRequest @params
            
            # Capture rate limit headers
            if ($response.Headers["X-RateLimit-Limit"]) {
                $rateLimitHeaders["Limit"] = $response.Headers["X-RateLimit-Limit"]
            }
            if ($response.Headers["X-RateLimit-Remaining"]) {
                $rateLimitHeaders["Remaining"] = $response.Headers["X-RateLimit-Remaining"]
            }
            if ($response.Headers["X-RateLimit-Policy"]) {
                $rateLimitHeaders["Policy"] = $response.Headers["X-RateLimit-Policy"]
            }
            
            if ($response.StatusCode -eq 429) {
                $rateLimitedCount++
                if ($firstRateLimitAt -eq 0) {
                    $firstRateLimitAt = $i
                }
                
                $retryAfter = $response.Headers["Retry-After"]
                Write-Host "  Request #$i : 429 TOO MANY REQUESTS" -ForegroundColor Red -NoNewline
                if ($retryAfter) {
                    Write-Host " (Retry after: $retryAfter seconds)" -ForegroundColor DarkRed
                } else {
                    Write-Host ""
                }
            } else {
                $successCount++
                $remaining = $rateLimitHeaders["Remaining"]
                Write-Host "  Request #$i : $($response.StatusCode) SUCCESS" -ForegroundColor Green -NoNewline
                if ($remaining) {
                    Write-Host " (Remaining: $remaining)" -ForegroundColor DarkGreen
                } else {
                    Write-Host ""
                }
            }
        }
        catch {
            Write-Host "  Request #$i : ERROR - $($_.Exception.Message)" -ForegroundColor Red
        }
        
        # Small delay to prevent overwhelming the server
        Start-Sleep -Milliseconds 50
    }
    
    Write-Host "`nResults:" -ForegroundColor Cyan
    Write-Host "  ✅ Successful: $successCount" -ForegroundColor Green
    Write-Host "  ❌ Rate Limited: $rateLimitedCount" -ForegroundColor Red
    
    if ($rateLimitHeaders["Limit"]) {
        Write-Host "  📊 Rate Limit: $($rateLimitHeaders["Limit"])" -ForegroundColor Cyan
    }
    if ($rateLimitHeaders["Policy"]) {
        Write-Host "  🎯 Policy: $($rateLimitHeaders["Policy"])" -ForegroundColor Cyan
    }
    if ($firstRateLimitAt -gt 0) {
        Write-Host "  🚦 First rate limit at request: $firstRateLimitAt" -ForegroundColor Yellow
    }
    
    # Validate results
    $passed = $true
    $message = ""
    
    if ($ExpectedLimit -gt 0) {
        if ($firstRateLimitAt -eq $ExpectedLimit + 1) {
            Write-Host "`n  ✓ TEST PASSED: Rate limit applied at expected threshold" -ForegroundColor Green
            $message = "PASSED"
        } elseif ($rateLimitedCount -gt 0) {
            Write-Host "`n  ⚠ TEST PARTIAL: Rate limit applied but at different threshold (expected: $ExpectedLimit, actual: $firstRateLimitAt)" -ForegroundColor Yellow
            $message = "PARTIAL"
            $passed = $false
        } else {
            Write-Host "`n  ✗ TEST FAILED: No rate limiting detected" -ForegroundColor Red
            $message = "FAILED"
            $passed = $false
        }
    } else {
        if ($rateLimitedCount -gt 0) {
            Write-Host "`n  ✓ TEST PASSED: Rate limiting is working" -ForegroundColor Green
            $message = "PASSED"
        } else {
            Write-Host "`n  ℹ TEST INFO: No rate limit hit in $RequestCount requests" -ForegroundColor Cyan
            $message = "INFO"
        }
    }
    
    # Add to results
    $script:testResults += [PSCustomObject]@{
        TestName = $TestName
        Success = $successCount
        RateLimited = $rateLimitedCount
        FirstLimitAt = $firstRateLimitAt
        Status = $message
        Passed = $passed
    }
    
    return $passed
}

# ===== TEST 1: Global Rate Limit =====
# Should allow 100 requests per minute per IP
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "TEST 1: Global Rate Limit (100/min)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

Test-RateLimit `
    -TestName "Global Rate Limit - Health Endpoint" `
    -Url "$gatewayUrl/health" `
    -RequestCount 105 `
    -ExpectedLimit 100

# ===== TEST 2: Auth Rate Limit =====
# Should allow only 5 attempts per 5 minutes
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "TEST 2: Auth Rate Limit (5/5min)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$loginBody = @{
    email = "test@test.com"
    password = "wrongpassword123"
}

Test-RateLimit `
    -TestName "Auth Rate Limit - Login Endpoint" `
    -Url "$gatewayUrl/api/users/login" `
    -Method "POST" `
    -Body $loginBody `
    -RequestCount 7 `
    -ExpectedLimit 5

# ===== TEST 3: Check Headers =====
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "TEST 3: Rate Limit Headers" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

try {
    $response = Invoke-WebRequest -Uri "$gatewayUrl/health" -SkipHttpErrorCheck
    
    Write-Host "`nResponse Headers:" -ForegroundColor Yellow
    
    $expectedHeaders = @(
        "X-RateLimit-Limit",
        "X-RateLimit-Remaining",
        "X-RateLimit-Policy"
    )
    
    $headersPassed = $true
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
        Write-Host "`n  ✓ All rate limit headers present" -ForegroundColor Green
    } else {
        Write-Host "`n  ✗ Some rate limit headers missing" -ForegroundColor Red
    }
    
    $script:testResults += [PSCustomObject]@{
        TestName = "Rate Limit Headers"
        Success = 1
        RateLimited = 0
        FirstLimitAt = 0
        Status = if ($headersPassed) { "PASSED" } else { "FAILED" }
        Passed = $headersPassed
    }
}
catch {
    Write-Host "  ✗ Error checking headers: $($_.Exception.Message)" -ForegroundColor Red
    $script:testResults += [PSCustomObject]@{
        TestName = "Rate Limit Headers"
        Success = 0
        RateLimited = 0
        FirstLimitAt = 0
        Status = "ERROR"
        Passed = $false
    }
}

# ===== TEST 4: 429 Response Format =====
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "TEST 4: 429 Response Format" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

Write-Host "`nSending requests to trigger rate limit..." -ForegroundColor Yellow

# Send enough requests to trigger rate limit
for ($i = 1; $i -le 105; $i++) {
    try {
        $response = Invoke-WebRequest -Uri "$gatewayUrl/health" -SkipHttpErrorCheck
        
        if ($response.StatusCode -eq 429) {
            Write-Host "`n429 Response received at request #$i" -ForegroundColor Yellow
            
            # Parse response body
            try {
                $body = $response.Content | ConvertFrom-Json
                
                Write-Host "`nResponse Body:" -ForegroundColor Yellow
                Write-Host "  Error: $($body.error)" -ForegroundColor Gray
                Write-Host "  Message: $($body.message)" -ForegroundColor Gray
                Write-Host "  Retry After: $($body.retryAfter) seconds" -ForegroundColor Gray
                Write-Host "  Endpoint: $($body.endpoint)" -ForegroundColor Gray
                Write-Host "  Timestamp: $($body.timestamp)" -ForegroundColor Gray
                
                # Validate response format
                $formatValid = ($body.error -and $body.message -and $body.retryAfter)
                
                if ($formatValid) {
                    Write-Host "`n  ✓ 429 Response format is correct" -ForegroundColor Green
                    $script:testResults += [PSCustomObject]@{
                        TestName = "429 Response Format"
                        Success = 1
                        RateLimited = 1
                        FirstLimitAt = $i
                        Status = "PASSED"
                        Passed = $true
                    }
                } else {
                    Write-Host "`n  ✗ 429 Response format is incomplete" -ForegroundColor Red
                    $script:testResults += [PSCustomObject]@{
                        TestName = "429 Response Format"
                        Success = 0
                        RateLimited = 1
                        FirstLimitAt = $i
                        Status = "FAILED"
                        Passed = $false
                    }
                }
            }
            catch {
                Write-Host "`n  ✗ Error parsing 429 response: $($_.Exception.Message)" -ForegroundColor Red
                $script:testResults += [PSCustomObject]@{
                    TestName = "429 Response Format"
                    Success = 0
                    RateLimited = 1
                    FirstLimitAt = $i
                    Status = "ERROR"
                    Passed = $false
                }
            }
            
            break
        }
    }
    catch {
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# ===== SUMMARY =====
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "   TEST SUMMARY" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$testResults | Format-Table -Property TestName, Success, RateLimited, FirstLimitAt, Status -AutoSize

$passedCount = ($testResults | Where-Object { $_.Passed -eq $true }).Count
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
