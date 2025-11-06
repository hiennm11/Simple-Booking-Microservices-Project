using Serilog;
using PaymentService.Data;
using PaymentService.EventBus;
using PaymentService.Services;
using PaymentService.Consumers;
using Shared.EventBus;
using Shared.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.WithProperty("Service", "PaymentService")
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Seq(
        serverUrl: builder.Configuration["Seq:ServerUrl"] ?? "http://seq:5341",
        apiKey: builder.Configuration["Seq:ApiKey"])
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
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
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Log.Warning("JWT Authentication failed in PaymentService: {Error}", context.Exception.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var userId = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                Log.Information("JWT Token validated in PaymentService for user: {UserId}", userId);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Configure MongoDB
builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDB"));
builder.Services.AddSingleton<MongoDbContext>();

// Configure RabbitMQ settings
builder.Services.Configure<RabbitMQSettings>(builder.Configuration.GetSection("RabbitMQ"));

// Register EventBus
builder.Services.AddSingleton<IEventBus, RabbitMQEventBus>();

// Register Resilience Pipeline Service
builder.Services.AddSingleton<IResiliencePipelineService, ResiliencePipelineService>();

// Register Services
builder.Services.AddScoped<IPaymentService, PaymentServiceImpl>();

// Register Background Services (Consumers)
// Note: BookingCreatedConsumer is available but not enabled by default
// Enable it to automatically process payments when bookings are created

// Add health checks with MongoDB database check (use MongoDB:ConnectionString)
var mongoConn = builder.Configuration["MongoDB:ConnectionString"];
var hcBuilder = builder.Services.AddHealthChecks();
if (!string.IsNullOrWhiteSpace(mongoConn))
{
    // Use a safe custom health check to avoid throwing on invalid connection strings
    hcBuilder.Add(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckRegistration(
        "paymentdb",
        new PaymentService.HealthChecks.MongoConnectionHealthCheck(mongoConn),
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
        new[] { "db", "mongodb", "paymentdb" }
    ));
}
else
{
    // If no connection string is configured, register a degraded check so the service still reports status
    hcBuilder.AddCheck("paymentdb-config", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded("MongoDB connection string not configured"));
}

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Add global exception handling
app.UseGlobalExceptionHandler();

app.UseHttpsRedirection();

// Enable Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Map health check endpoint
app.MapHealthChecks("/health");

app.Run();

// Clean up Serilog
Log.CloseAndFlush();
