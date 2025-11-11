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
        
        // Create indexes for optimal query performance
        CreateIndexes();
    }

    public virtual IMongoCollection<Payment> Payments => 
        _database.GetCollection<Payment>(_settings.Collections.GetValueOrDefault("Payments", "payments"));
    
    public virtual IMongoCollection<OutboxMessage> OutboxMessages =>
        _database.GetCollection<OutboxMessage>(_settings.Collections.GetValueOrDefault("OutboxMessages", "outbox_messages"));
    
    public virtual IMongoCollection<DeadLetterMessage> DeadLetterMessages =>
        _database.GetCollection<DeadLetterMessage>(_settings.Collections.GetValueOrDefault("DeadLetterMessages", "dead_letter_messages"));
    
    /// <summary>
    /// Creates necessary indexes for collections
    /// </summary>
    private void CreateIndexes()
    {
        // Create index on OutboxMessages for efficient querying of unpublished messages
        var outboxIndexKeys = Builders<OutboxMessage>.IndexKeys
            .Ascending(m => m.Published)
            .Ascending(m => m.CreatedAt);
        
        var outboxIndexOptions = new CreateIndexOptions
        {
            Name = "idx_published_created"
        };
        
        var outboxIndexModel = new CreateIndexModel<OutboxMessage>(outboxIndexKeys, outboxIndexOptions);
        OutboxMessages.Indexes.CreateOne(outboxIndexModel);
        
        // Create index on EventType for filtering
        var eventTypeIndexKeys = Builders<OutboxMessage>.IndexKeys.Ascending(m => m.EventType);
        var eventTypeIndexModel = new CreateIndexModel<OutboxMessage>(eventTypeIndexKeys);
        OutboxMessages.Indexes.CreateOne(eventTypeIndexModel);
        
        // Create indexes for DeadLetterMessages
        var dlqResolvedIndexKeys = Builders<DeadLetterMessage>.IndexKeys
            .Ascending(m => m.Resolved)
            .Descending(m => m.FailedAt);
        var dlqResolvedIndexModel = new CreateIndexModel<DeadLetterMessage>(dlqResolvedIndexKeys, 
            new CreateIndexOptions { Name = "idx_resolved_failed" });
        DeadLetterMessages.Indexes.CreateOne(dlqResolvedIndexModel);
        
        var dlqEventTypeIndexKeys = Builders<DeadLetterMessage>.IndexKeys.Ascending(m => m.EventType);
        var dlqEventTypeIndexModel = new CreateIndexModel<DeadLetterMessage>(dlqEventTypeIndexKeys);
        DeadLetterMessages.Indexes.CreateOne(dlqEventTypeIndexModel);
        
        var dlqSourceQueueIndexKeys = Builders<DeadLetterMessage>.IndexKeys.Ascending(m => m.SourceQueue);
        var dlqSourceQueueIndexModel = new CreateIndexModel<DeadLetterMessage>(dlqSourceQueueIndexKeys);
        DeadLetterMessages.Indexes.CreateOne(dlqSourceQueueIndexModel);
    }
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
