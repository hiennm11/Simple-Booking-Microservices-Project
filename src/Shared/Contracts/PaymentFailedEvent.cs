namespace Shared.Contracts;

/// <summary>
/// Event published when a payment fails
/// </summary>
public class PaymentFailedEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public string EventName { get; set; } = "PaymentFailed";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public PaymentFailedData Data { get; set; } = null!;
}

public class PaymentFailedData
{
    public Guid PaymentId { get; set; }
    public Guid BookingId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = null!;
    public string Status { get; set; } = "FAILED";
}
