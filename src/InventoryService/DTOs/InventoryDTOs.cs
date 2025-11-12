namespace InventoryService.DTOs;

public class ReserveInventoryRequest
{
    public Guid BookingId { get; set; }
    public string ItemId { get; set; } = null!;
    public int Quantity { get; set; } = 1;
}

public class ReserveInventoryResponse
{
    public Guid ReservationId { get; set; }
    public Guid BookingId { get; set; }
    public string ItemId { get; set; } = null!;
    public int Quantity { get; set; }
    public string Status { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
}

public class ReleaseInventoryRequest
{
    public Guid BookingId { get; set; }
    public string? Reason { get; set; }
}

public class ReleaseInventoryResponse
{
    public Guid ReservationId { get; set; }
    public Guid BookingId { get; set; }
    public string Status { get; set; } = null!;
    public DateTime ReleasedAt { get; set; }
}

public class CheckAvailabilityRequest
{
    public string ItemId { get; set; } = null!;
    public int Quantity { get; set; } = 1;
}

public class CheckAvailabilityResponse
{
    public string ItemId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public int TotalQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public bool IsAvailable { get; set; }
}

public class InventoryItemResponse
{
    public Guid Id { get; set; }
    public string ItemId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public int TotalQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public int ReservedQuantity { get; set; }
}
