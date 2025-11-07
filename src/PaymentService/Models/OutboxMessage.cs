using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PaymentService.Models;

/// <summary>
/// Represents an outbox message for reliable event publishing in MongoDB.
/// Events are first saved to the database along with business data,
/// then published asynchronously by a background worker.
/// </summary>
public class OutboxMessage
{
    /// <summary>
    /// Unique identifier for this outbox message
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Type of event (e.g., "PaymentSucceeded", "PaymentFailed")
    /// </summary>
    [BsonElement("eventType")]
    public string EventType { get; set; } = null!;
    
    /// <summary>
    /// Serialized JSON payload of the event
    /// </summary>
    [BsonElement("payload")]
    public string Payload { get; set; } = null!;
    
    /// <summary>
    /// When this message was created and saved to outbox
    /// </summary>
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Whether this message has been successfully published to RabbitMQ
    /// </summary>
    [BsonElement("published")]
    public bool Published { get; set; } = false;
    
    /// <summary>
    /// When this message was successfully published (null if not yet published)
    /// </summary>
    [BsonElement("publishedAt")]
    public DateTime? PublishedAt { get; set; }
    
    /// <summary>
    /// Number of times we've tried to publish this message
    /// </summary>
    [BsonElement("retryCount")]
    public int RetryCount { get; set; } = 0;
    
    /// <summary>
    /// Last error message if publishing failed
    /// </summary>
    [BsonElement("lastError")]
    public string? LastError { get; set; }
    
    /// <summary>
    /// When the last publish attempt was made
    /// </summary>
    [BsonElement("lastAttemptAt")]
    public DateTime? LastAttemptAt { get; set; }
}
