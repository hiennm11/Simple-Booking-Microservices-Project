# Rate Limiting Test Suite
Write-Host "Rate Limiting Test Suite" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan

$gatewayUrl = "http://localhost:5000"
$testResults = @()

# Test 1: Global Rate Limit
Write-Host "`nTest 1: Sending requests to health endpoint..." -ForegroundColor Yellow
$successCount = 0
$limitedCount = 0

for ($i = 1; $i -le 105; $i++) {
    try {
        $response = Invoke-WebRequest -Uri "$gatewayUrl/health" -TimeoutSec 5 -UseBasicParsing
        
        if ($response.StatusCode -eq 200) {
            $successCount = $successCount + 1
        }
    }
    catch {
        # Check if it's a 429 error
        if ($_.Exception.Response.StatusCode.Value__ -eq 429) {
            $limitedCount = $limitedCount + 1
            if ($limitedCount -eq 1) {
                Write-Host "  Rate limited at request $i" -ForegroundColor Red
            }
        } else {
            # Other error
            # Write-Host "  Error: $_" -ForegroundColor Red
        }
    }
    
    Start-Sleep -Milliseconds 30
}

Write-Host "  Success: $successCount" -ForegroundColor Green
Write-Host "  Limited: $limitedCount" -ForegroundColor Red

$test1Status = "FAIL"
if ($limitedCount -gt 0) {
    $test1Status = "PASS"
    Write-Host "  Result: PASSED - Rate limiting is working" -ForegroundColor Green
} else {
    Write-Host "  Result: FAILED - No rate limiting detected" -ForegroundColor Red
}

$testResults += @{Test="Global Limit"; Status=$test1Status; Success=$successCount; Limited=$limitedCount}

# Test 2: Check Headers
Write-Host "`nTest 2: Checking rate limit headers..." -ForegroundColor Yellow

Start-Sleep -Seconds 60

try {
    $response = Invoke-WebRequest -Uri "$gatewayUrl/health" -TimeoutSec 5 -UseBasicParsing
    $hasHeader = $false
    
    if ($response.Headers.ContainsKey("X-RateLimit-Remaining")) {
        $hasHeader = $true
        $value = $response.Headers["X-RateLimit-Remaining"]
        Write-Host "  X-RateLimit-Remaining: $value" -ForegroundColor Green
    }
    
    $test2Status = "FAIL"
    if ($hasHeader) {
        $test2Status = "PASS"
        Write-Host "  Result: PASSED - Headers present" -ForegroundColor Green
    } else {
        Write-Host "  Result: FAILED - Headers missing" -ForegroundColor Red
    }
    
    $testResults += @{Test="Rate Limit Headers"; Status=$test2Status}
}
catch {
    Write-Host "  Error: $_" -ForegroundColor Red
    $testResults += @{Test="Rate Limit Headers"; Status="ERROR"}
}

# Summary
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Test Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$passedCount = 0
foreach ($result in $testResults) {
    $testName = $result.Test
    $status = $result.Status
    $color = "Red"
    if ($status -eq "PASS") {
        $color = "Green"
        $passedCount = $passedCount + 1
    }
    Write-Host "$testName : $status" -ForegroundColor $color
}

Write-Host "`nTotal: $($testResults.Count)" -ForegroundColor White
Write-Host "Passed: $passedCount" -ForegroundColor Green
Write-Host "Failed: $($testResults.Count - $passedCount)" -ForegroundColor Red

if ($passedCount -eq $testResults.Count) {
    Write-Host "`nALL TESTS PASSED" -ForegroundColor Green
    exit 0
} else {
    Write-Host "`nSOME TESTS FAILED" -ForegroundColor Red
    exit 1
}
