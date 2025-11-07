using System.Text.Json;
using MongoDB.Driver;
using PaymentService.Data;
using PaymentService.Models;

namespace PaymentService.Services;

/// <summary>
/// Service interface for managing outbox messages
/// </summary>
public interface IOutboxService
{
    /// <summary>
    /// Adds an event to the outbox collection.
    /// This should be called within a MongoDB session/transaction for atomicity.
    /// </summary>
    Task AddToOutboxAsync<T>(T @event, string eventType, CancellationToken cancellationToken = default) where T : class;
    
    /// <summary>
    /// Retrieves unpublished messages from the outbox, ordered by creation time.
    /// Used by the background publisher to process pending events.
    /// </summary>
    Task<List<OutboxMessage>> GetUnpublishedMessagesAsync(int batchSize = 100, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Marks an outbox message as successfully published.
    /// </summary>
    Task MarkAsPublishedAsync(Guid messageId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates an outbox message when a publish attempt fails.
    /// Increments retry count and stores the error message.
    /// </summary>
    Task MarkAsFailedAsync(Guid messageId, string errorMessage, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of the Outbox Service for reliable event publishing in MongoDB.
/// This service implements the Transactional Outbox Pattern for MongoDB.
/// </summary>
public class OutboxService : IOutboxService
{
    private readonly MongoDbContext _dbContext;
    private readonly ILogger<OutboxService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public OutboxService(
        MongoDbContext dbContext,
        ILogger<OutboxService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task AddToOutboxAsync<T>(T @event, string eventType, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var payload = JsonSerializer.Serialize(@event, _jsonOptions);
            
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EventType = eventType,
                Payload = payload,
                CreatedAt = DateTime.UtcNow,
                Published = false,
                RetryCount = 0
            };

            await _dbContext.OutboxMessages.InsertOneAsync(outboxMessage, cancellationToken: cancellationToken);
            
            _logger.LogInformation(
                "Event added to outbox: {EventType}, MessageId: {MessageId}", 
                eventType, 
                outboxMessage.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add event to outbox: {EventType}", eventType);
            throw;
        }
    }

    public async Task<List<OutboxMessage>> GetUnpublishedMessagesAsync(
        int batchSize = 100, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var filter = Builders<OutboxMessage>.Filter.Eq(m => m.Published, false);
            var sort = Builders<OutboxMessage>.Sort.Ascending(m => m.CreatedAt);

            var messages = await _dbContext.OutboxMessages
                .Find(filter)
                .Sort(sort)
                .Limit(batchSize)
                .ToListAsync(cancellationToken);

            _logger.LogDebug("Retrieved {Count} unpublished messages from outbox", messages.Count);
            
            return messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve unpublished messages from outbox");
            throw;
        }
    }

    public async Task MarkAsPublishedAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        try
        {
            var filter = Builders<OutboxMessage>.Filter.Eq(m => m.Id, messageId);
            var update = Builders<OutboxMessage>.Update
                .Set(m => m.Published, true)
                .Set(m => m.PublishedAt, DateTime.UtcNow)
                .Set(m => m.LastAttemptAt, DateTime.UtcNow);

            var result = await _dbContext.OutboxMessages.UpdateOneAsync(
                filter, 
                update, 
                cancellationToken: cancellationToken);

            if (result.MatchedCount == 0)
            {
                _logger.LogWarning("Outbox message not found: {MessageId}", messageId);
                return;
            }

            _logger.LogInformation(
                "Outbox message marked as published: {MessageId}", 
                messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark message as published: {MessageId}", messageId);
            throw;
        }
    }

    public async Task MarkAsFailedAsync(
        Guid messageId, 
        string errorMessage, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var filter = Builders<OutboxMessage>.Filter.Eq(m => m.Id, messageId);
            
            // Truncate error message if too long
            var truncatedError = errorMessage?.Length > 2000 
                ? errorMessage.Substring(0, 2000) 
                : errorMessage;
            
            var update = Builders<OutboxMessage>.Update
                .Inc(m => m.RetryCount, 1)
                .Set(m => m.LastError, truncatedError)
                .Set(m => m.LastAttemptAt, DateTime.UtcNow);

            var result = await _dbContext.OutboxMessages.UpdateOneAsync(
                filter, 
                update, 
                cancellationToken: cancellationToken);

            if (result.MatchedCount == 0)
            {
                _logger.LogWarning("Outbox message not found: {MessageId}", messageId);
                return;
            }

            // Get the updated retry count for logging
            var message = await _dbContext.OutboxMessages
                .Find(filter)
                .FirstOrDefaultAsync(cancellationToken);

            _logger.LogWarning(
                "Outbox message marked as failed (attempt {RetryCount}): {MessageId}, Error: {Error}", 
                message?.RetryCount ?? 0,
                messageId, 
                errorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark message as failed: {MessageId}", messageId);
            throw;
        }
    }
}
