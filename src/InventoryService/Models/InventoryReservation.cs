using Shared.Common;

namespace InventoryService.Models;

/// <summary>
/// Represents a reservation of inventory for a booking
/// </summary>
public class InventoryReservation : BaseEntity
{
    /// <summary>
    /// Reference to the booking this reservation is for
    /// </summary>
    public Guid BookingId { get; set; }
    
    /// <summary>
    /// Reference to the inventory item being reserved
    /// </summary>
    public Guid InventoryItemId { get; set; }
    
    /// <summary>
    /// Quantity reserved
    /// </summary>
    public int Quantity { get; set; }
    
    /// <summary>
    /// Status: RESERVED, CONFIRMED, RELEASED, EXPIRED
    /// </summary>
    public string Status { get; set; } = "RESERVED";
    
    /// <summary>
    /// When the reservation was confirmed (payment succeeded)
    /// </summary>
    public DateTime? ConfirmedAt { get; set; }
    
    /// <summary>
    /// When the reservation was released (payment failed or cancelled)
    /// </summary>
    public DateTime? ReleasedAt { get; set; }
    
    /// <summary>
    /// Reason for release (if applicable)
    /// </summary>
    public string? ReleaseReason { get; set; }
    
    /// <summary>
    /// Reservation expires at (for automatic cleanup)
    /// </summary>
    public DateTime ExpiresAt { get; set; }
    
    /// <summary>
    /// Navigation property to inventory item
    /// </summary>
    public InventoryItem InventoryItem { get; set; } = null!;
}
