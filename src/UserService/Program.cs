using Microsoft.EntityFrameworkCore;
using Serilog;
using UserService.Controllers;
using UserService.Data;
using UserService.Services;
using Shared.Extensions;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel for high concurrency
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxConcurrentConnections = 500;
    serverOptions.Limits.MaxConcurrentUpgradedConnections = 500;
    serverOptions.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
    serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
    serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
});

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.WithProperty("Service", "UserService")
    .Enrich.WithClientIp()
    .Enrich.WithCorrelationId()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Seq(
        serverUrl: builder.Configuration["Seq:ServerUrl"] ?? "http://seq:5341",
        apiKey: builder.Configuration["Seq:ApiKey"])
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddOpenApi();

// Add DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<UserDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.MaxBatchSize(100);
        npgsqlOptions.CommandTimeout(30);
    }).EnableSensitiveDataLogging(builder.Environment.IsDevelopment()));

// Configure connection string pool size via environment or appsettings
// Example: "Host=localhost;Port=5432;Database=userdb;Username=user;Password=pass;Maximum Pool Size=200;Minimum Pool Size=10"

// Add services
builder.Services.AddScoped<IAuthService, AuthService>();

// Add health checks with PostgreSQL database check
builder.Services.AddHealthChecks()
    .AddNpgSql(
        connectionString!,
        name: "userdb",
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
        tags: new[] { "db", "postgresql", "userdb" });

var app = builder.Build();

// Apply migrations and seed data automatically
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<UserDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        // Apply migrations
        dbContext.Database.Migrate();
        Log.Information("Database migrations applied successfully");
        
        // Seed initial data
        await UserDbSeeder.SeedAsync(dbContext, logger);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "An error occurred while applying migrations or seeding data");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Add global exception handling
app.UseGlobalExceptionHandler();

app.UseHttpsRedirection();

// Map user endpoints
app.MapUserEndpoints();

// Map health check endpoint
app.MapHealthChecks("/health");

app.Run();

// Clean up Serilog
Log.CloseAndFlush();
