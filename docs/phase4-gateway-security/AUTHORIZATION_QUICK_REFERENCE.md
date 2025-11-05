# Authorization Quick Reference Card

## 🎯 Summary: Yes, Your UserService MUST Implement Authorization

Your **UserService** is responsible for:
- ✅ **Generating JWT tokens** (DONE)
- ✅ **User registration and login** (DONE)
- ⚠️ **Protecting its own endpoints** (NEEDS IMPLEMENTATION)

Your **API Gateway** is responsible for:
- ✅ **Validating JWT tokens** (DONE)
- ✅ **Enforcing authorization policies** (DONE)
- ✅ **Forwarding user claims to services** (DONE)

Your **Other Services** (BookingService, PaymentService) need:
- 🔄 **Read user info from headers** (PENDING IMPLEMENTATION)
- 🔄 **Implement business logic based on user** (PENDING IMPLEMENTATION)

---

## 🏗️ How Authorization Works

### Step-by-Step Flow

1. **User Registers/Logs In** → UserService generates JWT token
2. **Client includes token** → `Authorization: Bearer <token>` header
3. **API Gateway validates** → Checks signature, issuer, expiration
4. **Gateway forwards user info** → Adds `X-User-Id`, `X-User-Name`, `X-User-Email` headers
5. **Services use user info** → Read headers to know who is making the request

---

## 📋 Implementation Checklist

### ✅ Completed

- [x] JWT token generation in UserService
- [x] JWT validation in API Gateway
- [x] Authorization policies for protected routes
- [x] User claims forwarding middleware
- [x] Configuration for JWT settings

### 🔄 Pending

- [ ] Protect UserService's `GET /api/users/{id}` endpoint
- [ ] Implement BookingService endpoints with user context
- [ ] Implement PaymentService endpoints with user context
- [ ] Add role-based authorization (future)
- [ ] Add refresh token support (future)

---

## 🔑 Key Files Modified

| File | Purpose |
|------|---------|
| `src/ApiGateway/Program.cs` | Added JWT authentication & authorization |
| `src/ApiGateway/Middleware/UserClaimsForwardingMiddleware.cs` | Forwards user claims as headers |
| `src/ApiGateway/appsettings.json` | Added JWT config & route policies |
| `src/ApiGateway/ApiGateway.csproj` | Added JWT Bearer package |
| `src/UserService/appsettings.json` | Fixed JwtSettings configuration |

---

## 🎓 Core Concepts

### Authentication vs Authorization

| Concept | Question | Implementation |
|---------|----------|----------------|
| **Authentication** | "Who are you?" | JWT token validation |
| **Authorization** | "What can you do?" | Route policies & ownership checks |

### JWT Token Structure

```
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9  ← Header
.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ  ← Claims
.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c  ← Signature
```

Claims include:
- `sub` - User ID
- `unique_name` - Username
- `email` - Email address
- `exp` - Expiration timestamp
- `iss` - Issuer (UserService)
- `aud` - Audience (BookingSystem)

---

## 💻 Code Examples

### 1. Login to Get Token

```bash
curl -X POST http://localhost:5000/api/users/login \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","password":"password"}'
```

**Response:**
```json
{
  "success": true,
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "user": { "id": "guid", "username": "testuser" }
  }
}
```

### 2. Use Token in Request

```bash
curl -X GET http://localhost:5000/api/bookings \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

### 3. Read User in Service

```csharp
// In BookingService or PaymentService
var userId = httpContext.Request.Headers["X-User-Id"].FirstOrDefault();
var username = httpContext.Request.Headers["X-User-Name"].FirstOrDefault();
var email = httpContext.Request.Headers["X-User-Email"].FirstOrDefault();

if (string.IsNullOrEmpty(userId))
{
    return Results.Unauthorized();
}

// Use userId for business logic
var userGuid = Guid.Parse(userId);
var bookings = await GetUserBookingsAsync(userGuid);
```

---

## ⚙️ Configuration

### Environment Variables

```bash
# .env file
JWT_SECRET_KEY=YourSuperSecretKeyForJWTTokenGeneration123!
JWT_ISSUER=BookingSystem.UserService
JWT_AUDIENCE=BookingSystem.Clients
JWT_EXPIRY_MINUTES=60
```

### UserService appsettings.json

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

### API Gateway appsettings.json

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
        "AuthorizationPolicy": "authenticated"  ← Requires auth
      }
    }
  }
}
```

---

## 🛣️ Route Protection

| Route | Status | Auth Required | Notes |
|-------|--------|---------------|-------|
| `POST /api/users/register` | Public | ❌ No | Anyone can register |
| `POST /api/users/login` | Public | ❌ No | Anyone can login |
| `GET /api/users/{id}` | Public | ⚠️ Should be Yes | TODO: Add protection |
| `GET /api/bookings` | Protected | ✅ Yes | User's bookings only |
| `POST /api/bookings` | Protected | ✅ Yes | Create for authenticated user |
| `GET /api/payments` | Protected | ✅ Yes | User's payments only |
| `POST /api/payments` | Protected | ✅ Yes | Process for authenticated user |

---

## 🔒 Security Best Practices

### ✅ DO

1. **Use strong secret keys** (min 32 characters)
2. **Keep tokens short-lived** (15-60 minutes)
3. **Use HTTPS in production**
4. **Validate user headers in services**
5. **Check resource ownership** before allowing access
6. **Log authentication events**
7. **Use environment variables** for secrets

### ❌ DON'T

1. **Don't hardcode secrets** in code
2. **Don't trust user ID from request body** (use headers)
3. **Don't log tokens** or sensitive data
4. **Don't use same secret** across environments
5. **Don't skip validation** even in "trusted" networks
6. **Don't expose services directly** (always through gateway)

---

## 🐛 Troubleshooting

### "401 Unauthorized" Error

**Possible causes:**
- No token provided
- Token expired
- Invalid token signature
- Wrong issuer/audience

**Solution:**
1. Check token is included: `Authorization: Bearer <token>`
2. Decode token at https://jwt.io to check expiration
3. Verify JWT_SECRET_KEY matches between services
4. Check logs in Seq for validation errors

### "403 Forbidden" Error

**Possible causes:**
- User doesn't own the resource
- User lacks required role/permission

**Solution:**
- Check resource ownership logic
- Verify user ID matches resource owner

### Headers Not Forwarded

**Possible causes:**
- Request not going through API Gateway
- Middleware order incorrect

**Solution:**
- Always use gateway URL: `http://localhost:5000/api/...`
- Check middleware order in `Program.cs`

---

## 📚 Documentation

For detailed information, see:

1. **[AUTHORIZATION_IMPLEMENTATION.md](./AUTHORIZATION_IMPLEMENTATION.md)**
   - Implementation status and next steps

2. **[AUTHORIZATION_GUIDE.md](./AUTHORIZATION_GUIDE.md)**
   - Complete guide with configuration examples
   - Testing and troubleshooting

3. **[SERVICE_AUTHORIZATION.md](./SERVICE_AUTHORIZATION.md)**
   - Service-level code examples
   - Helper methods and patterns

4. **[AUTHORIZATION_DIAGRAMS.md](./AUTHORIZATION_DIAGRAMS.md)**
   - Visual flow diagrams
   - System architecture

---

## 🚀 Quick Start

```bash
# 1. Ensure .env is configured
cp .env.example .env

# 2. Start infrastructure
docker-compose up -d

# 3. Build all services
dotnet build

# 4. Run services (in separate terminals)
dotnet run --project src/ApiGateway
dotnet run --project src/UserService
dotnet run --project src/BookingService
dotnet run --project src/PaymentService

# 5. Test authentication
# Register
curl -X POST http://localhost:5000/api/users/register \
  -H "Content-Type: application/json" \
  -d '{"username":"test","email":"test@test.com","password":"Test123!","firstName":"Test","lastName":"User"}'

# Login
curl -X POST http://localhost:5000/api/users/login \
  -H "Content-Type: application/json" \
  -d '{"username":"test","password":"Test123!"}'

# Use the token from response for subsequent requests
```

---

## 🎯 Next Steps

1. **Implement BookingService endpoints** - See SERVICE_AUTHORIZATION.md
2. **Implement PaymentService endpoints** - See SERVICE_AUTHORIZATION.md  
3. **Protect UserService endpoints** - Add authorization to `GET /users/{id}`
4. **Test complete flow** - Register → Login → Create Booking → Process Payment
5. **Add role-based authorization** - For admin features

---

## 💡 Remember

Your microservices use **Gateway-Level Authentication**:
- ✅ Gateway validates JWT tokens
- ✅ Gateway enforces authorization policies
- ✅ Gateway forwards user claims as headers
- ✅ Services trust the gateway and read headers

This is simpler and more secure than having each service validate JWTs independently!
