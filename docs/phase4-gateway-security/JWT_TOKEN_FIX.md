# JWT Token Decoding Error Fix (IDX14102)

## Problem Description

The API Gateway was throwing the following error:
```
JWT Authentication failed: IDX14102: Unable to decode the header '[PII of type 'Microsoft.IdentityModel.Logging.SecurityArtifact' is hidden. For more details, see https://aka.ms/IdentityModel/PII.]' as Base64Url encoded string.
```

## Root Cause

The JWT token received from the UserService and passed to the API Gateway had extra whitespace characters (trailing spaces, newlines, etc.) that made it invalid for Base64Url decoding. This commonly happens when:

1. **JSON serialization adds whitespace** - Some JSON parsers may add formatting
2. **PowerShell string handling** - Variables in PowerShell may capture extra characters
3. **Network transmission artifacts** - HTTP response bodies may include invisible characters

## Solution

### 1. Enhanced Error Logging (API Gateway)

Added more detailed logging to the JWT authentication events to help diagnose token issues:

**File: `src/ApiGateway/Program.cs`**

```csharp
options.Events = new JwtBearerEvents
{
    OnAuthenticationFailed = context =>
    {
        // Get the actual token from header for debugging
        var authHeader = context.Request.Headers["Authorization"].ToString();
        var tokenPreview = authHeader.Length > 20 ? authHeader.Substring(0, 20) + "..." : authHeader;
        
        Log.Warning("JWT Authentication failed: {Error}. Header preview: '{TokenPreview}'", 
            context.Exception.Message, 
            tokenPreview);
        
        // Log more details for token format issues
        if (context.Exception.Message.Contains("IDX14102") || context.Exception.Message.Contains("decode"))
        {
            Log.Error("Token decode error. Full exception: {Exception}", context.Exception.ToString());
        }
        
        return Task.CompletedTask;
    },
    OnTokenValidated = context =>
    {
        var userId = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        Log.Information("JWT Token validated for user: {UserId}", userId);
        return Task.CompletedTask;
    },
    OnMessageReceived = context =>
    {
        // Log when token is received
        var authHeader = context.Request.Headers["Authorization"].ToString();
        if (!string.IsNullOrEmpty(authHeader))
        {
            var parts = authHeader.Split(' ');
            Log.Debug("Authorization header received. Scheme: {Scheme}, Token length: {Length}", 
                parts.Length > 0 ? parts[0] : "none",
                parts.Length > 1 ? parts[1].Length : 0);
        }
        return Task.CompletedTask;
    }
};
```

### 2. Token Trimming in Test Scripts

Added `.Trim()` to all token assignments in PowerShell test scripts to remove any whitespace:

**Modified Files:**
- `scripts/testing/test-auth.ps1`
- `scripts/testing/test-e2e-auth.ps1` (2 locations)
- `scripts/testing/test-e2e-load.ps1` (2 locations)
- `scripts/testing/test-gateway-full.ps1`

**Example change:**
```powershell
# Before
$token = $loginResponse.data.token

# After
$token = $loginResponse.data.token.Trim()
```

### 3. Token Format Validation

Added validation in `test-auth.ps1` to check token format:

```powershell
$token = $null
if ($loginResult.success) {
    $loginResponse = $loginResult.content | ConvertFrom-Json
    $token = $loginResponse.data.token.Trim()
    Write-Host "  Token received: $($token.Substring(0, [Math]::Min(50, $token.Length)))..." -ForegroundColor Gray
    Write-Host "  Token length: $($token.Length) characters" -ForegroundColor Gray
    
    # Validate token format (should be three base64url parts separated by dots)
    $tokenParts = $token -split '\.'
    if ($tokenParts.Count -ne 3) {
        Write-Host "  [WARNING] Token format invalid - expected 3 parts, got $($tokenParts.Count)" -ForegroundColor Yellow
    } else {
        Write-Host "  Token format validated: header.payload.signature" -ForegroundColor Gray
    }
}
```

## JWT Token Format

A valid JWT token has three parts separated by dots:
```
<header>.<payload>.<signature>
```

Example:
```
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c
```

Each part is a Base64Url-encoded JSON object:
- **Header**: Algorithm and token type
- **Payload**: Claims (user data, expiration, etc.)
- **Signature**: Cryptographic signature to verify authenticity

## Testing the Fix

### 1. Run the authentication test script:
```powershell
powershell -ExecutionPolicy Bypass -File scripts\testing\test-auth.ps1
```

### 2. Check Seq logs for detailed authentication events:
```
http://localhost:5341
```

### 3. Verify token format:
- Token should have exactly 3 parts when split by '.'
- No whitespace at beginning or end
- Each part should be valid Base64Url

### 4. Run E2E tests:
```powershell
# Simple E2E test
powershell -ExecutionPolicy Bypass -File scripts\testing\test-e2e-auth.ps1

# Load test with authentication
powershell -ExecutionPolicy Bypass -File scripts\testing\test-e2e-load.ps1 -NumberOfFlows 50 -WithAuth
```

## Additional Debugging Tips

### 1. Enable detailed logging in appsettings.Development.json:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information",
      "Microsoft.IdentityModel": "Debug",
      "Yarp": "Debug"
    }
  }
}
```

### 2. Check token in browser/tools:
- Copy the token from logs
- Visit https://jwt.io
- Paste the token to decode and validate

### 3. Verify JWT configuration matches:
All services must use the **same JWT settings**:
- SecretKey
- Issuer
- Audience

Check these in:
- `docker-compose.yml` environment variables
- `appsettings.json` for each service

### 4. Common JWT errors and solutions:

| Error | Cause | Solution |
|-------|-------|----------|
| IDX14102 | Invalid Base64Url encoding | Trim whitespace from token |
| IDX10503 | Signature validation failed | Check SecretKey matches |
| IDX10214 | Audience validation failed | Check Audience configuration |
| IDX10205 | Issuer validation failed | Check Issuer configuration |
| IDX10223 | Token expired | Generate new token or extend expiration |

## Prevention

To prevent this issue in the future:

1. **Always trim tokens** when extracting from JSON responses
2. **Validate token format** before using (3 parts separated by dots)
3. **Use proper Content-Type** headers (`application/json`)
4. **Log token metadata** (length, format) not the actual token value
5. **Enable detailed logging** during development
6. **Test with various clients** (PowerShell, curl, Postman, etc.)

## References

- [JWT.IO](https://jwt.io) - Decode and verify JWT tokens
- [Microsoft Identity Model Documentation](https://aka.ms/IdentityModel/PII)
- [RFC 7519 - JSON Web Token (JWT)](https://tools.ietf.org/html/rfc7519)
- [Base64Url Encoding](https://tools.ietf.org/html/rfc4648#section-5)

## Related Files

- `src/ApiGateway/Program.cs` - JWT authentication configuration
- `src/UserService/Services/AuthService.cs` - Token generation
- `scripts/testing/test-auth.ps1` - Authentication tests
- `scripts/testing/test-e2e-auth.ps1` - E2E authenticated flows
- `scripts/testing/test-e2e-load.ps1` - Load tests with auth
- `docs/phase4-gateway-security/JWT_AUTHENTICATION_IMPLEMENTATION.md` - JWT setup guide
