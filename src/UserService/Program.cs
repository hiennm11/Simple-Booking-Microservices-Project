using Microsoft.EntityFrameworkCore;
using Serilog;
using UserService.Controllers;
using UserService.Data;
using UserService.Services;
using Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.WithProperty("Service", "UserService")
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
    options.UseNpgsql(connectionString));

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
