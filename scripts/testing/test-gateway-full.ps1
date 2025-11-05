Write-Host "Testing API Gateway - User Registration"
Write-Host "========================================`n"

$body = @{
    username = "testuser"
    email = "testuser@example.com"
    password = "Test@2025!Pass"
    firstName = "Test"
    lastName = "User"
} | ConvertTo-Json

Write-Host "Registering new user..."
try {
    $response = Invoke-RestMethod -Uri "http://localhost:5000/api/users/register" `
        -Method Post `
        -ContentType "application/json" `
        -Body $body

    Write-Host "SUCCESS! User registered through API Gateway"
    Write-Host "`nResponse:"
    $response | ConvertTo-Json
} catch {
    Write-Host "Error: $_"
}

Write-Host "`n`nTesting API Gateway - User Login"
Write-Host "================================`n"

$loginBody = @{
    username = "testuser"
    password = "Test@2025!Pass"
} | ConvertTo-Json

Write-Host "Logging in..."
try {
    $loginResponse = Invoke-RestMethod -Uri "http://localhost:5000/api/users/login" `
        -Method Post `
        -ContentType "application/json" `
        -Body $loginBody

    Write-Host "SUCCESS! User logged in through API Gateway"
    Write-Host "`nResponse:"
    $loginResponse | ConvertTo-Json
} catch {
    Write-Host "Error: $_"
}
