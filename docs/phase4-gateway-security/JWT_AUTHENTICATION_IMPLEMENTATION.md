# JWT Authentication Implementation Guide

## 📋 Overview

This document describes the JWT (JSON Web Token) authentication implementation across the microservices architecture. JWT authentication provides secure, stateless authentication and authorization for the Booking System.

## 🎯 Architecture

### Authentication Flow

```
┌─────────┐         ┌─────────────┐         ┌──────────────┐         ┌─────────────┐
│ Client  │         │ API Gateway │         │ BookingSvc   │         │ UserService │
└────┬────┘         └──────┬──────┘         └──────┬───────┘         └──────┬──────┘
     │                     │                       │                        │
     │ 1. POST /users/     │                       │                        │
     │    api/login        │                       │                        │
     ├────────────────────>│                       │                        │
     │                     │                       │                        │
     │                     │ 2. Forward request    │                        │
     │                     ├───────────────────────┼───────────────────────>│
     │                     │                       │                        │
     │                     │                       │  3. Validate creds     │
     │                     │                       │     Generate JWT       │
     │                     │                       │                        │
     │                     │ 4. Return JWT token   │                        │
     │ 5. JWT Token        │<──────────────────────┼────────────────────────┤
     │<────────────────────┤                       │                        │
     │                     │                       │                        │
     │ 6. GET /bookings/   │                       │                        │
     │    api/bookings     │                       │                        │
     │    Header: Bearer   │                       │                        │
     │    <JWT-TOKEN>      │                       │                        │
     ├────────────────────>│                       │                        │
     │                     │                       │                        │
     │                     │ 7. Validate JWT       │                        │
     │                     │    Extract claims     │                        │
     │                     │                       │                        │
     │                     │ 8. Forward with       │                        │
     │                     │    X-User-Id header   │                        │
     │                     ├──────────────────────>│                        │
     │                     │                       │                        │
     │                     │                       │ 9. Process request     │
     │                     │                       │    using user context  │
     │                     │                       │                        │
     │                     │ 10. Return data       │                        │
     │ 11. Response        │<──────────────────────┤                        │
     │<────────────────────┤                       │                        │
     │                     │                       │                        │
```

## 🔐 Components

### 1. UserService - Token Generation

**Location:** `src/UserService/Services/AuthService.cs`

**Responsibility:** Generate JWT tokens upon successful login

**Token Claims:**
- `sub` (Subject): User ID (GUID)
- `unique_name`: Username
- `email`: User email address
- `jti`: Unique token identifier
- `iat`: Issued at timestamp

**Configuration:**
```json
{
  "JwtSettings": {
    "SecretKey": "${JWT_SECRET_KEY}",
    "Issuer": "${JWT_ISSUER}",
    "Audience": "${JWT_AUDIENCE}",
    "ExpirationMinutes": 60
  }
}
```

**Token Generation Code:**
```csharp
private string GenerateJwtToken(User user)
{
    var jwtSettings = _configuration.GetSection("JwtSettings");
    var secretKey = jwtSettings["SecretKey"];
    var issuer = jwtSettings["Issuer"];
    var audience = jwtSettings["Audience"];
    var expirationMinutes = int.Parse(jwtSettings["ExpirationMinutes"] ?? "60");

    var claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
        new Claim(JwtRegisteredClaimNames.Email, user.Email),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
    };

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        issuer: issuer,
        audience: audience,
        claims: claims,
        expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
        signingCredentials: credentials
    );

    return new JwtSecurityTokenHandler().WriteToken(token);
}
```

### 2. API Gateway - Token Validation

**Location:** `src/ApiGateway/Program.cs`

**Responsibility:** 
- Validate incoming JWT tokens
- Extract user claims
- Forward claims to downstream services
- Apply authorization policies to routes

**JWT Configuration:**
```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.Zero // No tolerance for expired tokens
        };
    });
```

**Authorization Policies:**
```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("authenticated", policy =>
    {
        policy.RequireAuthenticatedUser();
    });
});
```

**Route Protection (appsettings.json):**
```json
{
  "ReverseProxy": {
    "Routes": {
      "users-route": {
        "ClusterId": "users-cluster",
        "Match": {
          "Path": "/api/users/{**catch-all}"
        },
        "Metadata": {
          "Comment": "Register and Login are public"
        }
      },
      "bookings-route": {
        "ClusterId": "bookings-cluster",
        "AuthorizationPolicy": "authenticated",
        "Match": {
          "Path": "/api/bookings/{**catch-all}"
        }
      },
      "payments-route": {
        "ClusterId": "payments-cluster",
        "AuthorizationPolicy": "authenticated",
        "Match": {
          "Path": "/api/payments/{**catch-all}"
        }
      }
    }
  }
}
```

### 3. UserClaimsForwarding Middleware

**Location:** `src/ApiGateway/Middleware/UserClaimsForwardingMiddleware.cs`

**Responsibility:** Extract authenticated user claims and forward them as HTTP headers to downstream services

**Forwarded Headers:**
- `X-User-Id`: User identifier (GUID)
- `X-User-Name`: Username
- `X-User-Email`: User email
- `X-Forwarded-By`: Always set to "ApiGateway"

**Implementation:**
```csharp
public async Task InvokeAsync(HttpContext context)
{
    context.Request.Headers["X-Forwarded-By"] = "ApiGateway";
    
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var username = context.User.FindFirst(ClaimTypes.Name)?.Value;
        var email = context.User.FindFirst(ClaimTypes.Email)?.Value;

        if (!string.IsNullOrEmpty(userId))
            context.Request.Headers["X-User-Id"] = userId;
        if (!string.IsNullOrEmpty(username))
            context.Request.Headers["X-User-Name"] = username;
        if (!string.IsNullOrEmpty(email))
            context.Request.Headers["X-User-Email"] = email;
    }

    await _next(context);
}
```

### 4. BookingService & PaymentService - Token Validation

**Location:** 
- `src/BookingService/Program.cs`
- `src/PaymentService/Program.cs`

**Responsibility:** 
- Validate JWT tokens (for direct access scenarios)
- Apply [Authorize] attributes to controllers
- Support both JWT tokens and header-based authentication

**JWT Configuration (Same as API Gateway):**
```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.Zero
        };
    });
```

**Controller Protection:**
```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize] // Require authentication for all endpoints
public class BookingsController : ControllerBase
{
    // All endpoints require authentication
}
```

## 🔧 Configuration

### Environment Variables (.env)

```bash
# JWT Configuration
JWT_SECRET_KEY=B00k1ngSyst3m@S3cr3tK3y!2025#V3ryL0ngAndS3cur3P@ssw0rd
JWT_ISSUER=BookingSystem.UserService
JWT_AUDIENCE=BookingSystem.Clients
JWT_EXPIRY_MINUTES=60
```

**Security Note:** The secret key must be:
- At least 256 bits (32 characters) for HS256 algorithm
- Kept secret and never committed to version control
- Rotated periodically in production

### appsettings.json (All Services)

```json
{
  "JwtSettings": {
    "SecretKey": "${JWT_SECRET_KEY}",
    "Issuer": "${JWT_ISSUER}",
    "Audience": "${JWT_AUDIENCE}"
  }
}
```

## 📝 Usage Examples

### 1. User Registration (Public Endpoint)

```bash
POST http://localhost:5000/api/users/register
Content-Type: application/json

{
  "name": "John Doe",
  "email": "john@example.com",
  "password": "SecurePassword123!"
}
```

**Response:**
```json
{
  "id": "a3bb189e-8bf9-3888-9912-ace4e6543002",
  "username": "John Doe",
  "email": "john@example.com",
  "createdAt": "2025-11-05T10:00:00Z"
}
```

### 2. User Login (Public Endpoint)

```bash
POST http://localhost:5000/api/users/login
Content-Type: application/json

{
  "email": "john@example.com",
  "password": "SecurePassword123!"
}
```

**Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJhM2JiMTg5ZS04YmY5LTM4ODgtOTkxMi1hY2U0ZTY1NDMwMDIiLCJ1bmlxdWVfbmFtZSI6IkpvaG4gRG9lIiwiZW1haWwiOiJqb2huQGV4YW1wbGUuY29tIiwianRpIjoiN2M5ZTY2NzktNzQyNS00MGRlLTk0NGItZTA3ZmMxZjkwYWU3IiwiaWF0IjoiMTczMDc5NjAwMCIsImV4cCI6MTczMDc5OTYwMCwiaXNzIjoiQm9va2luZ1N5c3RlbS5Vc2VyU2VydmljZSIsImF1ZCI6IkJvb2tpbmdTeXN0ZW0uQ2xpZW50cyJ9.xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "userId": "a3bb189e-8bf9-3888-9912-ace4e6543002",
  "username": "John Doe",
  "email": "john@example.com",
  "expiresAt": "2025-11-05T11:00:00Z"
}
```

### 3. Create Booking (Protected Endpoint)

```bash
POST http://localhost:5000/api/bookings
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
  "userId": "a3bb189e-8bf9-3888-9912-ace4e6543002",
  "roomId": "ROOM-101",
  "amount": 500000
}
```

**Response (Success):**
```json
{
  "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "userId": "a3bb189e-8bf9-3888-9912-ace4e6543002",
  "roomId": "ROOM-101",
  "amount": 500000,
  "status": "PENDING",
  "createdAt": "2025-11-05T10:05:00Z"
}
```

**Response (Unauthorized - Missing Token):**
```json
{
  "status": 401,
  "title": "Unauthorized"
}
```

**Response (Unauthorized - Invalid Token):**
```json
{
  "status": 401,
  "title": "Unauthorized",
  "detail": "The token is invalid or expired"
}
```

### 4. Process Payment (Protected Endpoint)

```bash
POST http://localhost:5000/api/payment/pay
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
  "bookingId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "amount": 500000
}
```

## 🧪 Testing Authentication

### Testing with curl

```bash
# 1. Login and capture token
TOKEN=$(curl -s -X POST http://localhost:5000/api/users/login \
  -H "Content-Type: application/json" \
  -d '{"email":"testuser@example.com","password":"Test@123"}' \
  | jq -r '.token')

# 2. Use token to access protected endpoint
curl -X GET http://localhost:5000/api/bookings \
  -H "Authorization: Bearer $TOKEN"
```

### Testing with PowerShell

```powershell
# 1. Login and get token
$loginResponse = Invoke-RestMethod -Uri "http://localhost:5000/api/users/login" `
  -Method Post `
  -ContentType "application/json" `
  -Body (@{
    email = "testuser@example.com"
    password = "Test@123"
  } | ConvertTo-Json)

$token = $loginResponse.token

# 2. Use token to access protected endpoint
$headers = @{
    "Authorization" = "Bearer $token"
}

Invoke-RestMethod -Uri "http://localhost:5000/api/bookings" `
  -Method Get `
  -Headers $headers
```

## 🔍 Troubleshooting

### Common Issues

#### 1. "The token is invalid"

**Cause:** Token signature verification failed

**Solutions:**
- Ensure all services use the same `JWT_SECRET_KEY`
- Check that the secret key is at least 32 characters
- Verify environment variables are loaded correctly

#### 2. "The token is expired"

**Cause:** Token lifetime exceeded (default: 60 minutes)

**Solutions:**
- Login again to get a new token
- Adjust `JWT_EXPIRY_MINUTES` in configuration
- Implement token refresh mechanism (future enhancement)

#### 3. "Bearer token not found"

**Cause:** Authorization header missing or malformed

**Solutions:**
- Include `Authorization: Bearer <token>` header
- Ensure token has no leading/trailing spaces
- Check that the word "Bearer" is included with a space

#### 4. 401 Unauthorized on protected endpoints

**Cause:** Multiple possible causes

**Solutions:**
- Check if token is provided in Authorization header
- Verify token hasn't expired
- Ensure route has correct authorization policy
- Check service logs for detailed error messages

### Debugging Tips

1. **Enable detailed JWT logging:**
   ```csharp
   options.Events = new JwtBearerEvents
   {
       OnAuthenticationFailed = context =>
       {
           Log.Warning("JWT Authentication failed: {Error}", context.Exception.Message);
           return Task.CompletedTask;
       }
   };
   ```

2. **Check Seq logs** (http://localhost:5341):
   - Filter by: `Service = "ApiGateway"`
   - Search for: "JWT Authentication failed"
   - Look for token validation errors

3. **Decode JWT token** (for debugging only):
   - Use https://jwt.io to decode token
   - Verify claims: `sub`, `iss`, `aud`, `exp`
   - Check expiration timestamp

## 🚀 Best Practices

### Security Recommendations

1. **Secret Key Management:**
   - Use strong, randomly generated keys (minimum 256 bits)
   - Store in environment variables, never in code
   - Rotate keys periodically
   - Use different keys for dev/staging/production

2. **Token Expiration:**
   - Keep token lifetime short (60 minutes is reasonable)
   - Implement refresh tokens for long-lived sessions
   - Invalidate tokens on logout (requires token blacklist)

3. **HTTPS:**
   - Always use HTTPS in production
   - Never transmit tokens over HTTP
   - Configure HSTS headers

4. **Validation:**
   - Validate all token claims: issuer, audience, expiration
   - Set `ClockSkew = TimeSpan.Zero` to disable tolerance
   - Log all authentication failures

5. **Claims:**
   - Only include necessary information in tokens
   - Never include sensitive data (passwords, SSN, etc.)
   - Keep token payload small

### Performance Recommendations

1. **Token Caching:**
   - Consider caching validated tokens (with short TTL)
   - Reduce repeated signature verification

2. **Asymmetric Keys:**
   - For production, consider RS256 (RSA) instead of HS256
   - Public key can be distributed to services
   - Private key only on UserService

3. **Connection Pooling:**
   - Services already use efficient HTTP client pooling
   - No additional configuration needed

## 📊 Monitoring

### Key Metrics to Track

1. **Authentication Success Rate:**
   ```
   Service = "ApiGateway" AND Message LIKE "%validated%"
   ```

2. **Authentication Failures:**
   ```
   Service = "ApiGateway" AND Message LIKE "%Authentication failed%"
   ```

3. **Token Expiration Rate:**
   ```
   Service = "ApiGateway" AND Message LIKE "%token is expired%"
   ```

4. **Protected Endpoint Access:**
   ```
   Service IN ["BookingService", "PaymentService"] AND StatusCode = 401
   ```

## 🔮 Future Enhancements

1. **Refresh Tokens:**
   - Implement refresh token flow
   - Allow long-lived sessions without security risks

2. **Token Revocation:**
   - Implement token blacklist (Redis)
   - Support logout functionality

3. **Role-Based Access Control (RBAC):**
   - Add roles to JWT claims
   - Implement fine-grained authorization

4. **OAuth2/OpenID Connect:**
   - Integrate with external identity providers
   - Support social login (Google, Microsoft, etc.)

5. **API Key Authentication:**
   - Support for service-to-service authentication
   - Alternative to JWT for background jobs

## 📚 References

- [JWT.io - JWT Introduction](https://jwt.io/introduction)
- [Microsoft - ASP.NET Core Authentication](https://docs.microsoft.com/en-us/aspnet/core/security/authentication/)
- [RFC 7519 - JSON Web Token (JWT)](https://tools.ietf.org/html/rfc7519)
- [OWASP - JWT Security Best Practices](https://cheatsheetseries.owasp.org/cheatsheets/JSON_Web_Token_for_Java_Cheat_Sheet.html)

---

**Document Version:** 1.0  
**Last Updated:** November 5, 2025  
**Author:** Simple Booking Microservices Team
