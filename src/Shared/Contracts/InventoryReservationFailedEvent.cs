namespace Shared.Contracts;

/// <summary>
/// Event published when inventory reservation fails (insufficient inventory or item not found)
/// </summary>
public class InventoryReservationFailedEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public Guid CorrelationId { get; set; }
    public string EventName { get; set; } = "InventoryReservationFailed";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public InventoryReservationFailedData Data { get; set; } = null!;
}

public class InventoryReservationFailedData
{
    public Guid BookingId { get; set; }
    public string ItemId { get; set; } = null!;
    public string Reason { get; set; } = null!;
    public DateTime FailedAt { get; set; } = DateTime.UtcNow;
}
