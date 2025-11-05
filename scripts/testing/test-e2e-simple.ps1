param(
    [int]$NumberOfFlows = 10,
    [string]$GatewayUrl = "http://localhost:5000",
    [string]$BookingUrl = "http://localhost:5002",
    [string]$PaymentUrl = "http://localhost:5003"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Simple E2E Flow Test (Sequential)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Total Flows:       $NumberOfFlows"
Write-Host "Gateway URL:       $GatewayUrl"
Write-Host "Note: This runs flows sequentially (one at a time)"
Write-Host ""

$successCount = 0
$failCount = 0
$flowTimes = @()
$failureReasons = @()

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

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
        
        # Step 2: Create booking
        $bookingBody = @{
            UserId = $userId
            RoomId = "ROOM-$(Get-Random -Minimum 100 -Maximum 999)"
            Amount = Get-Random -Minimum 100000 -Maximum 1000000
        } | ConvertTo-Json
        
        $booking = Invoke-RestMethod -Uri "$BookingUrl/api/bookings" `
            -Method Post `
            -ContentType "application/json" `
            -Body $bookingBody `
            -TimeoutSec 30
        
        $bookingId = $booking.id
        $bookingAmount = $booking.amount
        
        # Step 3: Process payment
        $paymentBody = @{
            BookingId = $bookingId
            Amount = $bookingAmount
        } | ConvertTo-Json
        
        $payment = Invoke-RestMethod -Uri "$PaymentUrl/api/payment/pay" `
            -Method Post `
            -ContentType "application/json" `
            -Body $paymentBody `
            -TimeoutSec 30
        
        # Step 4: Wait for event processing
        Start-Sleep -Milliseconds 2000
        
        # Step 5: Verify booking status
        $updatedBooking = Invoke-RestMethod -Uri "$BookingUrl/api/bookings/$bookingId" `
            -Method Get `
            -TimeoutSec 30
        
        $flowStopwatch.Stop()
        $elapsed = $flowStopwatch.Elapsed.TotalMilliseconds
        
        if ($updatedBooking.status -eq "CONFIRMED") {
            Write-Host "[OK] Flow #$i completed in $([math]::Round($elapsed, 0))ms (User: $userId)" -ForegroundColor Green
            $successCount++
            $flowTimes += $elapsed
        } else {
            $reason = "Booking status is '$($updatedBooking.status)' instead of 'CONFIRMED'"
            Write-Host "[WARN] Flow #${i}: $reason" -ForegroundColor Yellow
            $failCount++
            $failureReasons += $reason
        }
        
    } catch {
        $flowStopwatch.Stop()
        $errorMsg = $_.Exception.Message
        if ($errorMsg.Length -gt 60) {
            $errorMsg = $errorMsg.Substring(0, 60) + "..."
        }
        Write-Host "[FAIL] Flow #$i failed: $errorMsg" -ForegroundColor Red
        $failCount++
        $failureReasons += $errorMsg
    }
}

$stopwatch.Stop()

# Calculate statistics
$avgTime = if ($flowTimes.Count -gt 0) { ($flowTimes | Measure-Object -Average).Average } else { 0 }
$minTime = if ($flowTimes.Count -gt 0) { ($flowTimes | Measure-Object -Minimum).Minimum } else { 0 }
$maxTime = if ($flowTimes.Count -gt 0) { ($flowTimes | Measure-Object -Maximum).Maximum } else { 0 }
$p95Time = if ($flowTimes.Count -gt 0) { 
    $sorted = $flowTimes | Sort-Object
    $index = [Math]::Floor($sorted.Count * 0.95)
    if ($index -ge $sorted.Count) { $index = $sorted.Count - 1 }
    $sorted[$index]
} else { 0 }

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Results" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Total Flows:       $NumberOfFlows"
Write-Host "Successful:        $successCount" -ForegroundColor Green
Write-Host "Failed:            $failCount" -ForegroundColor $(if ($failCount -gt 0) { "Red" } else { "Green" })
Write-Host "Success Rate:      $([math]::Round(($successCount / $NumberOfFlows) * 100, 2))%"
Write-Host ""
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
    
    $groupedReasons = $failureReasons | Group-Object | Sort-Object Count -Descending
    
    foreach ($group in $groupedReasons) {
        Write-Host "  [$($group.Count)x] $($group.Name)" -ForegroundColor Yellow
    }
    Write-Host ""
}

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
Write-Host "For concurrent testing, install PowerShell 7+:" -ForegroundColor Gray
Write-Host "https://aka.ms/powershell" -ForegroundColor Gray
