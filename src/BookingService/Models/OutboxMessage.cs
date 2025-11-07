namespace BookingService.Models;

/// <summary>
/// Represents an outbox message for reliable event publishing.
/// Events are first saved to the database in the same transaction as business data,
/// then published asynchronously by a background worker.
/// </summary>
public class OutboxMessage
{
    /// <summary>
    /// Unique identifier for this outbox message
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// Type of event (e.g., "BookingCreated", "BookingConfirmed")
    /// </summary>
    public string EventType { get; set; } = null!;
    
    /// <summary>
    /// Serialized JSON payload of the event
    /// </summary>
    public string Payload { get; set; } = null!;
    
    /// <summary>
    /// When this message was created and saved to outbox
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Whether this message has been successfully published to RabbitMQ
    /// </summary>
    public bool Published { get; set; }
    
    /// <summary>
    /// When this message was successfully published (null if not yet published)
    /// </summary>
    public DateTime? PublishedAt { get; set; }
    
    /// <summary>
    /// Number of times we've tried to publish this message
    /// </summary>
    public int RetryCount { get; set; }
    
    /// <summary>
    /// Last error message if publishing failed
    /// </summary>
    public string? LastError { get; set; }
    
    /// <summary>
    /// When the last publish attempt was made
    /// </summary>
    public DateTime? LastAttemptAt { get; set; }
}
