using Microsoft.AspNetCore.Mvc;
using Shared.Common;
using UserService.DTOs;
using UserService.Services;

namespace UserService.Controllers;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users");

        group.MapPost("/register", RegisterUser)
            .WithName("RegisterUser")
            .Produces<ApiResponse<UserResponse>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<UserResponse>>(StatusCodes.Status400BadRequest);

        group.MapPost("/login", LoginUser)
            .WithName("LoginUser")
            .Produces<ApiResponse<LoginResponse>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<LoginResponse>>(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id:guid}", GetUserById)
            .WithName("GetUserById")
            .Produces<ApiResponse<UserResponse>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<UserResponse>>(StatusCodes.Status404NotFound);

        group.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "UserService" }))
            .WithName("UserServiceHealth")
            .ExcludeFromDescription();
    }

    private static async Task<IResult> RegisterUser(
        [FromBody] RegisterRequest request,
        [FromServices] IAuthService authService)
    {
        if (!IsValidRequest(request))
        {
            return Results.BadRequest(ApiResponse<UserResponse>.ErrorResponse("Invalid request data"));
        }

        var user = await authService.RegisterAsync(request);
        
        if (user == null)
        {
            return Results.BadRequest(
                ApiResponse<UserResponse>.ErrorResponse("Username or email already exists"));
        }

        return Results.Ok(ApiResponse<UserResponse>.SuccessResponse(user, "User registered successfully"));
    }

    private static async Task<IResult> LoginUser(
        [FromBody] LoginRequest request,
        [FromServices] IAuthService authService)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(ApiResponse<LoginResponse>.ErrorResponse("Username and password are required"));
        }

        var loginResponse = await authService.LoginAsync(request);
        
        if (loginResponse == null)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(ApiResponse<LoginResponse>.SuccessResponse(loginResponse, "Login successful"));
    }

    private static async Task<IResult> GetUserById(
        [FromRoute] Guid id,
        [FromServices] IAuthService authService)
    {
        var user = await authService.GetUserByIdAsync(id);
        
        if (user == null)
        {
            return Results.NotFound(ApiResponse<UserResponse>.ErrorResponse("User not found"));
        }

        return Results.Ok(ApiResponse<UserResponse>.SuccessResponse(user));
    }

    private static bool IsValidRequest(RegisterRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.Username) &&
               !string.IsNullOrWhiteSpace(request.Email) &&
               !string.IsNullOrWhiteSpace(request.Password) &&
               !string.IsNullOrWhiteSpace(request.FirstName) &&
               !string.IsNullOrWhiteSpace(request.LastName);
    }
}
