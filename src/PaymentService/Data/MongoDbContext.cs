using Microsoft.Extensions.Options;
using MongoDB.Driver;
using PaymentService.Models;

namespace PaymentService.Data;

/// <summary>
/// MongoDB context for PaymentService
/// </summary>
public class MongoDbContext
{
    private readonly IMongoDatabase _database;
    private readonly MongoDbSettings _settings;

    public MongoDbContext(IOptions<MongoDbSettings> settings)
    {
        _settings = settings.Value;
        
        var client = new MongoClient(_settings.ConnectionString);
        _database = client.GetDatabase(_settings.DatabaseName);
    }

    public IMongoCollection<Payment> Payments => 
        _database.GetCollection<Payment>(_settings.Collections.GetValueOrDefault("Payments", "payments"));
}

/// <summary>
/// MongoDB configuration settings
/// </summary>
public class MongoDbSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public Dictionary<string, string> Collections { get; set; } = new();
}
