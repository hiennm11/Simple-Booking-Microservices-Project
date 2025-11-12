namespace Shared.Contracts;

/// <summary>
/// Event published when inventory reservation is released
/// </summary>
public class InventoryReleasedEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public Guid CorrelationId { get; set; }
    public string EventName { get; set; } = "InventoryReleased";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public InventoryReleasedData Data { get; set; } = null!;
}

public class InventoryReleasedData
{
    public Guid ReservationId { get; set; }
    public Guid BookingId { get; set; }
    public string ItemId { get; set; } = null!;
    public int Quantity { get; set; }
    public string Reason { get; set; } = null!;
    public DateTime ReleasedAt { get; set; }
}
