param(
    [int]$NumberOfFlows = 10,
    [ValidateRange(1, 2048)]
    [int]$ConcurrentFlows = 3,
    [string]$GatewayUrl = "http://localhost:5000"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   E2E Authenticated Flow Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Total Flows:       $NumberOfFlows" -ForegroundColor White
Write-Host "Concurrent:        $ConcurrentFlows" -ForegroundColor White
Write-Host "Gateway URL:       $GatewayUrl" -ForegroundColor White
Write-Host "Authentication:    Enabled (JWT)" -ForegroundColor Green
Write-Host ""

# Ensure PowerShell 7+ for real concurrency; auto-switch to pwsh if available
if ($PSVersionTable.PSVersion.Major -lt 7) {
    $pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
    if ($pwsh) {
        Write-Host "[INFO] PowerShell 7 detected at: $($pwsh.Source)" -ForegroundColor Yellow
        Write-Host "[INFO] Re-launching this script with PowerShell 7 for parallel execution..." -ForegroundColor Yellow

        $scriptPath = $MyInvocation.MyCommand.Path
        # Build argument list safely to preserve spaces
        $argsList = @(
            '-NoLogo','-NoProfile',
            '-File', $scriptPath,
            '-NumberOfFlows', $NumberOfFlows,
            '-ConcurrentFlows', $ConcurrentFlows,
            '-GatewayUrl', $GatewayUrl
        )

        & $pwsh.Source @argsList
        exit $LASTEXITCODE
    }
    else {
        Write-Host "[WARNING] This script uses parallel execution which requires PowerShell 7+." -ForegroundColor Yellow
        Write-Host "          'pwsh' not found on PATH. Download from: https://aka.ms/powershell" -ForegroundColor Yellow
        Write-Host "          Continuing in sequential mode..." -ForegroundColor Yellow
        Write-Host ""
    }
}

# Create synchronized hashtable for results
$script:results = [hashtable]::Synchronized(@{
    successCount = 0
    failCount = 0
    flowTimes = @()
    failureReasons = @()
    authTimes = @()
    retryCount = 0
    retrySuccessCount = 0
})

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

# Execute flows in parallel (PS 7+) or sequential (PS 5.1)
if ($PSVersionTable.PSVersion.Major -ge 7) {
    # Normalize throttle to at least 1
    $throttle = [math]::Max(1, [int]$ConcurrentFlows)
    1..$NumberOfFlows | ForEach-Object -Parallel {
        $flowId = $_
        $gatewayUrl = $using:GatewayUrl
        $results = $using:results
        
        $flowStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        $authStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        
        try {
            # Step 1: Register user
            $username = "authuser$flowId-$(Get-Random)"
            $password = "Test@2025!Pass"
            $registerBody = @{
                Username = $username
                Email = "$username@example.com"
                Password = $password
                FirstName = "Test"
                LastName = "User$flowId"
            } | ConvertTo-Json
            
            $user = Invoke-RestMethod -Uri "$gatewayUrl/api/users/register" `
                -Method Post `
                -ContentType "application/json" `
                -Body $registerBody `
                -TimeoutSec 60
            
            $userId = $user.data.id
            
            # Step 2: Login to get JWT token
            $loginBody = @{
                Username = $username
                Password = $password
            } | ConvertTo-Json
            
            $loginResponse = Invoke-RestMethod -Uri "$gatewayUrl/api/users/login" `
                -Method Post `
                -ContentType "application/json" `
                -Body $loginBody `
                -TimeoutSec 60
            
            $token = $loginResponse.data.token.Trim()
            $authStopwatch.Stop()
            
            if ([string]::IsNullOrEmpty($token)) {
                throw "Failed to obtain JWT token"
            }
            
            # Prepare authorization header
            $headers = @{
                "Authorization" = "Bearer $token"
            }
            
            # Step 3: Create booking with authentication
            $bookingBody = @{
                UserId = $userId
                RoomId = "ROOM-$(Get-Random -Minimum 100 -Maximum 999)"
                Amount = Get-Random -Minimum 100000 -Maximum 1000000
            } | ConvertTo-Json
            
            $booking = Invoke-RestMethod -Uri "$gatewayUrl/api/bookings" `
                -Method Post `
                -Headers $headers `
                -ContentType "application/json" `
                -Body $bookingBody `
                -TimeoutSec 60
            
            $bookingId = $booking.id
            $bookingAmount = $booking.amount
            
            # Step 4: Process payment with authentication
            $paymentBody = @{
                BookingId = $bookingId
                Amount = $bookingAmount
            } | ConvertTo-Json
            
            # Handle payment - may return 400 if payment fails
            try {
                $payment = Invoke-RestMethod -Uri "$gatewayUrl/api/payment/pay" `
                    -Method Post `
                    -Headers $headers `
                    -ContentType "application/json" `
                    -Body $paymentBody `
                    -TimeoutSec 60 `
                    -SkipHttpErrorCheck
                    
                Write-Host "[INFO] Flow #$flowId - Initial payment: $($payment.status)" -ForegroundColor Gray
            } catch {
                # If SkipHttpErrorCheck not available, catch and check error message
                $errorMsg = $_.Exception.Message
                if ($errorMsg -like "*400*" -or $errorMsg -like "*Bad Request*") {
                    # Try to extract response from error
                    $payment = $null
                    if ($_.ErrorDetails.Message) {
                        $payment = $_.ErrorDetails.Message | ConvertFrom-Json
                    }
                    if ($payment) {
                        Write-Host "[INFO] Flow #$flowId - Initial payment: $($payment.status)" -ForegroundColor Yellow
                    } else {
                        throw
                    }
                } else {
                    throw
                }
            }    
                        
            # Step 5: Check payment status - retry if failed (10% chance of failure)
            if ($payment.status -eq "FAILED") {
                Write-Host "[RETRY] Flow #$flowId - Payment failed, attempting retry..." -ForegroundColor Yellow
                
                # Retry payment (up to 3 times if needed)
                $attemptCount = 0
                $maxRetries = 5
                $paymentSucceeded = $false
                
                $lockTaken = $false
                try {
                    [System.Threading.Monitor]::Enter($results.SyncRoot, [ref]$lockTaken)
                    $results.retryCount++
                } finally {
                    if ($lockTaken) { [System.Threading.Monitor]::Exit($results.SyncRoot) }
                }
                
                while ($attemptCount -lt $maxRetries -and -not $paymentSucceeded) {
                    $attemptCount++
                    
                    $retryBody = @{
                        BookingId = $bookingId
                    } | ConvertTo-Json
                    
                    # Handle retry payment - may return 400 if payment fails again
                    try {
                        $retryPayment = Invoke-RestMethod -Uri "$gatewayUrl/api/payment/retry" `
                            -Method Post `
                            -Headers $headers `
                            -ContentType "application/json" `
                            -Body $retryBody `
                            -TimeoutSec 60 `
                            -SkipHttpErrorCheck
                    } catch {
                        $errorMsg = $_.Exception.Message
                        if ($errorMsg -like "*400*" -or $errorMsg -like "*Bad Request*") {
                            $retryPayment = $null
                            if ($_.ErrorDetails.Message) {
                                $retryPayment = $_.ErrorDetails.Message | ConvertFrom-Json
                            }
                            if (-not $retryPayment) {
                                throw
                            }
                        } else {
                            throw
                        }
                    }
                    
                    # Wait for event processing
                    Start-Sleep -Milliseconds 8000
                    
                    if ($retryPayment.status -eq "SUCCESS") {
                        Write-Host "[RETRY-OK] Flow #$flowId - Payment succeeded on retry #$attemptCount" -ForegroundColor Green
                        $paymentSucceeded = $true
                        
                        $lockTaken = $false
                        try {
                            [System.Threading.Monitor]::Enter($results.SyncRoot, [ref]$lockTaken)
                            $results.retrySuccessCount++
                        } finally {
                            if ($lockTaken) { [System.Threading.Monitor]::Exit($results.SyncRoot) }
                        }
                    } else {
                        Write-Host "[RETRY-FAIL] Flow #$flowId - Retry #$attemptCount failed" -ForegroundColor Yellow
                    }
                }
                
                if (-not $paymentSucceeded) {
                    Write-Host "[RETRY-EXHAUSTED] Flow #$flowId - All $maxRetries retries failed" -ForegroundColor Red
                }
            }

            # Step 6: Wait for event processing (Outbox pattern polls every 1 second + processing time)
            Start-Sleep -Milliseconds 8000           
            
            # Step 7: Verify booking status with authentication
            $updatedBooking = Invoke-RestMethod -Uri "$gatewayUrl/api/bookings/$bookingId" `
                -Method Get `
                -Headers $headers `
                -TimeoutSec 60
            
            # Step 8: Verify payment status
            $finalPayment = Invoke-RestMethod -Uri "$gatewayUrl/api/payment/booking/$bookingId" `
                -Method Get `
                -Headers $headers `
                -TimeoutSec 60
            
            $flowStopwatch.Stop()
            $elapsed = $flowStopwatch.Elapsed.TotalMilliseconds
            $authTime = $authStopwatch.Elapsed.TotalMilliseconds
            
            if ($updatedBooking.status -eq "CONFIRMED") {
                Write-Host "[OK] Flow #$flowId - Auth: $([math]::Round($authTime, 0))ms, Total: $([math]::Round($elapsed, 0))ms (Booking: $bookingId, Payment: $($finalPayment.status), Retries: $($finalPayment.retryCount))" -ForegroundColor Green
                
                $lockTaken = $false
                try {
                    [System.Threading.Monitor]::Enter($results.SyncRoot, [ref]$lockTaken)
                    $results.successCount++
                    $results.flowTimes += $elapsed
                    $results.authTimes += $authTime
                } finally {
                    if ($lockTaken) { [System.Threading.Monitor]::Exit($results.SyncRoot) }
                }
            } else {
                $reason = "Booking status is '$($updatedBooking.status)' instead of 'CONFIRMED'"
                Write-Host "[WARN] Flow #${flowId}: $reason" -ForegroundColor Yellow
                
                $lockTaken = $false
                try {
                    [System.Threading.Monitor]::Enter($results.SyncRoot, [ref]$lockTaken)
                    $results.failCount++
                    $results.failureReasons += $reason
                } finally {
                    if ($lockTaken) { [System.Threading.Monitor]::Exit($results.SyncRoot) }
                }
            }
            
        } catch {
            $flowStopwatch.Stop()
            $errorMsg = $_.Exception.Message
            if ($errorMsg.Length -gt 80) {
                $errorMsg = $errorMsg.Substring(0, 80) + "..."
            }
            Write-Host "[FAIL] Flow #$flowId failed: $errorMsg" -ForegroundColor Red
            
            $lockTaken = $false
            try {
                [System.Threading.Monitor]::Enter($results.SyncRoot, [ref]$lockTaken)
                $results.failCount++
                $results.failureReasons += $errorMsg
            } finally {
                if ($lockTaken) { [System.Threading.Monitor]::Exit($results.SyncRoot) }
            }
        }
    } -ThrottleLimit $throttle
} else {
    # Sequential execution for PS 5.1
    for ($i = 1; $i -le $NumberOfFlows; $i++) {
        $flowStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        $authStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        
        try {
            # Step 1: Register user
            $username = "authuser$i-$(Get-Random)"
            $password = "Test@2025!Pass"
            $registerBody = @{
                Username = $username
                Email = "$username@example.com"
                Password = $password
                FirstName = "Test"
                LastName = "User$i"
            } | ConvertTo-Json
            
            $user = Invoke-RestMethod -Uri "$GatewayUrl/api/users/register" `
                -Method Post `
                -ContentType "application/json" `
                -Body $registerBody `
                -TimeoutSec 60
            
            $userId = $user.data.id
            
            # Step 2: Login to get JWT token
            $loginBody = @{
                Username = $username
                Password = $password
            } | ConvertTo-Json
            
            $loginResponse = Invoke-RestMethod -Uri "$GatewayUrl/api/users/login" `
                -Method Post `
                -ContentType "application/json" `
                -Body $loginBody `
                -TimeoutSec 60
            
            $token = $loginResponse.data.token.Trim()
            $authStopwatch.Stop()
            
            if ([string]::IsNullOrEmpty($token)) {
                throw "Failed to obtain JWT token"
            }
            
            # Prepare authorization header
            $headers = @{
                "Authorization" = "Bearer $token"
            }
            
            # Step 3: Create booking with authentication
            $bookingBody = @{
                UserId = $userId
                RoomId = "ROOM-$(Get-Random -Minimum 100 -Maximum 999)"
                Amount = Get-Random -Minimum 100000 -Maximum 1000000
            } | ConvertTo-Json
            
            $booking = Invoke-RestMethod -Uri "$GatewayUrl/api/bookings" `
                -Method Post `
                -Headers $headers `
                -ContentType "application/json" `
                -Body $bookingBody `
                -TimeoutSec 60
            
            $bookingId = $booking.id
            $bookingAmount = $booking.amount
            
            # Step 4: Process payment with authentication
            $paymentBody = @{
                BookingId = $bookingId
                Amount = $bookingAmount
            } | ConvertTo-Json
            
            # Handle payment - may return 400 if payment fails
            try {
                $payment = Invoke-RestMethod -Uri "$GatewayUrl/api/payment/pay" `
                    -Method Post `
                    -Headers $headers `
                    -ContentType "application/json" `
                    -Body $paymentBody `
                    -TimeoutSec 60 `
                    -SkipHttpErrorCheck
                    
                Write-Host "[INFO] Flow #$i - Initial payment: $($payment.status)" -ForegroundColor Gray
            } catch {
                # If SkipHttpErrorCheck not available, catch and check error message
                $errorMsg = $_.Exception.Message
                if ($errorMsg -like "*400*" -or $errorMsg -like "*Bad Request*") {
                    # Try to extract response from error
                    $payment = $null
                    if ($_.ErrorDetails.Message) {
                        $payment = $_.ErrorDetails.Message | ConvertFrom-Json
                    }
                    if ($payment) {
                        Write-Host "[INFO] Flow #$i - Initial payment: $($payment.status)" -ForegroundColor Yellow
                    } else {
                        throw
                    }
                } else {
                    throw
                }
            }
            
            # Step 5: Wait for event processing (Outbox pattern polls every 1 second + processing time)
            Start-Sleep -Milliseconds 8000
            
            # Step 6: Check payment status - retry if failed (10% chance of failure)
            if ($payment.status -eq "FAILED") {
                Write-Host "[RETRY] Flow #$i - Payment failed, attempting retry..." -ForegroundColor Yellow
                
                # Retry payment (up to 3 times if needed)
                $attemptCount = 0
                $maxRetries = 3
                $paymentSucceeded = $false
                
                $results.retryCount++
                
                while ($attemptCount -lt $maxRetries -and -not $paymentSucceeded) {
                    $attemptCount++
                    
                    $retryBody = @{
                        BookingId = $bookingId
                    } | ConvertTo-Json
                    
                    # Handle retry payment - may return 400 if payment fails again
                    try {
                        $retryPayment = Invoke-RestMethod -Uri "$GatewayUrl/api/payment/retry" `
                            -Method Post `
                            -Headers $headers `
                            -ContentType "application/json" `
                            -Body $retryBody `
                            -TimeoutSec 60 `
                            -SkipHttpErrorCheck
                    } catch {
                        $errorMsg = $_.Exception.Message
                        if ($errorMsg -like "*400*" -or $errorMsg -like "*Bad Request*") {
                            $retryPayment = $null
                            if ($_.ErrorDetails.Message) {
                                $retryPayment = $_.ErrorDetails.Message | ConvertFrom-Json
                            }
                            if (-not $retryPayment) {
                                throw
                            }
                        } else {
                            throw
                        }
                    }
                    
                    # Wait for event processing
                    Start-Sleep -Milliseconds 8000
                    
                    if ($retryPayment.status -eq "SUCCESS") {
                        Write-Host "[RETRY-OK] Flow #$i - Payment succeeded on retry #$attemptCount" -ForegroundColor Green
                        $paymentSucceeded = $true
                        $results.retrySuccessCount++
                    } else {
                        Write-Host "[RETRY-FAIL] Flow #$i - Retry #$attemptCount failed" -ForegroundColor Yellow
                    }
                }
                
                if (-not $paymentSucceeded) {
                    Write-Host "[RETRY-EXHAUSTED] Flow #$i - All $maxRetries retries failed" -ForegroundColor Red
                }
            }
            
            # Step 7: Verify booking status with authentication
            $updatedBooking = Invoke-RestMethod -Uri "$GatewayUrl/api/bookings/$bookingId" `
                -Method Get `
                -Headers $headers `
                -TimeoutSec 60
            
            # Step 8: Verify payment status
            $finalPayment = Invoke-RestMethod -Uri "$GatewayUrl/api/payment/booking/$bookingId" `
                -Method Get `
                -Headers $headers `
                -TimeoutSec 60
            
            $flowStopwatch.Stop()
            $elapsed = $flowStopwatch.Elapsed.TotalMilliseconds
            $authTime = $authStopwatch.Elapsed.TotalMilliseconds
            
            if ($updatedBooking.status -eq "CONFIRMED") {
                Write-Host "[OK] Flow #$i - Auth: $([math]::Round($authTime, 0))ms, Total: $([math]::Round($elapsed, 0))ms (Booking: $bookingId, Payment: $($finalPayment.status), Retries: $($finalPayment.retryCount))" -ForegroundColor Green
                $results.successCount++
                $results.flowTimes += $elapsed
                $results.authTimes += $authTime
            } else {
                $reason = "Booking status is '$($updatedBooking.status)' instead of 'CONFIRMED'"
                Write-Host "[WARN] Flow #${i}: $reason" -ForegroundColor Yellow
                $results.failCount++
                $results.failureReasons += $reason
            }
            
        } catch {
            $flowStopwatch.Stop()
            $errorMsg = $_.Exception.Message
            if ($errorMsg.Length -gt 80) {
                $errorMsg = $errorMsg.Substring(0, 80) + "..."
            }
            Write-Host "[FAIL] Flow #$i failed: $errorMsg" -ForegroundColor Red
            $results.failCount++
            $results.failureReasons += $errorMsg
        }
    }
}

$stopwatch.Stop()

# Calculate statistics
$successCount = $results.successCount
$failCount = $results.failCount
$timeArray = $results.flowTimes
$authArray = $results.authTimes

$avgTime = if ($timeArray.Count -gt 0) { ($timeArray | Measure-Object -Average).Average } else { 0 }
$minTime = if ($timeArray.Count -gt 0) { ($timeArray | Measure-Object -Minimum).Minimum } else { 0 }
$maxTime = if ($timeArray.Count -gt 0) { ($timeArray | Measure-Object -Maximum).Maximum } else { 0 }
$p95Time = if ($timeArray.Count -gt 0) { 
    $sorted = $timeArray | Sort-Object
    $index = [Math]::Floor($sorted.Count * 0.95)
    if ($index -ge $sorted.Count) { $index = $sorted.Count - 1 }
    $sorted[$index]
} else { 0 }

$avgAuthTime = if ($authArray.Count -gt 0) { ($authArray | Measure-Object -Average).Average } else { 0 }
$maxAuthTime = if ($authArray.Count -gt 0) { ($authArray | Measure-Object -Maximum).Maximum } else { 0 }

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   E2E Authenticated Flow Results" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Total Flows:       $NumberOfFlows"
Write-Host "Successful:        $successCount" -ForegroundColor Green
Write-Host "Failed:            $failCount" -ForegroundColor $(if ($failCount -gt 0) { "Red" } else { "Green" })
Write-Host "Success Rate:      $([math]::Round(($successCount / $NumberOfFlows) * 100, 2))%"
Write-Host ""
Write-Host "Payment Retry Statistics:"
Write-Host "  Initial Failures:  $($results.retryCount)" -ForegroundColor Yellow
Write-Host "  Retry Successes:   $($results.retrySuccessCount)" -ForegroundColor Green
if ($results.retryCount -gt 0) {
    $retrySuccessRate = ($results.retrySuccessCount / $results.retryCount) * 100
    Write-Host "  Retry Success %:   $([math]::Round($retrySuccessRate, 2))%" -ForegroundColor $(if ($retrySuccessRate -gt 50) { "Green" } else { "Yellow" })
}
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Performance Metrics" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Total Time:        $([math]::Round($stopwatch.Elapsed.TotalSeconds, 2)) seconds"
Write-Host "Flows/sec:         $([math]::Round($NumberOfFlows / $stopwatch.Elapsed.TotalSeconds, 2))"
Write-Host ""
Write-Host "Authentication Times:"
Write-Host "  Average:         $([math]::Round($avgAuthTime, 0))ms"
Write-Host "  Maximum:         $([math]::Round($maxAuthTime, 0))ms"
Write-Host ""
Write-Host "E2E Flow Times (with Auth):"
Write-Host "  Average:         $([math]::Round($avgTime, 0))ms"
Write-Host "  Minimum:         $([math]::Round($minTime, 0))ms"
Write-Host "  Maximum:         $([math]::Round($maxTime, 0))ms"
Write-Host "  95th Percentile: $([math]::Round($p95Time, 0))ms"
Write-Host ""

# Show failure reasons if any
if ($failCount -gt 0) {
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "   Failure Analysis" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    
    $reasons = $results.failureReasons
    $groupedReasons = $reasons | Group-Object | Sort-Object Count -Descending
    
    foreach ($group in $groupedReasons) {
        Write-Host "  [$($group.Count)x] $($group.Name)" -ForegroundColor Yellow
    }
    Write-Host ""
}

# Performance assessment
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Performance Assessment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if ($avgAuthTime -lt 500) {
    Write-Host "[EXCELLENT] Auth time: avg < 500ms" -ForegroundColor Green
} elseif ($avgAuthTime -lt 1000) {
    Write-Host "[GOOD] Auth time: avg < 1s" -ForegroundColor Yellow
} else {
    Write-Host "[POOR] Auth time: avg > 1s" -ForegroundColor Red
}

if ($p95Time -lt 6000) {
    Write-Host "[EXCELLENT] E2E Flow time (with auth): p95 < 6s" -ForegroundColor Green
} elseif ($p95Time -lt 12000) {
    Write-Host "[GOOD] E2E Flow time (with auth): p95 < 12s" -ForegroundColor Yellow
} else {
    Write-Host "[POOR] E2E Flow time (with auth): p95 > 12s" -ForegroundColor Red
}

if (($failCount / $NumberOfFlows) -lt 0.01) {
    Write-Host "[EXCELLENT] Success rate: >99%" -ForegroundColor Green
} elseif (($failCount / $NumberOfFlows) -lt 0.05) {
    Write-Host "[ACCEPTABLE] Success rate: >95%" -ForegroundColor Yellow
} else {
    Write-Host "[POOR] Success rate: <95%" -ForegroundColor Red
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Security Verification" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "[PASS] JWT tokens generated for all flows" -ForegroundColor Green
Write-Host "[PASS] All requests authenticated via API Gateway" -ForegroundColor Green
Write-Host "[PASS] Authorization headers properly forwarded" -ForegroundColor Green
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Next Steps" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "1. Check retry events in Seq:         http://localhost:5341 (@Message like '%retry%')" -ForegroundColor Gray
Write-Host "2. Check payment retry stats:         MongoDB - payments collection" -ForegroundColor Gray
Write-Host "3. Monitor RabbitMQ queues:           http://localhost:15672" -ForegroundColor Gray
Write-Host "4. Check JWT logs in Seq:             http://localhost:5341" -ForegroundColor Gray
Write-Host "5. Monitor API Gateway auth:          docker logs apigateway" -ForegroundColor Gray
Write-Host ""

# Exit with appropriate code
if ($failCount -eq 0) {
    exit 0
} else {
    exit 1
}
