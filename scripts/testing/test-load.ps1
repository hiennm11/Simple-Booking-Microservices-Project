param(
    [int]$NumberOfRequests = 100,
    [int]$ConcurrentThreads = 10,
    [string]$BaseUrl = "http://localhost:5002"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Booking Service Load Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Target: $BaseUrl" -ForegroundColor White
Write-Host "Requests: $NumberOfRequests" -ForegroundColor White
Write-Host "Concurrent: $ConcurrentThreads" -ForegroundColor White
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
    times = @()
})

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

# Execute requests in parallel (PS 7+) or sequential (PS 5.1)
if ($PSVersionTable.PSVersion.Major -ge 7) {
    1..$NumberOfRequests | ForEach-Object -Parallel {
        $requestId = $_
        $url = $using:BaseUrl
        $results = $using:results
    
    try {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        
        # Create booking request
        $body = @{
            userId = [guid]::NewGuid().ToString()
            roomId = "ROOM-$(Get-Random -Minimum 100 -Maximum 999)"
            amount = Get-Random -Minimum 100000 -Maximum 1000000
        } | ConvertTo-Json
        
        $response = Invoke-RestMethod -Uri "$url/api/bookings" `
            -Method Post `
            -ContentType "application/json" `
            -Body $body `
            -TimeoutSec 30
        
        $sw.Stop()
        $elapsed = $sw.Elapsed.TotalMilliseconds
        
        Write-Host "[OK] Request #$requestId completed in $([math]::Round($elapsed, 0))ms" -ForegroundColor Green
        
        # Thread-safe increment
        $lockTaken = $false
        try {
            [System.Threading.Monitor]::Enter($results.SyncRoot, [ref]$lockTaken)
            $results.successCount++
            $results.times += $elapsed
        } finally {
            if ($lockTaken) { [System.Threading.Monitor]::Exit($results.SyncRoot) }
        }
        
    } catch {
        $errorMsg = $_.Exception.Message
        if ($errorMsg.Length -gt 80) {
            $errorMsg = $errorMsg.Substring(0, 80) + "..."
        }
        Write-Host "[FAIL] Request #$requestId failed: $errorMsg" -ForegroundColor Red
        
        $lockTaken = $false
        try {
            [System.Threading.Monitor]::Enter($results.SyncRoot, [ref]$lockTaken)
            $results.failCount++
        } finally {
            if ($lockTaken) { [System.Threading.Monitor]::Exit($results.SyncRoot) }
        }
    }
} -ThrottleLimit $ConcurrentThreads
} else {
    # Sequential execution for PS 5.1
    for ($i = 1; $i -le $NumberOfRequests; $i++) {
        try {
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            
            $body = @{
                userId = [guid]::NewGuid().ToString()
                roomId = "ROOM-$(Get-Random -Minimum 100 -Maximum 999)"
                amount = Get-Random -Minimum 100000 -Maximum 1000000
            } | ConvertTo-Json
            
            $response = Invoke-RestMethod -Uri "$BaseUrl/api/bookings" `
                -Method Post `
                -ContentType "application/json" `
                -Body $body `
                -TimeoutSec 30
            
            $sw.Stop()
            $elapsed = $sw.Elapsed.TotalMilliseconds
            
            Write-Host "[OK] Request #$i completed in $([math]::Round($elapsed, 0))ms" -ForegroundColor Green
            $results.successCount++
            $results.times += $elapsed
            
        } catch {
            $errorMsg = $_.Exception.Message
            if ($errorMsg.Length -gt 80) {
                $errorMsg = $errorMsg.Substring(0, 80) + "..."
            }
            Write-Host "[FAIL] Request #$i failed: $errorMsg" -ForegroundColor Red
            $results.failCount++
        }
    }
}

$stopwatch.Stop()

# Calculate statistics
$successCount = $results.successCount
$failCount = $results.failCount
$timeArray = $results.times
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
Write-Host "   Load Test Results" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Total Requests:    $NumberOfRequests"
Write-Host "Successful:        $successCount" -ForegroundColor Green
Write-Host "Failed:            $failCount" -ForegroundColor $(if ($failCount -gt 0) { "Red" } else { "Green" })
Write-Host "Success Rate:      $([math]::Round(($successCount / $NumberOfRequests) * 100, 2))%"
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Performance Metrics" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Total Time:        $([math]::Round($stopwatch.Elapsed.TotalSeconds, 2)) seconds"
Write-Host "Requests/sec:      $([math]::Round($NumberOfRequests / $stopwatch.Elapsed.TotalSeconds, 2))"
Write-Host ""
Write-Host "Response Times:"
Write-Host "  Average:         $([math]::Round($avgTime, 0))ms"
Write-Host "  Minimum:         $([math]::Round($minTime, 0))ms"
Write-Host "  Maximum:         $([math]::Round($maxTime, 0))ms"
Write-Host "  95th Percentile: $([math]::Round($p95Time, 0))ms"
Write-Host ""

# Performance assessment
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Performance Assessment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if ($p95Time -lt 500) {
    Write-Host "[EXCELLENT] Response time: p95 < 500ms" -ForegroundColor Green
} elseif ($p95Time -lt 1000) {
    Write-Host "[GOOD] Response time: p95 < 1000ms" -ForegroundColor Yellow
} else {
    Write-Host "[POOR] Response time: p95 > 1000ms" -ForegroundColor Red
}

if (($failCount / $NumberOfRequests) -lt 0.01) {
    Write-Host "[EXCELLENT] Error rate: <1%" -ForegroundColor Green
} elseif (($failCount / $NumberOfRequests) -lt 0.05) {
    Write-Host "[ACCEPTABLE] Error rate: <5%" -ForegroundColor Yellow
} else {
    Write-Host "[POOR] Error rate: >5%" -ForegroundColor Red
}

$rps = $NumberOfRequests / $stopwatch.Elapsed.TotalSeconds
if ($rps -gt 50) {
    Write-Host "[EXCELLENT] Throughput: >50 req/s" -ForegroundColor Green
} elseif ($rps -gt 20) {
    Write-Host "[GOOD] Throughput: >20 req/s" -ForegroundColor Yellow
} else {
    Write-Host "[POOR] Throughput: <20 req/s" -ForegroundColor Red
}

Write-Host ""
Write-Host "Tip: Monitor services with 'docker stats' and logs in Seq" -ForegroundColor Gray
Write-Host "     RabbitMQ: http://localhost:15672 | Seq: http://localhost:5341" -ForegroundColor Gray
