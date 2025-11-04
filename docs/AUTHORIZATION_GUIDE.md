# Authorization Implementation Guide

## Overview

This document explains how authorization works in the Simple Booking Microservices system and how to use it.

## Architecture

### Authentication Flow

```
┌─────────────┐
│   Client    │
└──────┬──────┘
       │ 1. POST /api/users/login
       │    {username, password}
       ▼
┌──────────────────┐
│   API Gateway    │
└──────┬───────────┘
       │ 2. Forward to UserService
       ▼
┌──────────────────┐
│  UserService     │────► Validate credentials
│                  │────► Generate JWT Token
└──────┬───────────┘
       │ 3. Return JWT Token
       ▼
┌─────────────┐
│   Client    │────► Store token
└─────────────┘
```

### Authorization Flow

```
┌─────────────┐
│   Client    │
└──────┬──────┘
       │ 1. Request with JWT Token
       │    Authorization: Bearer <token>
       ▼
┌──────────────────────────┐
│   API Gateway            │
│  ✓ Validate JWT Token    │
│  ✓ Extract User Claims   │
│  ✓ Check Authorization   │
└──────┬───────────────────┘
       │ 2. Forward with User Headers
       │    X-User-Id: {userId}
       │    X-User-Name: {username}
       │    X-User-Email: {email}
       ▼
┌──────────────────┐
│ BookingService / │
│ PaymentService   │────► Read user info from headers
│                  │────► Process business logic
└──────────────────┘
```

## Components

### 1. UserService (Authentication Provider)

**Responsibilities:**
- User registration
- User login & credential validation
- JWT token generation
- User data management

**Public Endpoints (No Auth Required):**
- `POST /api/users/register` - Register new user
- `POST /api/users/login` - Login and get JWT token

**Protected Endpoints (Auth Required):**
- `GET /api/users/{id}` - Get user details (should be protected)

**JWT Token Structure:**
```json
{
  "sub": "user-guid",           // User ID
  "unique_name": "username",    // Username
  "email": "user@example.com",  // Email
  "jti": "token-id",            // Token ID
  "iss": "UserService",         // Issuer
  "aud": "BookingSystem",       // Audience
  "exp": 1234567890             // Expiration
}
```

### 2. API Gateway (Authorization Enforcer)

**Responsibilities:**
- Validate JWT tokens for incoming requests
- Enforce authorization policies on routes
- Forward authenticated user claims to downstream services
- Centralized security enforcement

**Configuration:**
```json
{
  "JwtSettings": {
    "SecretKey": "shared-secret-key",
    "Issuer": "UserService",
    "Audience": "BookingSystem"
  }
}
```

**Route Policies:**
- `/api/users/**` - Public (register/login) + Protected (other endpoints)
- `/api/bookings/**` - Requires authentication
- `/api/payments/**` - Requires authentication

**Middleware Pipeline:**
1. Exception Handler
2. CORS
3. **Authentication** ← Validates JWT
4. **Authorization** ← Checks policies
5. **User Claims Forwarding** ← Adds headers
6. Request/Response Logging
7. YARP Reverse Proxy

### 3. BookingService & PaymentService (Resource Services)

**Responsibilities:**
- Trust the API Gateway (internal network security)
- Read user information from HTTP headers
- Implement business logic based on user context

**Reading User Information:**
```csharp
var userId = httpContext.Request.Headers["X-User-Id"].FirstOrDefault();
var username = httpContext.Request.Headers["X-User-Name"].FirstOrDefault();
var email = httpContext.Request.Headers["X-User-Email"].FirstOrDefault();
```

## Configuration

### Environment Variables Required

```bash
# JWT Settings (MUST be the same across all services)
JWT_SECRET_KEY=YourSecretKeyMustBe32CharactersOrMore123456!
JWT_ISSUER=UserService
JWT_AUDIENCE=BookingSystem
JWT_EXPIRY_MINUTES=60
```

### UserService Configuration

**appsettings.json:**
```json
{
  "JwtSettings": {
    "SecretKey": "${JWT_SECRET_KEY}",
    "Issuer": "${JWT_ISSUER}",
    "Audience": "${JWT_AUDIENCE}",
    "ExpirationMinutes": "${JWT_EXPIRY_MINUTES}"
  }
}
```

### API Gateway Configuration

**appsettings.json:**
```json
{
  "JwtSettings": {
    "SecretKey": "${JWT_SECRET_KEY}",
    "Issuer": "${JWT_ISSUER}",
    "Audience": "${JWT_AUDIENCE}"
  },
  "ReverseProxy": {
    "Routes": {
      "bookings-route": {
        "AuthorizationPolicy": "authenticated",
        // ... other config
      }
    }
  }
}
```

## Usage Examples

### 1. Register a New User

```bash
curl -X POST http://localhost:5000/api/users/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "john_doe",
    "email": "john@example.com",
    "password": "SecurePassword123!",
    "firstName": "John",
    "lastName": "Doe",
    "phoneNumber": "+1234567890"
  }'
```

**Response:**
```json
{
  "success": true,
  "message": "User registered successfully",
  "data": {
    "id": "guid",
    "username": "john_doe",
    "email": "john@example.com",
    "firstName": "John",
    "lastName": "Doe"
  }
}
```

### 2. Login to Get JWT Token

```bash
curl -X POST http://localhost:5000/api/users/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "john_doe",
    "password": "SecurePassword123!"
  }'
```

**Response:**
```json
{
  "success": true,
  "message": "Login successful",
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "user": {
      "id": "guid",
      "username": "john_doe",
      "email": "john@example.com"
    }
  }
}
```

### 3. Access Protected Endpoint

```bash
curl -X GET http://localhost:5000/api/bookings \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

**Without Token:**
```json
{
  "statusCode": 401,
  "message": "Unauthorized"
}
```

**With Valid Token:**
- API Gateway validates token
- Forwards request with user headers
- BookingService processes request

## Security Best Practices

### ✅ DO:

1. **Use Strong Secret Keys**
   - Minimum 32 characters
   - Mix of letters, numbers, symbols
   - Different for each environment

2. **Use HTTPS in Production**
   - Never send tokens over HTTP
   - Configure SSL certificates

3. **Set Appropriate Token Expiration**
   - Short-lived tokens (15-60 minutes)
   - Implement refresh tokens for longer sessions

4. **Validate Tokens Properly**
   - Check signature
   - Verify issuer and audience
   - Check expiration time

5. **Use Environment Variables**
   - Never hardcode secrets
   - Different keys per environment

### ❌ DON'T:

1. **Don't Store Tokens in LocalStorage** (if using browser)
   - Use httpOnly cookies instead
   - Prevents XSS attacks

2. **Don't Log Tokens**
   - Sensitive information
   - Log only token validation events

3. **Don't Trust Client Data**
   - Always validate user claims
   - Re-check permissions on server

4. **Don't Use Same Secret Across Environments**
   - Dev, Staging, Production need different keys

## Adding Authorization to UserService Endpoints

Currently, UserService endpoints like `GET /api/users/{id}` should also be protected. Here's how:

### Option 1: Protect at Gateway Level

Add authorization to users-route in `appsettings.json`:

```json
"users-route": {
  "ClusterId": "users-cluster",
  "AuthorizationPolicy": "authenticated",
  "Match": {
    "Path": "/api/users/{id:guid}"
  }
}
```

### Option 2: Protect at Service Level

Add authorization to UserEndpoints.cs:

```csharp
group.MapGet("/{id:guid}", GetUserById)
    .RequireAuthorization()  // Add this
    .WithName("GetUserById");
```

## Extending Authorization

### Role-Based Authorization

**1. Add Role to User Model:**
```csharp
public class User
{
    // ... existing properties
    public string Role { get; set; } = "User"; // Admin, User, Manager
}
```

**2. Add Role Claim to JWT:**
```csharp
var claims = new[]
{
    // ... existing claims
    new Claim(ClaimTypes.Role, user.Role)
};
```

**3. Create Role-Based Policies:**
```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("authenticated", policy => 
        policy.RequireAuthenticatedUser());
    
    options.AddPolicy("admin", policy => 
        policy.RequireRole("Admin"));
    
    options.AddPolicy("manager", policy => 
        policy.RequireRole("Admin", "Manager"));
});
```

**4. Apply to Routes:**
```json
"bookings-route": {
  "AuthorizationPolicy": "manager"
}
```

### Resource-Based Authorization

For checking if a user owns a resource:

```csharp
public async Task<IResult> GetBooking(Guid bookingId, HttpContext context)
{
    var userId = context.Request.Headers["X-User-Id"].FirstOrDefault();
    var booking = await _context.Bookings.FindAsync(bookingId);
    
    if (booking.UserId.ToString() != userId)
    {
        return Results.Forbid(); // 403 Forbidden
    }
    
    return Results.Ok(booking);
}
```

## Troubleshooting

### Token Validation Fails

**Symptoms:** 401 Unauthorized even with valid token

**Causes:**
1. Secret key mismatch between services
2. Token expired
3. Invalid issuer/audience
4. Clock skew issues

**Solutions:**
```bash
# Check JWT settings are identical
echo %JWT_SECRET_KEY%

# Verify token hasn't expired
# Decode token at https://jwt.io

# Check logs in Seq
# Look for "JWT Authentication failed" messages
```

### User Claims Not Forwarded

**Symptoms:** Headers `X-User-Id` etc. are null in downstream services

**Causes:**
1. Middleware order incorrect
2. Claims mapping wrong
3. Token claims missing

**Solutions:**
```csharp
// Ensure middleware order in API Gateway
app.UseAuthentication();      // MUST be first
app.UseAuthorization();        // MUST be second
app.UseUserClaimsForwarding(); // MUST be third
```

### CORS Issues with Authorization

**Symptoms:** Preflight requests fail

**Solutions:**
```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("Authorization"); // Important
    });
});
```

## Testing Authorization

### Manual Testing with cURL

```bash
# 1. Get token
TOKEN=$(curl -s -X POST http://localhost:5000/api/users/login \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","password":"password"}' \
  | jq -r '.data.token')

# 2. Use token
curl -X GET http://localhost:5000/api/bookings \
  -H "Authorization: Bearer $TOKEN"
```

### Testing with PowerShell

```powershell
# 1. Get token
$loginResponse = Invoke-RestMethod -Uri "http://localhost:5000/api/users/login" `
    -Method Post `
    -ContentType "application/json" `
    -Body '{"username":"testuser","password":"password"}'

$token = $loginResponse.data.token

# 2. Use token
$headers = @{
    "Authorization" = "Bearer $token"
}

Invoke-RestMethod -Uri "http://localhost:5000/api/bookings" `
    -Method Get `
    -Headers $headers
```

## Next Steps

1. ✅ **Implemented**: JWT generation in UserService
2. ✅ **Implemented**: JWT validation in API Gateway
3. ✅ **Implemented**: User claims forwarding to services
4. 🔄 **Pending**: Protect UserService endpoints
5. 🔄 **Pending**: Implement role-based authorization
6. 🔄 **Pending**: Add refresh token support
7. 🔄 **Pending**: Implement proper HTTPS in production

## References

- [Microsoft JWT Authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/)
- [YARP Documentation](https://microsoft.github.io/reverse-proxy/)
- [JWT.io](https://jwt.io/) - Token decoder/validator
