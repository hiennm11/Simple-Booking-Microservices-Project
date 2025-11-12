namespace Shared.Contracts;

/// <summary>
/// Event published when a new booking is created
/// </summary>
public class BookingCreatedEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public Guid CorrelationId { get; set; }
    public string EventName { get; set; } = "BookingCreated";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public BookingCreatedData Data { get; set; } = null!;
}

public class BookingCreatedData
{
    public Guid BookingId { get; set; }
    public Guid UserId { get; set; }
    public string RoomId { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Status { get; set; } = "PENDING";
}
