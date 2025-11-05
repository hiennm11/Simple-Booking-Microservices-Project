param(
    [int]$NumberOfFlows = 50,
    [int]$ConcurrentFlows = 5,
    [string]$GatewayUrl = "http://localhost:5000",
    [string]$BookingUrl = "http://localhost:5002",
    [string]$PaymentUrl = "http://localhost:5003",
    [switch]$UseAuthentication = $false
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   End-to-End Flow Load Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Total Flows:       $NumberOfFlows" -ForegroundColor White
Write-Host "Concurrent:        $ConcurrentFlows" -ForegroundColor White
Write-Host "Gateway URL:       $GatewayUrl" -ForegroundColor White
Write-Host "Authentication:    $(if ($UseAuthentication) { 'Enabled' } else { 'Disabled' })" -ForegroundColor White
Write-Host ""

# Check PowerShell version
if ($PSVersionTable.PSVersion.Major -lt 7) {
    Write-Host "[WARNING] This script requires PowerShell 7+ for parallel execution." -ForegroundColor Yellow
    Write-Host "          Download from: https://aka.ms/powershell" -ForegroundColor Yellow
    Write-Host "          Running in sequential mode..." -ForegroundColor Yellow
    Write-Host ""
}

# Create synchronized hashtable for results
$script:results = [hashtable]::Synchronized(@{
    successCount = 0
    failCount = 0
    flowTimes = @()
    failureReasons = @()
})

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

# Execute flows in parallel (PS 7+) or sequential (PS 5.1)
if ($PSVersionTable.PSVersion.Major -ge 7) {
    1..$NumberOfFlows | ForEach-Object -Parallel {
        $flowId = $_
        $gatewayUrl = $using:GatewayUrl
        $bookingUrl = $using:BookingUrl
        $paymentUrl = $using:PaymentUrl
        $results = $using:results
    
    $flowStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    
    try {
        # Step 1: Register user
        $username = "user$flowId-$(Get-Random)"
        $registerBody = @{
            Username = $username
            Email = "$username@example.com"
            Password = "Test@2025!Pass"
            FirstName = "Test"
            LastName = "User$flowId"
        } | ConvertTo-Json
        
        $user = Invoke-RestMethod -Uri "$gatewayUrl/api/users/register" `
            -Method Post `
            -ContentType "application/json" `
            -Body $registerBody `
            -TimeoutSec 30
        
        # API returns wrapped response: { success, message, data: { id, username, ... } }
        $userId = $user.data.id
        
        # Step 1.5: Login to get JWT token (if authentication enabled)
        $token = $null
        $headers = @{}
        if ($using:UseAuthentication) {
            $loginBody = @{
                Username = $username
                Password = "Test@2025!Pass"
            } | ConvertTo-Json
            
            $loginResponse = Invoke-RestMethod -Uri "$gatewayUrl/api/users/login" `
                -Method Post `
                -ContentType "application/json" `
                -Body $loginBody `
                -TimeoutSec 30
            
            $token = $loginResponse.data.token.Trim()
            $headers = @{
                "Authorization" = "Bearer $token"
            }
        }
        
        # Step 2: Create booking
        $bookingBody = @{
            UserId = $userId
            RoomId = "ROOM-$(Get-Random -Minimum 100 -Maximum 999)"
            Amount = Get-Random -Minimum 100000 -Maximum 1000000
        } | ConvertTo-Json
        
        $bookingParams = @{
            Uri = "$bookingUrl/api/bookings"
            Method = "Post"
            ContentType = "application/json"
            Body = $bookingBody
            TimeoutSec = 30
        }
        if ($headers.Count -gt 0) {
            $bookingParams['Headers'] = $headers
        }
        
        $booking = Invoke-RestMethod @bookingParams
        
        $bookingId = $booking.id
        $bookingAmount = $booking.amount
        
        # Step 3: Process payment
        $paymentBody = @{
            BookingId = $bookingId
            Amount = $bookingAmount
        } | ConvertTo-Json
        
        $paymentParams = @{
            Uri = "$paymentUrl/api/payment/pay"
            Method = "Post"
            ContentType = "application/json"
            Body = $paymentBody
            TimeoutSec = 30
        }
        if ($headers.Count -gt 0) {
            $paymentParams['Headers'] = $headers
        }
        
        $payment = Invoke-RestMethod @paymentParams
        
        # Step 4: Wait for event processing
        Start-Sleep -Milliseconds 2000
        
        # Step 5: Verify booking status
        $verifyParams = @{
            Uri = "$bookingUrl/api/bookings/$bookingId"
            Method = "Get"
            TimeoutSec = 30
        }
        if ($headers.Count -gt 0) {
            $verifyParams['Headers'] = $headers
        }
        
        $updatedBooking = Invoke-RestMethod @verifyParams
        
        $flowStopwatch.Stop()
        $elapsed = $flowStopwatch.Elapsed.TotalMilliseconds
        
        if ($updatedBooking.status -eq "CONFIRMED") {
            Write-Host "[OK] Flow #$flowId completed in $([math]::Round($elapsed, 0))ms (User: $userId, Booking: $bookingId)" -ForegroundColor Green
            
            $lockTaken = $false
            try {
                [System.Threading.Monitor]::Enter($results.SyncRoot, [ref]$lockTaken)
                $results.successCount++
                $results.flowTimes += $elapsed
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
        if ($errorMsg.Length -gt 60) {
            $errorMsg = $errorMsg.Substring(0, 60) + "..."
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
} -ThrottleLimit $ConcurrentFlows
} else {
    # Sequential execution for PS 5.1
    for ($i = 1; $i -le $NumberOfFlows; $i++) {
        $flowStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        
        try {
            # Step 1: Register user
            $username = "user$i-$(Get-Random)"
            $registerBody = @{
                Username = $username
                Email = "$username@example.com"
                Password = "Test@2025!Pass"
                FirstName = "Test"
                LastName = "User$i"
            } | ConvertTo-Json
            
            $user = Invoke-RestMethod -Uri "$GatewayUrl/api/users/register" `
                -Method Post `
                -ContentType "application/json" `
                -Body $registerBody `
                -TimeoutSec 30
            
        # API returns wrapped response: { success, message, data: { id, username, ... } }
        $userId = $user.data.id
        
        # Step 1.5: Login to get JWT token (if authentication enabled)
        $token = $null
        $headers = @{}
        if ($using:UseAuthentication) {
            $loginBody = @{
                Username = $username
                Password = "Test@2025!Pass"
            } | ConvertTo-Json
            
            $loginResponse = Invoke-RestMethod -Uri "$using:GatewayUrl/api/users/login" `
                -Method Post `
                -ContentType "application/json" `
                -Body $loginBody `
                -TimeoutSec 30
            
            $token = $loginResponse.data.token.Trim()
            $headers = @{
                "Authorization" = "Bearer $token"
            }
        }
        
        # Step 2: Create booking
        $bookingBody = @{
            UserId = $userId
            RoomId = "ROOM-$(Get-Random -Minimum 100 -Maximum 999)"
            Amount = Get-Random -Minimum 100000 -Maximum 1000000
        } | ConvertTo-Json
        
        $bookingParams = @{
            Uri = "$using:BookingUrl/api/bookings"
            Method = "Post"
            ContentType = "application/json"
            Body = $bookingBody
            TimeoutSec = 30
        }
        if ($headers.Count -gt 0) {
            $bookingParams['Headers'] = $headers
        }
        
        $booking = Invoke-RestMethod @bookingParams
        
        $bookingId = $booking.id
        $bookingAmount = $booking.amount
        
        # Step 3: Process payment
        $paymentBody = @{
            BookingId = $bookingId
            Amount = $bookingAmount
        } | ConvertTo-Json
        
        $paymentParams = @{
            Uri = "$using:PaymentUrl/api/payment/pay"
            Method = "Post"
            ContentType = "application/json"
            Body = $paymentBody
            TimeoutSec = 30
        }
        if ($headers.Count -gt 0) {
            $paymentParams['Headers'] = $headers
        }
        
        $payment = Invoke-RestMethod @paymentParams
        
        # Step 4: Wait for event processing
        Start-Sleep -Milliseconds 2000
        
        # Step 5: Verify booking status
        $verifyParams = @{
            Uri = "$using:BookingUrl/api/bookings/$bookingId"
            Method = "Get"
            TimeoutSec = 30
        }
        if ($headers.Count -gt 0) {
            $verifyParams['Headers'] = $headers
        }
        
        $updatedBooking = Invoke-RestMethod @verifyParams            $flowStopwatch.Stop()
            $elapsed = $flowStopwatch.Elapsed.TotalMilliseconds
            
            if ($updatedBooking.status -eq "CONFIRMED") {
                Write-Host "[OK] Flow #$i completed in $([math]::Round($elapsed, 0))ms (User: $userId, Booking: $bookingId)" -ForegroundColor Green
                $results.successCount++
                $results.flowTimes += $elapsed
            } else {
                $reason = "Booking status is '$($updatedBooking.status)' instead of 'CONFIRMED'"
                Write-Host "[WARN] Flow #${i}: $reason" -ForegroundColor Yellow
                $results.failCount++
                $results.failureReasons += $reason
            }
            
        } catch {
            $flowStopwatch.Stop()
            $errorMsg = $_.Exception.Message
            if ($errorMsg.Length -gt 60) {
                $errorMsg = $errorMsg.Substring(0, 60) + "..."
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
$avgTime = if ($timeArray.Count -gt 0) { ($timeArray | Measure-Object -Average).Average } else { 0 }
$minTime = if ($timeArray.Count -gt 0) { ($timeArray | Measure-Object -Minimum).Minimum } else { 0 }
$maxTime = if ($timeArray.Count -gt 0) { ($timeArray | Measure-Object -Maximum).Maximum } else { 0 }
$p95Time = if ($timeArray.Count -gt 0) { 
    $sorted = $timeArray | Sort-Object
    $index = [Math]::Floor($sorted.Count * 0.95)
    if ($index -ge $sorted.Count) { $index = $sorted.Count - 1 }
    $sorted[$index]
} else { 0 }

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   E2E Flow Test Results" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Total Flows:       $NumberOfFlows"
Write-Host "Successful:        $successCount" -ForegroundColor Green
Write-Host "Failed:            $failCount" -ForegroundColor $(if ($failCount -gt 0) { "Red" } else { "Green" })
Write-Host "Success Rate:      $([math]::Round(($successCount / $NumberOfFlows) * 100, 2))%"
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Performance Metrics" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Total Time:        $([math]::Round($stopwatch.Elapsed.TotalSeconds, 2)) seconds"
Write-Host "Flows/sec:         $([math]::Round($NumberOfFlows / $stopwatch.Elapsed.TotalSeconds, 2))"
Write-Host ""
Write-Host "E2E Flow Times:"
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

if ($p95Time -lt 5000) {
    Write-Host "[EXCELLENT] E2E Flow time: p95 < 5s" -ForegroundColor Green
} elseif ($p95Time -lt 10000) {
    Write-Host "[GOOD] E2E Flow time: p95 < 10s" -ForegroundColor Yellow
} else {
    Write-Host "[POOR] E2E Flow time: p95 > 10s" -ForegroundColor Red
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
Write-Host "   Next Steps" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "1. Check RabbitMQ Management: http://localhost:15672" -ForegroundColor Gray
Write-Host "2. View logs in Seq:          http://localhost:5341" -ForegroundColor Gray
Write-Host "3. Monitor containers:        docker stats" -ForegroundColor Gray
Write-Host "4. Check service health:      .\test-health.bat" -ForegroundColor Gray
Write-Host ""
