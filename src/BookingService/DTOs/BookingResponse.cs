namespace BookingService.DTOs;

/// <summary>
/// Response model for booking information
/// </summary>
public class BookingResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string RoomId { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
}
