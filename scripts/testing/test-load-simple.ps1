param(
    [int]$NumberOfRequests = 20,
    [string]$BaseUrl = "http://localhost:5002"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Simple Load Test (Sequential)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Target: $BaseUrl"
Write-Host "Requests: $NumberOfRequests"
Write-Host "Note: This runs requests sequentially (one at a time)"
Write-Host ""

$successCount = 0
$failCount = 0
$times = @()

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

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
        $successCount++
        $times += $elapsed
        
    } catch {
        $errorMsg = $_.Exception.Message
        if ($errorMsg.Length -gt 80) {
            $errorMsg = $errorMsg.Substring(0, 80) + "..."
        }
        Write-Host "[FAIL] Request #$i failed: $errorMsg" -ForegroundColor Red
        $failCount++
    }
}

$stopwatch.Stop()

# Calculate statistics
$avgTime = if ($times.Count -gt 0) { ($times | Measure-Object -Average).Average } else { 0 }
$minTime = if ($times.Count -gt 0) { ($times | Measure-Object -Minimum).Minimum } else { 0 }
$maxTime = if ($times.Count -gt 0) { ($times | Measure-Object -Maximum).Maximum } else { 0 }
$p95Time = if ($times.Count -gt 0) { 
    $sorted = $times | Sort-Object
    $index = [Math]::Floor($sorted.Count * 0.95)
    if ($index -ge $sorted.Count) { $index = $sorted.Count - 1 }
    $sorted[$index]
} else { 0 }

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Results" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Total Requests:    $NumberOfRequests"
Write-Host "Successful:        $successCount" -ForegroundColor Green
Write-Host "Failed:            $failCount" -ForegroundColor $(if ($failCount -gt 0) { "Red" } else { "Green" })
Write-Host "Success Rate:      $([math]::Round(($successCount / $NumberOfRequests) * 100, 2))%"
Write-Host ""
Write-Host "Total Time:        $([math]::Round($stopwatch.Elapsed.TotalSeconds, 2)) seconds"
Write-Host "Requests/sec:      $([math]::Round($NumberOfRequests / $stopwatch.Elapsed.TotalSeconds, 2))"
Write-Host ""
Write-Host "Response Times:"
Write-Host "  Average:         $([math]::Round($avgTime, 0))ms"
Write-Host "  Minimum:         $([math]::Round($minTime, 0))ms"
Write-Host "  Maximum:         $([math]::Round($maxTime, 0))ms"
Write-Host "  95th Percentile: $([math]::Round($p95Time, 0))ms"
Write-Host ""

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

Write-Host ""
Write-Host "For concurrent testing, install PowerShell 7+:" -ForegroundColor Gray
Write-Host "https://aka.ms/powershell" -ForegroundColor Gray
