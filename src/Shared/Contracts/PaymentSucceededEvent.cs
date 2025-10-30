namespace Shared.Contracts;

/// <summary>
/// Event published when a payment is successfully processed
/// </summary>
public class PaymentSucceededEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public string EventName { get; set; } = "PaymentSucceeded";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public PaymentSucceededData Data { get; set; } = null!;
}

public class PaymentSucceededData
{
    public Guid PaymentId { get; set; }
    public Guid BookingId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "SUCCESS";
}
