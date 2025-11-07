using System.Text.Json;
using BookingService.Data;
using BookingService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.EventBus;

namespace BookingService.Services;

/// <summary>
/// Service interface for managing outbox messages
/// </summary>
public interface IOutboxService
{
    /// <summary>
    /// Adds an event to the outbox table within the current database transaction.
    /// This ensures atomicity - the event is saved only if the business transaction succeeds.
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
/// Implementation of the Outbox Service for reliable event publishing.
/// This service implements the Transactional Outbox Pattern to ensure events are never lost.
/// </summary>
public class OutboxService : IOutboxService
{
    private readonly BookingDbContext _dbContext;
    private readonly ILogger<OutboxService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public OutboxService(
        BookingDbContext dbContext,
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

            await _dbContext.OutboxMessages.AddAsync(outboxMessage, cancellationToken);
            
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
            var messages = await _dbContext.OutboxMessages
                .Where(m => !m.Published)
                .OrderBy(m => m.CreatedAt)
                .Take(batchSize)
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
            var message = await _dbContext.OutboxMessages
                .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

            if (message == null)
            {
                _logger.LogWarning("Outbox message not found: {MessageId}", messageId);
                return;
            }

            message.Published = true;
            message.PublishedAt = DateTime.UtcNow;
            message.LastAttemptAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Outbox message marked as published: {MessageId}, EventType: {EventType}", 
                messageId, 
                message.EventType);
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
            var message = await _dbContext.OutboxMessages
                .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

            if (message == null)
            {
                _logger.LogWarning("Outbox message not found: {MessageId}", messageId);
                return;
            }

            message.RetryCount++;
            message.LastError = errorMessage?.Length > 2000 
                ? errorMessage.Substring(0, 2000) 
                : errorMessage;
            message.LastAttemptAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Outbox message marked as failed (attempt {RetryCount}): {MessageId}, Error: {Error}", 
                message.RetryCount,
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
