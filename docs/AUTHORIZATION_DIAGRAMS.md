# Authorization Flow Diagrams

## 1. User Registration & Login Flow

```
┌─────────┐                 ┌─────────────┐                 ┌─────────────┐
│ Client  │                 │ API Gateway │                 │ UserService │
└────┬────┘                 └──────┬──────┘                 └──────┬──────┘
     │                             │                               │
     │ 1. POST /api/users/register │                               │
     │ {username, email, password} │                               │
     ├────────────────────────────>│                               │
     │                             │ 2. Forward request            │
     │                             ├──────────────────────────────>│
     │                             │                               │
     │                             │                     3. Validate data
     │                             │                     4. Hash password
     │                             │                     5. Save to DB
     │                             │                               │
     │                             │ 6. Return user data           │
     │                             │<──────────────────────────────┤
     │ 7. 200 OK {user}            │                               │
     │<────────────────────────────┤                               │
     │                             │                               │
     │ 8. POST /api/users/login    │                               │
     │ {username, password}        │                               │
     ├────────────────────────────>│                               │
     │                             │ 9. Forward request            │
     │                             ├──────────────────────────────>│
     │                             │                               │
     │                             │                  10. Verify password
     │                             │                  11. Generate JWT
     │                             │                      {                               │                             │                        "sub": "user-id",
     │                             │                        "unique_name": "username",
     │                             │                        "email": "email",
     │                             │                        "exp": 1234567890
     │                             │                      }
     │                             │                               │
     │                             │ 12. Return JWT token          │
     │                             │<──────────────────────────────┤
     │ 13. 200 OK {token, user}    │                               │
     │<────────────────────────────┤                               │
     │                             │                               │
     │ 14. Store token locally     │                               │
     │                             │                               │
```

## 2. Authenticated Request Flow

```
┌─────────┐         ┌─────────────┐         ┌──────────────┐
│ Client  │         │ API Gateway │         │BookingService│
└────┬────┘         └──────┬──────┘         └──────┬───────┘
     │                     │                       │
     │ 1. GET /api/bookings│                       │
     │ Authorization: Bearer <JWT_TOKEN>           │
     ├────────────────────>│                       │
     │                     │                       │
     │         2. Validate JWT Token               │
     │            ├─ Check signature                │
     │            ├─ Verify issuer                 │
     │            ├─ Verify audience                │
     │            └─ Check expiration              │
     │                     │                       │
     │         3. Extract Claims from JWT          │
     │            ├─ sub → User ID                 │
     │            ├─ unique_name → Username        │
     │            └─ email → Email                 │
     │                     │                       │
     │         4. Check Authorization Policy       │
     │            (Route requires "authenticated") │
     │                     │                       │
     │         5. Add Headers to Request           │
     │            ├─ X-User-Id: {guid}             │
     │            ├─ X-User-Name: {username}       │
     │            └─ X-User-Email: {email}         │
     │                     │                       │
     │                     │ 6. Forward request    │
     │                     │   with user headers   │
     │                     ├──────────────────────>│
     │                     │                       │
     │                     │            7. Read X-User-Id header
     │                     │            8. Get user's bookings
     │                     │            9. Return bookings
     │                     │                       │
     │                     │ 10. Response          │
     │                     │<──────────────────────┤
     │ 11. 200 OK          │                       │
     │<────────────────────┤                       │
     │                     │                       │
```

## 3. Unauthorized Request Flow

```
┌─────────┐                 ┌─────────────┐
│ Client  │                 │ API Gateway │
└────┬────┘                 └──────┬──────┘
     │                             │
     │ 1. GET /api/bookings        │
     │ (No Authorization header)   │
     ├────────────────────────────>│
     │                             │
     │         2. No token found   │
     │            ↓                │
     │         3. Return 401       │
     │                             │
     │ 4. 401 Unauthorized         │
     │<────────────────────────────┤
     │                             │
```

## 4. Invalid/Expired Token Flow

```
┌─────────┐                 ┌─────────────┐
│ Client  │                 │ API Gateway │
└────┬────┘                 └──────┬──────┘
     │                             │
     │ 1. GET /api/bookings        │
     │ Authorization: Bearer <EXPIRED_TOKEN>
     ├────────────────────────────>│
     │                             │
     │         2. Validate token   │
     │            ├─ Signature OK  │
     │            ├─ Issuer OK     │
     │            └─ EXPIRED ❌    │
     │                             │
     │         3. Token invalid    │
     │            ↓                │
     │         4. Return 401       │
     │                             │
     │ 5. 401 Unauthorized         │
     │    "Token expired"          │
     │<────────────────────────────┤
     │                             │
     │ 6. Client needs to re-login │
     │                             │
```

## 5. Resource Ownership Check Flow

```
┌─────────┐    ┌─────────────┐    ┌──────────────┐
│ Client  │    │ API Gateway │    │BookingService│
└────┬────┘    └──────┬──────┘    └──────┬───────┘
     │                │                   │
     │ 1. GET /api/bookings/{booking-id}  │
     │ Authorization: Bearer <TOKEN>      │
     ├───────────────>│                   │
     │                │                   │
     │    2. Validate token (User A)      │
     │                │                   │
     │    3. Forward with X-User-Id=A     │
     │                ├──────────────────>│
     │                │                   │
     │                │        4. Get booking from DB
     │                │           ├─ booking.userId = B
     │                │           ├─ X-User-Id = A
     │                │           └─ A ≠ B ❌
     │                │                   │
     │                │        5. Not owned by user
     │                │           ↓
     │                │        6. Return 403 Forbidden
     │                │                   │
     │                │ 7. 403 Response   │
     │                │<──────────────────┤
     │ 8. 403 Forbidden│                  │
     │<───────────────┤                   │
     │                │                   │
```

## 6. Complete System Architecture

```
┌────────────────────────────────────────────────────────┐
│                     CLIENT LAYER                        │
│  - Web Browser                                          │
│  - Mobile App                                           │
│  - External API Consumer                                │
└───────────────┬────────────────────────────────────────┘
                │
                │ HTTPS (Authorization: Bearer <token>)
                │
                ▼
┌────────────────────────────────────────────────────────┐
│               API GATEWAY (Port 5000)                   │
│  ┌──────────────────────────────────────────────────┐  │
│  │ Middleware Pipeline:                              │  │
│  │ 1. Exception Handler                             │  │
│  │ 2. CORS                                          │  │
│  │ 3. ✓ JWT Authentication ← Validates token        │  │
│  │ 4. ✓ Authorization ← Checks policies             │  │
│  │ 5. ✓ Claims Forwarding ← Adds user headers       │  │
│  │ 6. Request/Response Logging                      │  │
│  │ 7. YARP Reverse Proxy ← Routes to services      │  │
│  └──────────────────────────────────────────────────┘  │
│                                                         │
│  Route Configuration:                                   │
│  • /api/users/** → Public (login/register)              │
│  • /api/bookings/** → Requires authentication           │
│  • /api/payments/** → Requires authentication           │
└───────────┬──────────────┬──────────────┬──────────────┘
            │              │              │
            │ Internal     │ Internal     │ Internal
            │ Network      │ Network      │ Network
            │              │              │
            ▼              ▼              ▼
┌───────────────────┐ ┌──────────────┐ ┌────────────────┐
│   UserService     │ │BookingService│ │PaymentService  │
│   (Port 5001)     │ │ (Port 5002)  │ │  (Port 5003)   │
│                   │ │              │ │                │
│ Responsibilities: │ │Receives:     │ │Receives:       │
│ • Register        │ │ • Request    │ │ • Request      │
│ • Login           │ │ • X-User-Id  │ │ • X-User-Id    │
│ • Generate JWT    │ │ • X-User-Name│ │ • X-User-Name  │
│                   │ │ • X-User-Email│ │ • X-User-Email│
│ JWT Claims:       │ │              │ │                │
│ • sub (User ID)   │ │Actions:      │ │Actions:        │
│ • unique_name     │ │ • Create     │ │ • Process      │
│ • email           │ │   booking    │ │   payment      │
│ • exp             │ │ • Check      │ │ • Link to user │
│                   │ │   ownership  │ │                │
└─────┬─────────────┘ └──────┬───────┘ └────────┬───────┘
      │                      │                   │
      ▼                      ▼                   ▼
┌──────────────┐      ┌──────────────┐    ┌──────────────┐
│ PostgreSQL   │      │ PostgreSQL   │    │   MongoDB    │
│   UserDB     │      │  BookingDB   │    │  PaymentDB   │
└──────────────┘      └──────────────┘    └──────────────┘
```

## 7. JWT Token Structure

```
Header:
{
  "alg": "HS256",           ← Algorithm
  "typ": "JWT"              ← Type
}

Payload (Claims):
{
  "sub": "123e4567-...",    ← Subject (User ID)
  "unique_name": "john_doe", ← Username
  "email": "john@ex.com",   ← Email
  "jti": "token-uuid",      ← Token ID
  "iss": "UserService",     ← Issuer
  "aud": "BookingSystem",   ← Audience
  "exp": 1735567890,        ← Expiration (Unix timestamp)
  "iat": 1735564290         ← Issued At (Unix timestamp)
}

Signature:
HMACSHA256(
  base64UrlEncode(header) + "." +
  base64UrlEncode(payload),
  JWT_SECRET_KEY
)

Final Token Format:
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.
eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.
SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c

     ↑ Header          ↑ Payload              ↑ Signature
```

## 8. Security Decision Tree

```
                    ┌─────────────────┐
                    │ Request arrives │
                    └────────┬────────┘
                             │
                   ┌─────────▼─────────┐
                   │ Has Authorization │
                   │     header?       │
                   └─┬─────────────┬───┘
                     │ YES         │ NO
                     │             │
                ┌────▼───┐    ┌────▼────┐
                │Extract │    │ Return  │
                │ Token  │    │ 401     │
                └────┬───┘    └─────────┘
                     │
                ┌────▼─────┐
                │ Validate │
                │  Token   │
                └─┬─────┬──┘
                  │     │
            Valid │     │ Invalid
                  │     │
             ┌────▼──┐ ┌▼─────────┐
             │Extract│ │ Return   │
             │Claims │ │ 401      │
             └───┬───┘ └──────────┘
                 │
          ┌──────▼──────┐
          │ Check       │
          │ Authorization│
          │ Policy      │
          └─┬─────────┬─┘
            │         │
      Allowed│       │ Denied
            │         │
       ┌────▼───┐  ┌──▼──────┐
       │Forward │  │ Return  │
       │to      │  │ 403     │
       │Service │  └─────────┘
       └────┬───┘
            │
       ┌────▼─────────┐
       │ Service      │
       │ Processes    │
       │ Request      │
       └──────────────┘
```

## Key Concepts

### 🔐 Authentication
**"Who are you?"**
- Verifying the user's identity
- Handled by JWT token validation
- Results in knowing the user ID

### 🛡️ Authorization
**"What are you allowed to do?"**
- Verifying permissions/access rights
- Handled by policies and ownership checks
- Results in allowing or denying the action

### 🎫 JWT Token
- Self-contained authentication token
- Contains user claims (ID, name, email)
- Signed by UserService
- Validated by API Gateway
- Cannot be tampered with (signature verification)

### 📨 Claims Forwarding
- Gateway extracts validated user info from JWT
- Forwards as HTTP headers to services
- Services don't need to validate JWT
- Trust model: services trust the gateway
