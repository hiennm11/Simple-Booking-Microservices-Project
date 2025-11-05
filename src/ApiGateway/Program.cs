using System.Text;
using ApiGateway.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.WithProperty("Service", "ApiGateway")
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Seq(
        serverUrl: builder.Configuration["Seq:ServerUrl"] ?? "http://seq:5341",
        apiKey: builder.Configuration["Seq:ApiKey"])
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddOpenApi();

// Add JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? "YourDefaultSecretKeyForDevelopment123!";
var issuer = jwtSettings["Issuer"] ?? "UserService";
var audience = jwtSettings["Audience"] ?? "BookingSystem";

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
            ClockSkew = TimeSpan.Zero // Remove default 5 minute tolerance
        };
        
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
    });

builder.Services.AddAuthorization(options =>
{
    // Define authorization policy for authenticated users
    options.AddPolicy("authenticated", policy =>
    {
        policy.RequireAuthenticatedUser();
    });
});

// Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add YARP reverse proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Add basic health checks (YARP handles downstream health checks)
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Use global exception handler
app.UseGlobalExceptionHandler();

// Enable CORS
app.UseCors();

// Enable Authentication & Authorization (MUST be before MapReverseProxy)
app.UseAuthentication();
app.UseAuthorization();

// Forward user claims to downstream services (after authentication)
app.UseUserClaimsForwarding();

// Use request/response logging
app.UseRequestResponseLogging();

// Map health check endpoint
app.MapHealthChecks("/health");

// Map gateway info endpoint
app.MapGet("/", () => Results.Ok(new 
{ 
    service = "API Gateway",
    version = "1.0.0",
    status = "running",
    timestamp = DateTime.UtcNow,
    endpoints = new
    {
        users = "/api/users",
        bookings = "/api/bookings",
        payments = "/api/payments",
        health = "/health"
    }
}))
.WithName("GatewayInfo")
.WithDescription("Get API Gateway information and available routes")
.ExcludeFromDescription();

// Map YARP reverse proxy
app.MapReverseProxy();

Log.Information("API Gateway started successfully");

app.Run();

// Clean up Serilog
Log.CloseAndFlush();



