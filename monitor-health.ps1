# Health monitoring script
# Continuously monitors health of all services

param(
    [int]$RefreshIntervalSeconds = 5
)

Write-Host "Starting Health Monitor - Press Ctrl+C to stop" -ForegroundColor Cyan
Write-Host "Refresh interval: $RefreshIntervalSeconds seconds" -ForegroundColor Gray
Write-Host ""

$services = @(
    @{Name="API Gateway"; Url="http://localhost:5000/health"; Port=5000},
    @{Name="UserService"; Url="http://localhost:5001/health"; Port=5001},
    @{Name="BookingService"; Url="http://localhost:5002/health"; Port=5002},
    @{Name="PaymentService"; Url="http://localhost:5003/health"; Port=5003}
)

while ($true) {
    Clear-Host
    
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "   Service Health Dashboard" -ForegroundColor Cyan
    Write-Host "   $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    
    # Check each service
    $allHealthy = $true
    foreach ($service in $services) {
        try {
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            $response = Invoke-RestMethod -Uri $service.Url -TimeoutSec 3 -ErrorAction Stop
            $sw.Stop()
            
            $status = if ($response.status) { $response.status } else { "Healthy" }
            $responseTime = $sw.Elapsed.TotalMilliseconds
            
            Write-Host "âœ“ " -NoNewline -ForegroundColor Green
            Write-Host "$($service.Name.PadRight(20))" -NoNewline -ForegroundColor White
            Write-Host " | Status: " -NoNewline -ForegroundColor Gray
            Write-Host "$status".PadRight(10) -NoNewline -ForegroundColor Green
            Write-Host " | Response: " -NoNewline -ForegroundColor Gray
            Write-Host "$([math]::Round($responseTime, 0))ms".PadRight(8) -ForegroundColor Cyan
            
        } catch {
            $allHealthy = $false
            $errorType = if ($_.Exception.Message -match "timed out") { "TIMEOUT" } 
                        elseif ($_.Exception.Message -match "refused") { "CONNECTION REFUSED" }
                        else { "UNHEALTHY" }
            
            Write-Host "âœ— " -NoNewline -ForegroundColor Red
            Write-Host "$($service.Name.PadRight(20))" -NoNewline -ForegroundColor White
            Write-Host " | Status: " -NoNewline -ForegroundColor Gray
            Write-Host "$errorType" -ForegroundColor Red
        }
    }
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "   Infrastructure Status" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    
    # Check RabbitMQ
    try {
        $rabbitmqResponse = Invoke-RestMethod -Uri "http://localhost:15672/api/overview" `
            -Method Get `
            -Credential (New-Object System.Management.Automation.PSCredential("guest", (ConvertTo-SecureString "guest" -AsPlainText -Force))) `
            -TimeoutSec 3 -ErrorAction Stop
        
        Write-Host "âœ“ " -NoNewline -ForegroundColor Green
        Write-Host "RabbitMQ".PadRight(20) -NoNewline -ForegroundColor White
        Write-Host " | Messages: " -NoNewline -ForegroundColor Gray
        Write-Host "$($rabbitmqResponse.queue_totals.messages)".PadRight(8) -NoNewline -ForegroundColor Cyan
        Write-Host " | Connections: " -NoNewline -ForegroundColor Gray
        Write-Host "$($rabbitmqResponse.object_totals.connections)" -ForegroundColor Cyan
    } catch {
        Write-Host "âœ— " -NoNewline -ForegroundColor Red
        Write-Host "RabbitMQ".PadRight(20) -NoNewline -ForegroundColor White
        Write-Host " | Status: UNAVAILABLE" -ForegroundColor Red
    }
    
    # Check Seq
    try {
        $seqResponse = Invoke-WebRequest -Uri "http://localhost:5341/" -TimeoutSec 3 -ErrorAction Stop
        Write-Host "âœ“ " -NoNewline -ForegroundColor Green
        Write-Host "Seq Logging".PadRight(20) -NoNewline -ForegroundColor White
        Write-Host " | Status: Available" -ForegroundColor Green
    } catch {
        Write-Host "âœ— " -NoNewline -ForegroundColor Red
        Write-Host "Seq Logging".PadRight(20) -NoNewline -ForegroundColor White
        Write-Host " | Status: UNAVAILABLE" -ForegroundColor Red
    }
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "   Database Connections" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    
    # Check PostgreSQL (UserDB)
    try {
        $userdbCheck = docker exec userdb pg_isready -U userservice 2>&1
        if ($userdbCheck -match "accepting connections") {
            Write-Host "âœ“ " -NoNewline -ForegroundColor Green
            Write-Host "PostgreSQL (UserDB)".PadRight(25) -NoNewline -ForegroundColor White
            Write-Host " | Port 5432" -ForegroundColor Gray
        } else {
            Write-Host "âœ— " -NoNewline -ForegroundColor Red
            Write-Host "PostgreSQL (UserDB)".PadRight(25) -NoNewline -ForegroundColor White
            Write-Host " | NOT READY" -ForegroundColor Red
        }
    } catch {
        Write-Host "âœ— " -NoNewline -ForegroundColor Red
        Write-Host "PostgreSQL (UserDB)".PadRight(25) -NoNewline -ForegroundColor White
        Write-Host " | UNAVAILABLE" -ForegroundColor Red
    }
    
    # Check PostgreSQL (BookingDB)
    try {
        $bookingdbCheck = docker exec bookingdb pg_isready -U bookingservice 2>&1
        if ($bookingdbCheck -match "accepting connections") {
            Write-Host "âœ“ " -NoNewline -ForegroundColor Green
            Write-Host "PostgreSQL (BookingDB)".PadRight(25) -NoNewline -ForegroundColor White
            Write-Host " | Port 5433" -ForegroundColor Gray
        } else {
            Write-Host "âœ— " -NoNewline -ForegroundColor Red
            Write-Host "PostgreSQL (BookingDB)".PadRight(25) -NoNewline -ForegroundColor White
            Write-Host " | NOT READY" -ForegroundColor Red
        }
    } catch {
        Write-Host "âœ— " -NoNewline -ForegroundColor Red
        Write-Host "PostgreSQL (BookingDB)".PadRight(25) -NoNewline -ForegroundColor White
        Write-Host " | UNAVAILABLE" -ForegroundColor Red
    }
    
    # Check MongoDB
    try {
        $mongoCheck = docker exec paymentdb mongosh --quiet --eval "db.adminCommand('ping')" 2>&1
        if ($mongoCheck -match "ok") {
            Write-Host "âœ“ " -NoNewline -ForegroundColor Green
            Write-Host "MongoDB (PaymentDB)".PadRight(25) -NoNewline -ForegroundColor White
            Write-Host " | Port 27017" -ForegroundColor Gray
        } else {
            Write-Host "âœ— " -NoNewline -ForegroundColor Red
            Write-Host "MongoDB (PaymentDB)".PadRight(25) -NoNewline -ForegroundColor White
            Write-Host " | NOT READY" -ForegroundColor Red
        }
    } catch {
        Write-Host "âœ— " -NoNewline -ForegroundColor Red
        Write-Host "MongoDB (PaymentDB)".PadRight(25) -NoNewline -ForegroundColor White
        Write-Host " | UNAVAILABLE" -ForegroundColor Red
    }
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    
    if ($allHealthy) {
        Write-Host "   âœ“ All Services Healthy" -ForegroundColor Green
    } else {
        Write-Host "   âš  Some Services Unhealthy" -ForegroundColor Yellow
    }
    
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Next refresh in $RefreshIntervalSeconds seconds..." -ForegroundColor Gray
    Write-Host "Press Ctrl+C to stop monitoring" -ForegroundColor Gray
    
    Start-Sleep -Seconds $RefreshIntervalSeconds
}

