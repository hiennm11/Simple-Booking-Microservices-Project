using System.ComponentModel.DataAnnotations;

namespace BookingService.DTOs;

/// <summary>
/// Request model for updating booking status
/// </summary>
public class UpdateBookingStatusRequest
{
    [Required]
    [RegularExpression("^(PENDING|CONFIRMED|CANCELLED)$", 
        ErrorMessage = "Status must be PENDING, CONFIRMED, or CANCELLED")]
    public string Status { get; set; } = null!;
    
    public string? CancellationReason { get; set; }
}
