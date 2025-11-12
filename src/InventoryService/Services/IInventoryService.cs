using InventoryService.DTOs;

namespace InventoryService.Services;

public interface IInventoryService
{
    /// <summary>
    /// Reserve inventory for a booking
    /// </summary>
    Task<ReserveInventoryResponse> ReserveAsync(ReserveInventoryRequest request);
    
    /// <summary>
    /// Release inventory reservation (e.g., when payment fails)
    /// </summary>
    Task<ReleaseInventoryResponse> ReleaseAsync(ReleaseInventoryRequest request);
    
    /// <summary>
    /// Check if inventory is available
    /// </summary>
    Task<CheckAvailabilityResponse> CheckAvailabilityAsync(CheckAvailabilityRequest request);
    
    /// <summary>
    /// Confirm a reservation (e.g., when payment succeeds)
    /// </summary>
    Task ConfirmReservationAsync(Guid bookingId);
    
    /// <summary>
    /// Get all inventory items
    /// </summary>
    Task<List<InventoryItemResponse>> GetAllInventoryAsync();
    
    /// <summary>
    /// Get inventory item by ItemId
    /// </summary>
    Task<InventoryItemResponse?> GetInventoryByItemIdAsync(string itemId);
}
