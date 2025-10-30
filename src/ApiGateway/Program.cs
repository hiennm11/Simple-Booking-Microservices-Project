using ApiGateway.Middleware;
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



