using Shared.Common;

namespace BookingService.Models;

/// <summary>
/// Represents a booking entity
/// </summary>
public class Booking : BaseEntity
{
    public Guid UserId { get; set; }
    public string RoomId { get; set; } = null!;
    public decimal Amount { get; set; }
    
    /// <summary>
    /// Booking status: PENDING, CONFIRMED, CANCELLED
    /// </summary>
    public string Status { get; set; } = "PENDING";
    
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
}
