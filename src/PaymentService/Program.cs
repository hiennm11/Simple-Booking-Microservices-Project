using Serilog;

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

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

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

// (MongoConnectionHealthCheck moved to a separate file under HealthChecks/ to avoid top-level type declaration issues)

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

// Map health check endpoint
app.MapHealthChecks("/health");

app.Run();

// Clean up Serilog
Log.CloseAndFlush();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
