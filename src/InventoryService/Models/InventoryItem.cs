using Shared.Common;

namespace InventoryService.Models;

/// <summary>
/// Represents an inventory item (e.g., a room that can be booked)
/// </summary>
public class InventoryItem : BaseEntity
{
    /// <summary>
    /// Unique identifier for the room/item
    /// </summary>
    public string ItemId { get; set; } = null!;
    
    /// <summary>
    /// Name/description of the item
    /// </summary>
    public string Name { get; set; } = null!;
    
    /// <summary>
    /// Total quantity available
    /// </summary>
    public int TotalQuantity { get; set; }
    
    /// <summary>
    /// Currently available quantity (not reserved)
    /// </summary>
    public int AvailableQuantity { get; set; }
    
    /// <summary>
    /// Currently reserved quantity
    /// </summary>
    public int ReservedQuantity { get; set; }
    
    /// <summary>
    /// Navigation property to reservations
    /// </summary>
    public ICollection<InventoryReservation> Reservations { get; set; } = new List<InventoryReservation>();
}
