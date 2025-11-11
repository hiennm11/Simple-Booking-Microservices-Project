using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PaymentService.Models;

/// <summary>
/// Payment document for MongoDB
/// </summary>
public class Payment
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [BsonElement("bookingId")]
    [BsonRepresentation(BsonType.String)]
    public Guid BookingId { get; set; }

    [BsonElement("amount")]
    public decimal Amount { get; set; }

    [BsonElement("status")]
    public string Status { get; set; } = "PENDING"; // PENDING, SUCCESS, FAILED, PERMANENTLY_FAILED

    [BsonElement("paymentMethod")]
    public string PaymentMethod { get; set; } = "CREDIT_CARD";

    [BsonElement("transactionId")]
    public string? TransactionId { get; set; }

    [BsonElement("errorMessage")]
    public string? ErrorMessage { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("processedAt")]
    public DateTime? ProcessedAt { get; set; }

    [BsonElement("retryCount")]
    public int RetryCount { get; set; } = 0;

    [BsonElement("lastRetryAt")]
    public DateTime? LastRetryAt { get; set; }
}
