using Serilog;
using PaymentService.Data;
using PaymentService.EventBus;
using PaymentService.Services;
using PaymentService.Consumers;
using Shared.EventBus;

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
// Uncomment the line below to enable automatic payment processing when bookings are created
// builder.Services.AddHostedService<BookingCreatedConsumer>();

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

app.UseHttpsRedirection();

app.MapControllers();

// Map health check endpoint
app.MapHealthChecks("/health");

app.Run();

// Clean up Serilog
Log.CloseAndFlush();
