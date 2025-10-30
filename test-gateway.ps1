$body = @{
    username = "admin"
    password = "Admin@2025!Pass"
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "http://localhost:5000/api/users/login" `
    -Method Post `
    -ContentType "application/json" `
    -Body $body

$response | ConvertTo-Json
