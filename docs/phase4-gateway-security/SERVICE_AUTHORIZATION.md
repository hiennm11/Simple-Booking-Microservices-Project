# Service-Level Authorization - Quick Reference

## Overview

This guide shows how to read and use authenticated user information in downstream services (BookingService, PaymentService).

## Reading User Information from Headers

The API Gateway forwards authenticated user information as HTTP headers:

```csharp
// Extract user information from headers
var userId = httpContext.Request.Headers["X-User-Id"].FirstOrDefault();
var username = httpContext.Request.Headers["X-User-Name"].FirstOrDefault();
var email = httpContext.Request.Headers["X-User-Email"].FirstOrDefault();

if (string.IsNullOrEmpty(userId))
{
    return Results.Unauthorized(); // No user info = not authenticated
}

// Convert to Guid if needed
if (Guid.TryParse(userId, out var userGuid))
{
    // Use userGuid for database queries
}
```

## Example: BookingService Implementation

### Creating a Booking (Authenticated)

```csharp
// BookingEndpoints.cs
public static void MapBookingEndpoints(this IEndpointRouteBuilder app)
{
    var group = app.MapGroup("/api/bookings")
        .WithTags("Bookings");

    // Create booking - requires authentication (enforced by API Gateway)
    group.MapPost("/", CreateBooking)
        .WithName("CreateBooking")
        .Produces<ApiResponse<BookingResponse>>(StatusCodes.Status201Created)
        .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
        .Produces<ApiResponse<object>>(StatusCodes.Status401Unauthorized);

    // Get user's bookings
    group.MapGet("/my-bookings", GetMyBookings)
        .WithName("GetMyBookings")
        .Produces<ApiResponse<List<BookingResponse>>>(StatusCodes.Status200OK);

    // Get booking by ID (with ownership check)
    group.MapGet("/{id:guid}", GetBookingById)
        .WithName("GetBookingById")
        .Produces<ApiResponse<BookingResponse>>(StatusCodes.Status200OK)
        .Produces<ApiResponse<object>>(StatusCodes.Status403Forbidden)
        .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);
}

private static async Task<IResult> CreateBooking(
    HttpContext httpContext,
    [FromBody] CreateBookingRequest request,
    [FromServices] IBookingService bookingService)
{
    // Get authenticated user ID from header (set by API Gateway)
    var userIdStr = httpContext.Request.Headers["X-User-Id"].FirstOrDefault();
    
    if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
    {
        return Results.Unauthorized(); // Should not happen if gateway is configured correctly
    }

    // Create booking for the authenticated user
    var booking = await bookingService.CreateBookingAsync(userId, request);
    
    return Results.Created(
        $"/api/bookings/{booking.Id}", 
        ApiResponse<BookingResponse>.SuccessResponse(booking, "Booking created successfully")
    );
}

private static async Task<IResult> GetMyBookings(
    HttpContext httpContext,
    [FromServices] IBookingService bookingService)
{
    var userIdStr = httpContext.Request.Headers["X-User-Id"].FirstOrDefault();
    
    if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
    {
        return Results.Unauthorized();
    }

    // Get all bookings for the authenticated user
    var bookings = await bookingService.GetUserBookingsAsync(userId);
    
    return Results.Ok(
        ApiResponse<List<BookingResponse>>.SuccessResponse(bookings)
    );
}

private static async Task<IResult> GetBookingById(
    HttpContext httpContext,
    [FromRoute] Guid id,
    [FromServices] IBookingService bookingService)
{
    var userIdStr = httpContext.Request.Headers["X-User-Id"].FirstOrDefault();
    
    if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
    {
        return Results.Unauthorized();
    }

    var booking = await bookingService.GetBookingByIdAsync(id);
    
    if (booking == null)
    {
        return Results.NotFound(
            ApiResponse<object>.ErrorResponse("Booking not found")
        );
    }

    // Check if the booking belongs to the authenticated user
    if (booking.UserId != userId)
    {
        return Results.Forbid(); // 403 Forbidden - user doesn't own this resource
    }

    return Results.Ok(
        ApiResponse<BookingResponse>.SuccessResponse(booking)
    );
}
```

## Example: PaymentService Implementation

### Processing a Payment (Authenticated)

```csharp
// PaymentEndpoints.cs
group.MapPost("/", ProcessPayment)
    .WithName("ProcessPayment")
    .Produces<ApiResponse<PaymentResponse>>(StatusCodes.Status200OK)
    .Produces<ApiResponse<object>>(StatusCodes.Status401Unauthorized);

private static async Task<IResult> ProcessPayment(
    HttpContext httpContext,
    [FromBody] PaymentRequest request,
    [FromServices] IPaymentService paymentService)
{
    // Get authenticated user info
    var userId = httpContext.Request.Headers["X-User-Id"].FirstOrDefault();
    var username = httpContext.Request.Headers["X-User-Name"].FirstOrDefault();
    var email = httpContext.Request.Headers["X-User-Email"].FirstOrDefault();
    
    if (string.IsNullOrEmpty(userId))
    {
        return Results.Unauthorized();
    }

    // Use user information in payment processing
    var payment = await paymentService.ProcessPaymentAsync(new PaymentDto
    {
        UserId = Guid.Parse(userId),
        Username = username,
        Email = email,
        Amount = request.Amount,
        BookingId = request.BookingId
    });

    return Results.Ok(
        ApiResponse<PaymentResponse>.SuccessResponse(payment)
    );
}
```

## Helper Extension Method

Create a reusable extension method for extracting user information:

```csharp
// Extensions/HttpContextExtensions.cs
namespace BookingService.Extensions;

public static class HttpContextExtensions
{
    public static (bool IsAuthenticated, Guid? UserId, string? Username, string? Email) GetAuthenticatedUser(
        this HttpContext context)
    {
        var userIdStr = context.Request.Headers["X-User-Id"].FirstOrDefault();
        var username = context.Request.Headers["X-User-Name"].FirstOrDefault();
        var email = context.Request.Headers["X-User-Email"].FirstOrDefault();

        if (string.IsNullOrEmpty(userIdStr))
        {
            return (false, null, null, null);
        }

        if (!Guid.TryParse(userIdStr, out var userId))
        {
            return (false, null, null, null);
        }

        return (true, userId, username, email);
    }
}
```

**Usage:**

```csharp
private static async Task<IResult> CreateBooking(
    HttpContext httpContext,
    [FromBody] CreateBookingRequest request,
    [FromServices] IBookingService bookingService)
{
    var (isAuthenticated, userId, username, email) = httpContext.GetAuthenticatedUser();
    
    if (!isAuthenticated || userId == null)
    {
        return Results.Unauthorized();
    }

    // Use userId, username, email...
    var booking = await bookingService.CreateBookingAsync(userId.Value, request);
    
    return Results.Created($"/api/bookings/{booking.Id}", booking);
}
```

## Authorization Patterns

### Pattern 1: User-Owned Resources

Check if the resource belongs to the authenticated user:

```csharp
// Get booking
var booking = await _context.Bookings.FindAsync(bookingId);

// Check ownership
if (booking.UserId != authenticatedUserId)
{
    return Results.Forbid(); // 403 Forbidden
}
```

### Pattern 2: Role-Based Access

If you implement roles, check them from headers:

```csharp
var userRole = httpContext.Request.Headers["X-User-Role"].FirstOrDefault();

if (userRole != "Admin" && userRole != "Manager")
{
    return Results.Forbid(); // 403 Forbidden
}
```

### Pattern 3: Scope-Based Access

Check specific permissions:

```csharp
var permissions = httpContext.Request.Headers["X-User-Permissions"]
    .FirstOrDefault()?
    .Split(',');

if (!permissions?.Contains("bookings:write") == true)
{
    return Results.Forbid();
}
```

## Important Notes

### ✅ DO:

1. **Always validate user headers exist**
   ```csharp
   if (string.IsNullOrEmpty(userId))
       return Results.Unauthorized();
   ```

2. **Check resource ownership**
   ```csharp
   if (resource.UserId != authenticatedUserId)
       return Results.Forbid();
   ```

3. **Use Guid.TryParse for safety**
   ```csharp
   if (!Guid.TryParse(userIdStr, out var userId))
       return Results.BadRequest();
   ```

4. **Log authorization failures**
   ```csharp
   _logger.LogWarning("User {UserId} attempted to access booking {BookingId} without permission", 
       userId, bookingId);
   ```

### ❌ DON'T:

1. **Don't trust user ID from request body**
   ```csharp
   // ❌ WRONG - user can fake this
   var userId = request.UserId;
   
   // ✅ CORRECT - get from headers (set by gateway)
   var userId = httpContext.Request.Headers["X-User-Id"];
   ```

2. **Don't skip validation in "trusted" environments**
   ```csharp
   // ❌ WRONG - always validate
   var userId = Guid.Parse(httpContext.Request.Headers["X-User-Id"].First());
   ```

3. **Don't log sensitive information**
   ```csharp
   // ❌ WRONG
   _logger.LogInformation("User password: {Password}", password);
   
   // ✅ CORRECT
   _logger.LogInformation("User {UserId} authenticated", userId);
   ```

## Testing Service Endpoints

### Test with Headers (Simulating Gateway)

```bash
curl -X POST http://localhost:5002/api/bookings \
  -H "Content-Type: application/json" \
  -H "X-User-Id: 123e4567-e89b-12d3-a456-426614174000" \
  -H "X-User-Name: john_doe" \
  -H "X-User-Email: john@example.com" \
  -d '{
    "serviceId": "123e4567-e89b-12d3-a456-426614174001",
    "date": "2025-11-15",
    "notes": "Test booking"
  }'
```

### Test through Gateway (Real Scenario)

```bash
# 1. Get token
TOKEN=$(curl -s -X POST http://localhost:5000/api/users/login \
  -H "Content-Type: application/json" \
  -d '{"username":"john_doe","password":"password"}' \
  | jq -r '.data.token')

# 2. Create booking with token
curl -X POST http://localhost:5000/api/bookings \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "serviceId": "123e4567-e89b-12d3-a456-426614174001",
    "date": "2025-11-15"
  }'
```

## Troubleshooting

### Headers Not Present

**Symptom:** `X-User-Id` header is null or empty

**Causes:**
1. Request not going through API Gateway
2. Gateway middleware not configured correctly
3. Token not validated by gateway

**Solution:**
- Always access services through the gateway: `http://localhost:5000/api/bookings`
- Not directly: `http://localhost:5002/api/bookings`

### User ID Format Issues

**Symptom:** Cannot parse user ID to Guid

**Solution:**
```csharp
if (!Guid.TryParse(userIdStr, out var userId))
{
    _logger.LogWarning("Invalid user ID format: {UserIdStr}", userIdStr);
    return Results.BadRequest(ApiResponse<object>.ErrorResponse("Invalid user ID format"));
}
```

## Next Steps

1. Implement the examples in BookingService
2. Implement the examples in PaymentService
3. Add comprehensive logging
4. Write unit tests for authorization logic
5. Add integration tests with mock authentication
