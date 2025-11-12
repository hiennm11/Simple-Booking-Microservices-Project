namespace Shared.Contracts;

/// <summary>
/// Event published when a booking is cancelled (e.g., due to inventory failure)
/// </summary>
public class BookingCancelledEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public Guid CorrelationId { get; set; }
    public string EventName { get; set; } = "BookingCancelled";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public BookingCancelledData Data { get; set; } = null!;
}

public class BookingCancelledData
{
    public Guid BookingId { get; set; }
    public Guid UserId { get; set; }
    public string RoomId { get; set; } = null!;
    public string Reason { get; set; } = null!;
    public DateTime CancelledAt { get; set; }
}
