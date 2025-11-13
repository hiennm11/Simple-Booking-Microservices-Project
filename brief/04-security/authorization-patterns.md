# Authorization Patterns - Deep Dive

## Table of Contents

- [Authentication vs Authorization](#authentication-vs-authorization)
- [Authorization in Microservices](#authorization-in-microservices)
- [Policy-Based Authorization](#policy-based-authorization)
- [Claims-Based Authorization](#claims-based-authorization)
- [Claims Forwarding Pattern](#claims-forwarding-pattern)
- [Route-Level Authorization](#route-level-authorization)
- [Resource-Level Authorization](#resource-level-authorization)
- [Service-to-Service Authorization](#service-to-service-authorization)
- [Testing Authorization](#testing-authorization)
- [Common Pitfalls](#common-pitfalls)
- [Best Practices](#best-practices)
- [Interview Questions](#interview-questions)

---

## Authentication vs Authorization

### Simple Analogy

Think of entering a secured office building:

**Authentication** = Showing your ID badge at the front desk
- Proves WHO you are
- Question: "Are you really John Doe?"
- Answer: Yes (valid badge) or No (invalid badge)

**Authorization** = What rooms you can access after entering
- Determines WHAT you can do
- Question: "Can John Doe enter the server room?"
- Answer: Yes (has permission) or No (lacks permission)

### Technical Definitions

**Authentication:**
```
Process: Verify identity
Input: Credentials (username/password, JWT, API key)
Output: User identity (claims, user object)
Question: "Who are you?"
```

**Authorization:**
```
Process: Check permissions
Input: User identity + requested resource + action
Output: Allow or Deny
Question: "Can you do this?"
```

### Real-World Example

```
Scenario: User tries to delete booking #123

Step 1: Authentication
- Client sends: JWT token in Authorization header
- Gateway validates: Token signature, expiration, issuer
- Result: User identified as "john_doe" (ID: 3fa85f64...)

Step 2: Authorization
- Check 1: Is user authenticated? ✅
- Check 2: Does user have "bookings.delete" permission? ✅
- Check 3: Does booking #123 belong to user 3fa85f64...? ✅
- Result: ALLOW deletion

All checks must pass for authorization to succeed!
```

### Key Differences

| Aspect | Authentication | Authorization |
|--------|----------------|---------------|
| **Purpose** | Verify identity | Control access |
| **Question** | "Who are you?" | "What can you do?" |
| **When** | First (login) | After authentication |
| **Output** | User identity | Access decision |
| **Failure** | 401 Unauthorized | 403 Forbidden |
| **Example** | JWT validation | Policy check |

---

## Authorization in Microservices

### The Challenge

Traditional monolithic authorization:
```
┌────────────────────────────────────┐
│       Monolithic Application       │
│                                    │
│  ┌──────────────────────────────┐ │
│  │   Authorization Service      │ │
│  │   (Single source of truth)   │ │
│  └──────────────────────────────┘ │
│           │                        │
│           ├──> User Module         │
│           ├──> Booking Module      │
│           ├──> Payment Module      │
│           └──> Admin Module        │
│                                    │
└────────────────────────────────────┘

✅ Centralized authorization logic
✅ Consistent policies
❌ Tight coupling
❌ Doesn't scale
```

Microservices authorization challenge:
```
┌──────────┐     ┌──────────┐     ┌──────────┐     ┌──────────┐
│  User    │     │ Booking  │     │ Payment  │     │Inventory │
│ Service  │     │ Service  │     │ Service  │     │ Service  │
└──────────┘     └──────────┘     └──────────┘     └──────────┘
     │                │                 │                │
     │                │                 │                │
     └────────────────┴─────────────────┴────────────────┘
                            │
              Who decides authorization?
              How to share user context?
              How to stay decoupled?
```

### Our Solution: Layered Authorization

```
┌────────────────────────────────────────────────────────────┐
│                     API GATEWAY                            │
│                                                            │
│  Layer 1: Route-Level Authorization                       │
│  ┌──────────────────────────────────────────────────────┐ │
│  │ Policy: "authenticated"                              │ │
│  │ Check: Is JWT valid?                                 │ │
│  │ Result: ALLOW or 401 Unauthorized                    │ │
│  └──────────────────────────────────────────────────────┘ │
│                          │                                 │
│                          ▼                                 │
│  Layer 2: Claims Extraction & Forwarding                  │
│  ┌──────────────────────────────────────────────────────┐ │
│  │ Extract: User ID, Username, Email                    │ │
│  │ Forward: X-User-Id, X-User-Name, X-User-Email        │ │
│  └──────────────────────────────────────────────────────┘ │
│                          │                                 │
└──────────────────────────┼─────────────────────────────────┘
                           ▼
┌────────────────────────────────────────────────────────────┐
│                 BACKEND SERVICES                           │
│                                                            │
│  Layer 3: Resource-Level Authorization                    │
│  ┌──────────────────────────────────────────────────────┐ │
│  │ Check: Does resource belong to user?                 │ │
│  │ Example: Booking.UserId == X-User-Id?                │ │
│  │ Result: ALLOW or 403 Forbidden                       │ │
│  └──────────────────────────────────────────────────────┘ │
│                                                            │
└────────────────────────────────────────────────────────────┘

Benefits:
✅ Gateway handles authentication
✅ Services focus on business logic
✅ Each service owns its authorization rules
✅ Decoupled architecture
✅ Easy to scale
```

---

## Policy-Based Authorization

### What is a Policy?

A **policy** is a named set of authorization requirements that must be satisfied.

```csharp
// Define a policy
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("authenticated", policy =>
    {
        policy.RequireAuthenticatedUser();
    });
});

// Apply to route
"AuthorizationPolicy": "authenticated"

// What happens:
// 1. User makes request
// 2. Middleware checks if policy requirements met
// 3. If yes: proceed to handler
// 4. If no: return 401 Unauthorized
```

### Built-In Policy Requirements

```csharp
builder.Services.AddAuthorization(options =>
{
    // 1. Require authenticated user
    options.AddPolicy("authenticated", policy =>
    {
        policy.RequireAuthenticatedUser();
    });
    
    // 2. Require specific role
    options.AddPolicy("admin", policy =>
    {
        policy.RequireRole("Admin");
    });
    
    // 3. Require specific claim
    options.AddPolicy("canCreateBooking", policy =>
    {
        policy.RequireClaim("permissions", "bookings.write");
    });
    
    // 4. Require all roles
    options.AddPolicy("adminOrManager", policy =>
    {
        policy.RequireRole("Admin", "Manager");  // User must have one of these
    });
    
    // 5. Custom requirement
    options.AddPolicy("over18", policy =>
    {
        policy.RequireAssertion(context =>
        {
            var ageClaimValue = context.User.FindFirst("age")?.Value;
            if (int.TryParse(ageClaimValue, out int age))
            {
                return age >= 18;
            }
            return false;
        });
    });
    
    // 6. Combine multiple requirements
    options.AddPolicy("premiumUser", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("subscription", "premium");
        policy.RequireAssertion(context =>
        {
            var expiryDate = context.User.FindFirst("subscriptionExpiry")?.Value;
            if (DateTime.TryParse(expiryDate, out DateTime expiry))
            {
                return expiry > DateTime.UtcNow;
            }
            return false;
        });
    });
});
```

### Our Implementation (API Gateway)

**Configuration:**
```csharp
// Program.cs
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("authenticated", policy =>
    {
        policy.RequireAuthenticatedUser();
    });
});
```

**Applying to Routes:**
```json
// appsettings.json
{
  "ReverseProxy": {
    "Routes": {
      "users-route": {
        "ClusterId": "users-cluster",
        "Match": {
          "Path": "/api/users/{**catch-all}"
        }
        // No AuthorizationPolicy = Public access
        // (Register and Login endpoints)
      },
      "bookings-route": {
        "ClusterId": "bookings-cluster",
        "AuthorizationPolicy": "authenticated",  // Requires valid JWT
        "Match": {
          "Path": "/api/bookings/{**catch-all}"
        }
      },
      "payments-route": {
        "ClusterId": "payments-cluster",
        "AuthorizationPolicy": "authenticated",  // Requires valid JWT
        "Match": {
          "Path": "/api/payments/{**catch-all}"
        }
      },
      "inventory-route": {
        "ClusterId": "inventory-cluster",
        "AuthorizationPolicy": "authenticated",  // Requires valid JWT
        "Match": {
          "Path": "/api/inventory/{**catch-all}"
        }
      }
    }
  }
}
```

### Policy Evaluation Flow

```
Request: GET /api/bookings
Authorization: Bearer eyJhbGc...

STEP 1: Route Matching
- Path: /api/bookings
- Matches route: "bookings-route"
- Policy required: "authenticated"

STEP 2: Retrieve Policy
- Lookup policy by name
- Policy requirements: RequireAuthenticatedUser()

STEP 3: Evaluate Requirements
- Check: context.User.Identity.IsAuthenticated
- JWT validated: ✅ Yes
- User authenticated: ✅ Yes

STEP 4: Policy Result
- All requirements met: ✅
- Authorization: SUCCESS
- Continue to downstream service

STEP 5: If Policy Failed
- Return: 401 Unauthorized
- Stop request pipeline
- No downstream call
```

---

## Claims-Based Authorization

### What are Claims?

**Claims** are key-value pairs that describe a user's identity and attributes.

```csharp
// Examples of claims
new Claim("sub", "3fa85f64-5717-4562-b3fc-2c963f66afa6")  // User ID
new Claim("unique_name", "john_doe")                       // Username
new Claim("email", "john@example.com")                     // Email
new Claim("role", "Admin")                                 // Role
new Claim("permissions", "bookings.read")                  // Permission
new Claim("subscription", "premium")                       // Subscription tier
new Claim("age", "25")                                     // Age
```

### Claims vs Roles

**Roles (traditional):**
```
User has roles: Admin, Manager
Authorization: if (user.IsInRole("Admin")) { ... }

❌ Coarse-grained (all-or-nothing)
❌ Tightly coupled to code
❌ Hard to extend (add new role = code changes)
```

**Claims (modern):**
```
User has claims:
- permissions: bookings.read
- permissions: bookings.write
- permissions: payments.read

Authorization: if (user.HasClaim("permissions", "bookings.write")) { ... }

✅ Fine-grained control
✅ Flexible and extensible
✅ Data-driven (no code changes)
```

### Using Claims for Authorization

**Policy with claims:**
```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("canCreateBooking", policy =>
    {
        policy.RequireClaim("permissions", "bookings.write");
    });
    
    options.AddPolicy("canCancelBooking", policy =>
    {
        policy.RequireAssertion(context =>
        {
            // Can cancel if:
            // 1. User has bookings.delete permission, OR
            // 2. User is Admin
            var hasPermission = context.User.HasClaim("permissions", "bookings.delete");
            var isAdmin = context.User.IsInRole("Admin");
            return hasPermission || isAdmin;
        });
    });
});
```

**Controller usage:**
```csharp
[Authorize(Policy = "canCreateBooking")]
public IActionResult CreateBooking([FromBody] CreateBookingRequest request)
{
    // User has "bookings.write" permission
    var booking = _service.CreateBooking(request);
    return Ok(booking);
}

[Authorize(Policy = "canCancelBooking")]
public IActionResult CancelBooking(Guid id)
{
    // User has "bookings.delete" permission OR is Admin
    _service.CancelBooking(id);
    return NoContent();
}
```

### Adding Claims to JWT

**UserService (token generation):**
```csharp
private string GenerateJwtToken(User user)
{
    var claims = new List<Claim>
    {
        new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
        new Claim(JwtRegisteredClaimNames.Email, user.Email),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
    };
    
    // Add role claims
    foreach (var role in user.Roles)
    {
        claims.Add(new Claim(ClaimTypes.Role, role));
    }
    
    // Add permission claims
    foreach (var permission in user.Permissions)
    {
        claims.Add(new Claim("permissions", permission));
    }
    
    // Create token with claims
    var token = new JwtSecurityToken(
        issuer: issuer,
        audience: audience,
        claims: claims,
        expires: DateTime.UtcNow.AddMinutes(60),
        signingCredentials: credentials
    );
    
    return new JwtSecurityTokenHandler().WriteToken(token);
}
```

---

## Claims Forwarding Pattern

### The Problem

Backend services don't receive the JWT token (by design):

```
Client → API Gateway → BookingService

API Gateway:
- Validates JWT ✅
- Extracts claims ✅
- Forwards request to BookingService
- Does NOT forward JWT token (security best practice)

BookingService:
- Receives request
- Needs user context (who made the request?)
- ❌ No JWT token available
- ❌ Can't extract claims

Problem: How does BookingService know the user identity?
```

### The Solution: Claims Forwarding Middleware

**Middleware implementation:**

**Location:** `src/ApiGateway/Middleware/UserClaimsForwardingMiddleware.cs`

```csharp
public class UserClaimsForwardingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<UserClaimsForwardingMiddleware> _logger;

    public UserClaimsForwardingMiddleware(RequestDelegate next, ILogger<UserClaimsForwardingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Identify requests from gateway
        context.Request.Headers["X-Forwarded-By"] = "ApiGateway";
        
        // Check if user is authenticated
        if (context.User.Identity?.IsAuthenticated == true)
        {
            // Extract user ID from claims
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                        ?? context.User.FindFirst("sub")?.Value;
            
            var username = context.User.FindFirst(ClaimTypes.Name)?.Value 
                          ?? context.User.FindFirst("unique_name")?.Value;
            
            var email = context.User.FindFirst(ClaimTypes.Email)?.Value 
                       ?? context.User.FindFirst("email")?.Value;

            // Forward user claims as HTTP headers
            if (!string.IsNullOrEmpty(userId))
            {
                context.Request.Headers["X-User-Id"] = userId;
                _logger.LogDebug("Forwarding User ID: {UserId}", userId);
            }
            
            if (!string.IsNullOrEmpty(username))
            {
                context.Request.Headers["X-User-Name"] = username;
            }
            
            if (!string.IsNullOrEmpty(email))
            {
                context.Request.Headers["X-User-Email"] = email;
            }

            _logger.LogInformation(
                "Forwarding authenticated user claims - UserId: {UserId}, Username: {Username}", 
                userId, username);
        }

        await _next(context);
    }
}
```

**Middleware registration:**
```csharp
// Program.cs
app.UseAuthentication();         // Validate JWT
app.UseAuthorization();          // Check policies
app.UseUserClaimsForwarding();   // Forward claims as headers
app.MapReverseProxy();           // Route to services
```

### Middleware Pipeline Order

```
Request arrives → [Middleware Pipeline]

1. UseHttpsRedirection()
   ↓
2. UseAuthentication()        ← JWT validated, User identity set
   ↓
3. UseAuthorization()         ← Policy checked
   ↓
4. UseUserClaimsForwarding()  ← Claims extracted, headers added
   ↓
5. UseRateLimiter()
   ↓
6. MapReverseProxy()          ← Forwarded to backend service
   ↓
Backend Service receives request with headers:
- X-User-Id: 3fa85f64-5717-4562-b3fc-2c963f66afa6
- X-User-Name: john_doe
- X-User-Email: john@example.com
- X-Forwarded-By: ApiGateway
```

### Using Forwarded Claims in Backend Services

**BookingService controller:**
```csharp
[ApiController]
[Route("api/[controller]")]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _service;

    [HttpGet]
    public IActionResult GetMyBookings()
    {
        // Extract user ID from header (set by API Gateway)
        var userId = Request.Headers["X-User-Id"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not provided");
        }
        
        // Query bookings for this user
        var bookings = _service.GetBookingsByUserId(Guid.Parse(userId));
        
        return Ok(bookings);
    }
    
    [HttpGet("{id}")]
    public async Task<IActionResult> GetBooking(Guid id)
    {
        var userId = Request.Headers["X-User-Id"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }
        
        var booking = await _service.GetBookingByIdAsync(id);
        
        if (booking == null)
        {
            return NotFound();
        }
        
        // Authorization check: booking must belong to user
        if (booking.UserId.ToString() != userId)
        {
            return Forbid();  // 403 Forbidden
        }
        
        return Ok(booking);
    }
}
```

### Security Considerations

**Trust boundary:**
```
┌──────────────────────────────────────┐
│         TRUSTED ZONE                 │
│                                      │
│  ┌──────────┐     ┌──────────────┐  │
│  │ Gateway  │────→│   Booking    │  │
│  │          │     │   Service    │  │
│  └──────────┘     └──────────────┘  │
│       │                   │          │
│       └───────────────────┘          │
│     Private Docker network           │
│     (services trust each other)      │
└──────────────────────────────────────┘
         ▲
         │
    Public Internet
         │
   ┌─────┴──────┐
   │   Client   │
   └────────────┘

Security model:
✅ Client MUST authenticate with JWT at gateway
✅ Gateway validates JWT (signature, expiration, issuer)
✅ Gateway extracts claims and forwards as headers
✅ Backend services trust headers from gateway
✅ Backend services are NOT exposed to public
✅ Only gateway is publicly accessible
```

**Important:** Never expose backend services directly to the internet! They trust headers from the gateway without re-validation.

---

## Route-Level Authorization

**Route-level authorization** controls access to entire endpoints based on authentication status or policies.

### Configuration (YARP)

```json
{
  "ReverseProxy": {
    "Routes": {
      "public-route": {
        "ClusterId": "users-cluster",
        "Match": {
          "Path": "/api/users/register"
        }
        // No AuthorizationPolicy = Public
      },
      "protected-route": {
        "ClusterId": "bookings-cluster",
        "AuthorizationPolicy": "authenticated",
        "Match": {
          "Path": "/api/bookings/{**catch-all}"
        }
        // Requires "authenticated" policy
      }
    }
  }
}
```

### Access Control Matrix

| Route | Policy | Authenticated? | Result |
|-------|--------|----------------|--------|
| `/api/users/register` | None | No | ✅ Allow |
| `/api/users/register` | None | Yes | ✅ Allow |
| `/api/users/login` | None | No | ✅ Allow |
| `/api/bookings` | authenticated | No | ❌ 401 |
| `/api/bookings` | authenticated | Yes | ✅ Allow |
| `/api/payments` | authenticated | No | ❌ 401 |
| `/api/payments` | authenticated | Yes | ✅ Allow |

### Public vs Protected Routes

**Public routes (no policy):**
```
/api/users/register  → Anyone can register
/api/users/login     → Anyone can login
/health              → Health check (monitoring)
```

**Protected routes (requires authenticated policy):**
```
/api/bookings        → Must have valid JWT
/api/payments        → Must have valid JWT
/api/inventory       → Must have valid JWT
/api/users/profile   → Must have valid JWT
```

---

## Resource-Level Authorization

**Resource-level authorization** controls access to specific resources (e.g., "Can user edit booking #123?").

### Ownership Checks

**Pattern:** User can only access their own resources

```csharp
[HttpGet("{id}")]
public async Task<IActionResult> GetBooking(Guid id)
{
    // 1. Get user ID from header (gateway-provided)
    var userId = Request.Headers["X-User-Id"].FirstOrDefault();
    
    if (string.IsNullOrEmpty(userId))
    {
        return Unauthorized();
    }
    
    // 2. Fetch resource from database
    var booking = await _service.GetBookingByIdAsync(id);
    
    if (booking == null)
    {
        return NotFound();
    }
    
    // 3. Authorization check: resource ownership
    if (booking.UserId.ToString() != userId)
    {
        return Forbid();  // 403 Forbidden - user doesn't own this booking
    }
    
    // 4. User owns resource, return it
    return Ok(booking);
}
```

### Resource Authorization Service

**Centralized authorization logic:**

```csharp
public interface IAuthorizationService
{
    Task<bool> CanAccessBookingAsync(Guid bookingId, Guid userId);
    Task<bool> CanCancelBookingAsync(Guid bookingId, Guid userId);
    Task<bool> CanViewPaymentAsync(Guid paymentId, Guid userId);
}

public class ResourceAuthorizationService : IAuthorizationService
{
    private readonly BookingDbContext _context;
    
    public async Task<bool> CanAccessBookingAsync(Guid bookingId, Guid userId)
    {
        var booking = await _context.Bookings.FindAsync(bookingId);
        
        if (booking == null)
        {
            return false;  // Resource doesn't exist
        }
        
        // User must own the booking
        return booking.UserId == userId;
    }
    
    public async Task<bool> CanCancelBookingAsync(Guid bookingId, Guid userId)
    {
        var booking = await _context.Bookings.FindAsync(bookingId);
        
        if (booking == null)
        {
            return false;
        }
        
        // Can cancel if:
        // 1. User owns booking, AND
        // 2. Booking is not already cancelled, AND
        // 3. Event hasn't started yet
        return booking.UserId == userId
            && booking.Status != BookingStatus.Cancelled
            && booking.EventDate > DateTime.UtcNow;
    }
    
    public async Task<bool> CanViewPaymentAsync(Guid paymentId, Guid userId)
    {
        var payment = await _context.Payments
            .Include(p => p.Booking)
            .FirstOrDefaultAsync(p => p.Id == paymentId);
        
        if (payment == null)
        {
            return false;
        }
        
        // User must own the booking associated with payment
        return payment.Booking.UserId == userId;
    }
}
```

**Controller usage:**
```csharp
[HttpDelete("{id}")]
public async Task<IActionResult> CancelBooking(Guid id)
{
    var userId = Request.Headers["X-User-Id"].FirstOrDefault();
    
    if (string.IsNullOrEmpty(userId))
    {
        return Unauthorized();
    }
    
    // Check authorization
    var canCancel = await _authService.CanCancelBookingAsync(id, Guid.Parse(userId));
    
    if (!canCancel)
    {
        return Forbid();  // 403 Forbidden
    }
    
    // Perform cancellation
    await _bookingService.CancelBookingAsync(id);
    
    return NoContent();
}
```

### Multi-Tenant Resource Access

**Scenario:** Admin can access any resource, regular users only their own

```csharp
[HttpGet("{id}")]
public async Task<IActionResult> GetBooking(Guid id)
{
    var userId = Request.Headers["X-User-Id"].FirstOrDefault();
    var userRole = Request.Headers["X-User-Role"].FirstOrDefault();
    
    if (string.IsNullOrEmpty(userId))
    {
        return Unauthorized();
    }
    
    var booking = await _service.GetBookingByIdAsync(id);
    
    if (booking == null)
    {
        return NotFound();
    }
    
    // Admin can view any booking
    if (userRole == "Admin")
    {
        return Ok(booking);
    }
    
    // Regular user can only view their own
    if (booking.UserId.ToString() != userId)
    {
        return Forbid();
    }
    
    return Ok(booking);
}
```

---

## Service-to-Service Authorization

### The Challenge

What if services need to call each other?

```
Client → Gateway → BookingService → PaymentService

Problem: PaymentService needs to know if request is:
1. From authenticated client (via Gateway), OR
2. From another service (BookingService)

Current solution: X-Forwarded-By header identifies gateway requests
```

### Trusted Service Pattern

**Option 1: Header-based trust (our implementation)**

```csharp
// PaymentService checks header
public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request)
{
    var forwardedBy = Request.Headers["X-Forwarded-By"].FirstOrDefault();
    
    if (forwardedBy != "ApiGateway")
    {
        return Unauthorized("Request must come through API Gateway");
    }
    
    // Trust gateway-provided user context
    var userId = Request.Headers["X-User-Id"].FirstOrDefault();
    
    // Process payment...
}
```

**Option 2: Service-to-service JWT (more secure)**

```csharp
// BookingService generates service JWT
var serviceToken = GenerateServiceJwt("BookingService");

// BookingService calls PaymentService
var response = await _httpClient.PostAsync("http://paymentservice/api/payments", 
    new StringContent(jsonPayload), 
    headers: new { Authorization = $"Bearer {serviceToken}" });

// PaymentService validates service JWT
// - Check issuer = "BookingService"
// - Check audience = "PaymentService"
// - Check claims contain service identity
```

**Option 3: Mutual TLS (mTLS)**

```
Services authenticate each other using client certificates
- BookingService has certificate signed by CA
- PaymentService validates certificate
- Ensures request came from trusted service

✅ Most secure
❌ Complex setup (PKI infrastructure)
❌ Certificate management overhead
```

### Our Implementation

We use **header-based trust** with network isolation:

```
✅ Services communicate over private Docker network
✅ Services not exposed to public internet
✅ Only gateway is publicly accessible
✅ X-Forwarded-By header identifies gateway requests
✅ Simple and sufficient for learning project

Production enhancement:
→ Add service-to-service JWTs
→ Or implement mTLS
```

---

## Testing Authorization

### Test Scenario 1: Authenticated Access

```bash
# Step 1: Login
TOKEN=$(curl -s -X POST http://localhost:5001/api/users/login \
  -H "Content-Type: application/json" \
  -d '{"username":"john_doe","password":"SecurePass123!"}' \
  | jq -r '.token')

# Step 2: Access protected endpoint with JWT
curl -X GET http://localhost:5000/api/bookings \
  -H "Authorization: Bearer $TOKEN"

# Response: 200 OK with bookings
[
  {
    "id": "abc123...",
    "userId": "3fa85f64...",
    "eventName": "Concert",
    "status": "Confirmed"
  }
]
```

### Test Scenario 2: Unauthenticated Access

```bash
# Access protected endpoint without JWT
curl -X GET http://localhost:5000/api/bookings

# Response: 401 Unauthorized
{
  "type": "https://tools.ietf.org/html/rfc7235#section-3.1",
  "title": "Unauthorized",
  "status": 401
}
```

### Test Scenario 3: Resource Ownership

```bash
# User john_doe tries to access another user's booking
USER1_TOKEN=$(curl -s -X POST http://localhost:5001/api/users/login \
  -d '{"username":"john_doe","password":"pass123"}' | jq -r '.token')

USER2_TOKEN=$(curl -s -X POST http://localhost:5001/api/users/login \
  -d '{"username":"jane_smith","password":"pass456"}' | jq -r '.token')

# User 2 creates booking
BOOKING_ID=$(curl -s -X POST http://localhost:5000/api/bookings \
  -H "Authorization: Bearer $USER2_TOKEN" \
  -d '{"eventName":"Concert","eventDate":"2024-12-01"}' \
  | jq -r '.id')

# User 1 tries to access User 2's booking
curl -X GET http://localhost:5000/api/bookings/$BOOKING_ID \
  -H "Authorization: Bearer $USER1_TOKEN"

# Response: 403 Forbidden
{
  "error": "Access denied",
  "message": "You do not have permission to access this resource"
}
```

### Test Scenario 4: Claims Forwarding

```bash
# Verify headers received by backend service
curl -X GET http://localhost:5000/api/bookings/debug-headers \
  -H "Authorization: Bearer $TOKEN"

# Response: Headers forwarded by gateway
{
  "headers": {
    "X-User-Id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "X-User-Name": "john_doe",
    "X-User-Email": "john@example.com",
    "X-Forwarded-By": "ApiGateway",
    "Authorization": "Bearer eyJhbGc..."
  }
}
```

---

## Common Pitfalls

### 1. Confusing 401 vs 403

**401 Unauthorized:**
```
Meaning: Authentication failed
Reason: No credentials, invalid credentials, expired token
Message: "Who are you? I don't recognize you."
Solution: Login or refresh token
```

**403 Forbidden:**
```
Meaning: Authorization failed
Reason: Authenticated but lacks permission
Message: "I know who you are, but you can't do this."
Solution: Request access or check resource ownership
```

**Example:**
```csharp
// ❌ Wrong: Using Unauthorized for authorization failure
if (booking.UserId != currentUserId)
{
    return Unauthorized();  // Wrong! User IS authenticated
}

// ✅ Correct: Using Forbid for authorization failure
if (booking.UserId != currentUserId)
{
    return Forbid();  // Correct! User lacks permission
}
```

### 2. Forgetting Resource Ownership Checks

**Vulnerable code:**
```csharp
[HttpGet("{id}")]
public async Task<IActionResult> GetBooking(Guid id)
{
    var booking = await _service.GetBookingByIdAsync(id);
    
    if (booking == null)
    {
        return NotFound();
    }
    
    return Ok(booking);  // ❌ Any authenticated user can see any booking!
}
```

**Secure code:**
```csharp
[HttpGet("{id}")]
public async Task<IActionResult> GetBooking(Guid id)
{
    var userId = Request.Headers["X-User-Id"].FirstOrDefault();
    var booking = await _service.GetBookingByIdAsync(id);
    
    if (booking == null)
    {
        return NotFound();
    }
    
    // ✅ Check ownership
    if (booking.UserId.ToString() != userId)
    {
        return Forbid();
    }
    
    return Ok(booking);
}
```

### 3. Trusting Client-Provided User ID

**Vulnerable code:**
```csharp
[HttpPost]
public IActionResult CreateBooking([FromBody] CreateBookingRequest request)
{
    // ❌ Client provides userId in request body - CAN BE FAKED!
    var booking = new Booking
    {
        UserId = request.UserId,  // Attacker can set any user ID
        EventName = request.EventName
    };
    
    _service.SaveBooking(booking);
    return Ok();
}
```

**Secure code:**
```csharp
[HttpPost]
public IActionResult CreateBooking([FromBody] CreateBookingRequest request)
{
    // ✅ Get user ID from gateway-provided header
    var userId = Request.Headers["X-User-Id"].FirstOrDefault();
    
    if (string.IsNullOrEmpty(userId))
    {
        return Unauthorized();
    }
    
    var booking = new Booking
    {
        UserId = Guid.Parse(userId),  // Use authenticated user ID
        EventName = request.EventName
    };
    
    _service.SaveBooking(booking);
    return Ok();
}
```

### 4. Missing Header Validation

**Insecure code:**
```csharp
[HttpGet]
public IActionResult GetMyBookings()
{
    var userId = Request.Headers["X-User-Id"].FirstOrDefault();
    
    // ❌ What if header is missing?
    var bookings = _service.GetBookingsByUserId(Guid.Parse(userId));  // NullReferenceException!
    
    return Ok(bookings);
}
```

**Secure code:**
```csharp
[HttpGet]
public IActionResult GetMyBookings()
{
    var userId = Request.Headers["X-User-Id"].FirstOrDefault();
    
    // ✅ Validate header present and valid
    if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
    {
        return Unauthorized("User context missing or invalid");
    }
    
    var bookings = _service.GetBookingsByUserId(userGuid);
    
    return Ok(bookings);
}
```

### 5. Information Disclosure via 404 vs 403

**Information leaking:**
```csharp
// Attacker tries to access booking #123
GET /api/bookings/123

Response: 404 Not Found → Booking doesn't exist
Response: 403 Forbidden → Booking exists, but belongs to someone else

❌ Attacker learns which booking IDs exist!
```

**Security-conscious approach:**
```csharp
[HttpGet("{id}")]
public async Task<IActionResult> GetBooking(Guid id)
{
    var userId = Request.Headers["X-User-Id"].FirstOrDefault();
    var booking = await _service.GetBookingByIdAsync(id);
    
    // Return 404 for both "doesn't exist" and "not owned by user"
    if (booking == null || booking.UserId.ToString() != userId)
    {
        return NotFound();  // Don't reveal if booking exists
    }
    
    return Ok(booking);
}
```

---

## Best Practices

### 1. Validate User Context Headers

```csharp
// Helper method to extract and validate user context
private bool TryGetUserId(out Guid userId)
{
    var userIdHeader = Request.Headers["X-User-Id"].FirstOrDefault();
    return Guid.TryParse(userIdHeader, out userId);
}

// Usage
[HttpGet]
public IActionResult GetMyBookings()
{
    if (!TryGetUserId(out var userId))
    {
        return Unauthorized("User context missing");
    }
    
    var bookings = _service.GetBookingsByUserId(userId);
    return Ok(bookings);
}
```

### 2. Use Authorization Service for Complex Logic

```csharp
// Don't scatter authorization logic in controllers
// Centralize in service

public interface IBookingAuthorizationService
{
    Task<bool> CanAccessAsync(Guid bookingId, Guid userId);
    Task<bool> CanCancelAsync(Guid bookingId, Guid userId);
    Task<bool> CanModifyAsync(Guid bookingId, Guid userId);
}

// Controller stays clean
[HttpGet("{id}")]
public async Task<IActionResult> GetBooking(Guid id)
{
    if (!TryGetUserId(out var userId))
    {
        return Unauthorized();
    }
    
    if (!await _authService.CanAccessAsync(id, userId))
    {
        return Forbid();
    }
    
    var booking = await _service.GetBookingByIdAsync(id);
    return Ok(booking);
}
```

### 3. Log Authorization Failures

```csharp
[HttpGet("{id}")]
public async Task<IActionResult> GetBooking(Guid id)
{
    if (!TryGetUserId(out var userId))
    {
        _logger.LogWarning("Authorization failed: User context missing for booking {BookingId}", id);
        return Unauthorized();
    }
    
    var booking = await _service.GetBookingByIdAsync(id);
    
    if (booking == null || booking.UserId != userId)
    {
        _logger.LogWarning(
            "Authorization failed: User {UserId} attempted to access booking {BookingId} owned by {OwnerId}",
            userId, id, booking?.UserId);
        return NotFound();
    }
    
    return Ok(booking);
}
```

### 4. Use Policies for Reusable Authorization

```csharp
// Define policy
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanManageBookings", policy =>
    {
        policy.RequireAssertion(context =>
        {
            // Admin or Manager can manage all bookings
            return context.User.IsInRole("Admin") || context.User.IsInRole("Manager");
        });
    });
});

// Apply to controller
[Authorize(Policy = "CanManageBookings")]
public class AdminBookingsController : ControllerBase
{
    // All actions require CanManageBookings policy
}
```

### 5. Fail Securely

```csharp
// ❌ Bad: Fails open (allows access on error)
try
{
    if (await _authService.CanAccessAsync(bookingId, userId))
    {
        return Ok(booking);
    }
    return Forbid();
}
catch
{
    return Ok(booking);  // Oops! Grants access on error
}

// ✅ Good: Fails closed (denies access on error)
try
{
    if (await _authService.CanAccessAsync(bookingId, userId))
    {
        return Ok(booking);
    }
    return Forbid();
}
catch (Exception ex)
{
    _logger.LogError(ex, "Authorization check failed for booking {BookingId}", bookingId);
    return StatusCode(500, "Authorization check failed");  // Deny on error
}
```

---

## Interview Questions

### Q1: What's the difference between authentication and authorization?

**Answer:**
- **Authentication** verifies WHO you are (identity)
- **Authorization** determines WHAT you can do (permissions)

Example: JWT validation is authentication. Checking if booking belongs to user is authorization. You need authentication first, then authorization.

---

### Q2: Explain the claims forwarding pattern and why it's useful.

**Answer:**
Claims forwarding extracts user information from JWT at the gateway and forwards it as HTTP headers to backend services.

**Why useful:**
- Backend services don't need JWT validation logic
- Services get user context without coupling to auth system
- Gateway becomes single point of authentication
- Services stay simple and focused on business logic

**Flow:**
1. Gateway validates JWT
2. Extracts claims (user ID, name, email)
3. Adds as headers (X-User-Id, X-User-Name, X-User-Email)
4. Forwards to backend service
5. Service reads headers to know user context

---

### Q3: When should you return 401 vs 403?

**Answer:**
- **401 Unauthorized:** Authentication failed (no/invalid credentials)
  - "I don't know who you are"
  - Solution: Login or provide valid token
  
- **403 Forbidden:** Authorization failed (authenticated but lacks permission)
  - "I know who you are, but you can't do this"
  - Solution: Request access or verify resource ownership

**Example:**
- No JWT token → 401
- Valid JWT, but accessing someone else's booking → 403

---

### Q4: How do you prevent users from accessing other users' resources?

**Answer:**
Implement resource-level authorization (ownership checks):

```csharp
// 1. Get authenticated user ID from header
var userId = Request.Headers["X-User-Id"].FirstOrDefault();

// 2. Fetch resource from database
var booking = await _service.GetBookingByIdAsync(bookingId);

// 3. Check ownership
if (booking.UserId.ToString() != userId)
{
    return Forbid();  // 403 Forbidden
}

// 4. Allow access
return Ok(booking);
```

Never trust client-provided user IDs—always use gateway-authenticated context.

---

### Q5: What security risks exist if backend services are exposed directly to the internet?

**Answer:**
Backend services trust headers from the gateway (X-User-Id, etc.) without validation.

**If exposed directly:**
- Attacker can send fake headers:
  ```
  GET /api/bookings
  X-User-Id: admin-user-id-12345
  X-User-Name: admin
  ```
- Service trusts header and grants admin access
- Complete security bypass!

**Solution:**
- Only expose gateway to public
- Backend services on private network
- Services validate X-Forwarded-By: ApiGateway header
- Consider service-to-service JWTs for production

---

### Q6: Implement role-based authorization for admin-only endpoint.

**Answer:**
```csharp
// 1. Add role claim to JWT (UserService)
var claims = new[]
{
    new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
    new Claim(ClaimTypes.Role, "Admin")  // Role claim
};

// 2. Forward role as header (Gateway middleware)
var role = context.User.FindFirst(ClaimTypes.Role)?.Value;
if (!string.IsNullOrEmpty(role))
{
    context.Request.Headers["X-User-Role"] = role;
}

// 3. Check role in backend service
[HttpDelete("{id}")]
public IActionResult DeleteUser(Guid id)
{
    var role = Request.Headers["X-User-Role"].FirstOrDefault();
    
    if (role != "Admin")
    {
        return Forbid();  // Only admins can delete users
    }
    
    _service.DeleteUser(id);
    return NoContent();
}

// Alternative: Use policy at gateway level
options.AddPolicy("admin", policy => policy.RequireRole("Admin"));
"AuthorizationPolicy": "admin"
```

---

### Q7: How would you implement audit logging for authorization failures?

**Answer:**
```csharp
// Authorization service with logging
public class BookingAuthorizationService : IBookingAuthorizationService
{
    private readonly ILogger<BookingAuthorizationService> _logger;
    private readonly BookingDbContext _context;
    
    public async Task<bool> CanAccessAsync(Guid bookingId, Guid userId)
    {
        var booking = await _context.Bookings.FindAsync(bookingId);
        
        if (booking == null)
        {
            _logger.LogWarning(
                "Authorization denied: Booking {BookingId} not found for user {UserId}",
                bookingId, userId);
            return false;
        }
        
        if (booking.UserId != userId)
        {
            _logger.LogWarning(
                "Authorization denied: User {UserId} attempted to access booking {BookingId} owned by {OwnerId}",
                userId, bookingId, booking.UserId);
            return false;
        }
        
        _logger.LogInformation(
            "Authorization granted: User {UserId} accessed booking {BookingId}",
            userId, bookingId);
        return true;
    }
}

// Send to Seq/ELK for analysis
// Monitor patterns: repeated failures = potential attack
```

---

### Q8: Design a multi-tenant system where admins see all resources, users see only theirs.

**Answer:**
```csharp
public class BookingsController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetBookings()
    {
        var userId = Request.Headers["X-User-Id"].FirstOrDefault();
        var userRole = Request.Headers["X-User-Role"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }
        
        IEnumerable<Booking> bookings;
        
        // Admin sees all bookings
        if (userRole == "Admin")
        {
            bookings = await _service.GetAllBookingsAsync();
            _logger.LogInformation("Admin {UserId} accessed all bookings", userId);
        }
        else
        {
            // Regular user sees only their bookings
            bookings = await _service.GetBookingsByUserIdAsync(Guid.Parse(userId));
            _logger.LogInformation("User {UserId} accessed their bookings", userId);
        }
        
        return Ok(bookings);
    }
}
```

---

## Summary

Authorization in microservices requires a layered approach:

**Layer 1: Route-Level (Gateway)**
- Policy-based authorization (authenticated, admin, etc.)
- Applied at YARP route configuration
- Blocks unauthenticated requests early

**Layer 2: Claims Forwarding (Gateway)**
- Extract user identity from JWT
- Forward as HTTP headers (X-User-Id, X-User-Name, X-User-Email)
- Provides user context to backend services

**Layer 3: Resource-Level (Services)**
- Check resource ownership
- Verify user has permission for specific action
- Business logic authorization

**Key Takeaways:**
1. Authentication (who) comes before authorization (what)
2. Return 401 for authentication failures, 403 for authorization failures
3. Always validate resource ownership—don't trust client-provided IDs
4. Use claims forwarding to share user context across services
5. Keep backend services on private network (trust gateway)
6. Log authorization failures for security monitoring
7. Fail securely—deny access on errors

This layered approach keeps services decoupled while maintaining strong security across the microservices architecture!
