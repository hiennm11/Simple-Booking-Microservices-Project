namespace Shared.Contracts;

/// <summary>
/// Event published when inventory is successfully reserved
/// </summary>
public class InventoryReservedEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public Guid CorrelationId { get; set; }
    public string EventName { get; set; } = "InventoryReserved";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public InventoryReservedData Data { get; set; } = null!;
}

public class InventoryReservedData
{
    public Guid ReservationId { get; set; }
    public Guid BookingId { get; set; }
    public string ItemId { get; set; } = null!;
    public int Quantity { get; set; }
    public string Status { get; set; } = "RESERVED";
    public DateTime ExpiresAt { get; set; }
}
