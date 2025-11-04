using System.ComponentModel.DataAnnotations;

namespace BookingService.DTOs;

/// <summary>
/// Request model for creating a new booking
/// </summary>
public class CreateBookingRequest
{
    [Required]
    public Guid UserId { get; set; }
    
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string RoomId { get; set; } = null!;
    
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    public decimal Amount { get; set; }
}
