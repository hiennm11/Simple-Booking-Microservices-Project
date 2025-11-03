using Serilog;
using Microsoft.EntityFrameworkCore;
using BookingService.Data;
using BookingService.Services;
using BookingService.EventBus;
using BookingService.Consumers;
using Shared.EventBus;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.WithProperty("Service", "BookingService")
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

// Configure Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<BookingDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

// Configure RabbitMQ settings
builder.Services.Configure<RabbitMQSettings>(builder.Configuration.GetSection("RabbitMQ"));

// Register EventBus
builder.Services.AddSingleton<IEventBus, RabbitMQEventBus>();

// Register Services
builder.Services.AddScoped<IBookingService, BookingServiceImpl>();

// Register Background Services (Consumers)
builder.Services.AddHostedService<PaymentSucceededConsumer>();

// Add health checks with PostgreSQL database check
builder.Services.AddHealthChecks()
    .AddNpgSql(
        connectionString!,
        name: "bookingdb",
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
        tags: new[] { "db", "postgresql", "bookingdb" });

var app = builder.Build();

// Run database migrations automatically
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        Log.Information("Applying database migrations...");
        await dbContext.Database.MigrateAsync();
        Log.Information("Database migrations applied successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "An error occurred while migrating the database");
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// Map health check endpoint
app.MapHealthChecks("/health");

Log.Information("BookingService is starting...");

app.Run();

// Clean up Serilog
Log.CloseAndFlush();
