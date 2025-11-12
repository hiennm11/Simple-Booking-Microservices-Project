using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Shared.Common;

namespace PaymentService.Models;

/// <summary>
/// Represents a message that failed processing after maximum retry attempts
/// Stored for manual investigation and potential recovery
/// </summary>
public class DeadLetterMessage
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    /// <summary>
    /// The name of the queue where the message originally came from
    /// </summary>
    public string SourceQueue { get; set; } = null!;

    /// <summary>
    /// The type of event (e.g., PaymentFailed, PaymentSucceeded)
    /// </summary>
    public string EventType { get; set; } = null!;

    /// <summary>
    /// The raw message payload as JSON
    /// </summary>
    public string Payload { get; set; } = null!;

    /// <summary>
    /// The error message from the last processing attempt
    /// </summary>
    public string ErrorMessage { get; set; } = null!;

    /// <summary>
    /// Stack trace of the exception if available
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// Number of times processing was attempted
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Timestamp when the message was first received
    /// </summary>
    public DateTime FirstAttemptAt { get; set; }

    /// <summary>
    /// Timestamp when the message finally failed
    /// </summary>
    public DateTime FailedAt { get; set; }

    /// <summary>
    /// Whether this message has been manually resolved
    /// </summary>
    public bool Resolved { get; set; } = false;

    /// <summary>
    /// Timestamp when the message was resolved
    /// </summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>
    /// Notes about the resolution or investigation
    /// </summary>
    public string? ResolutionNotes { get; set; }

    /// <summary>
    /// User or system that resolved the message
    /// </summary>
    public string? ResolvedBy { get; set; }
}
