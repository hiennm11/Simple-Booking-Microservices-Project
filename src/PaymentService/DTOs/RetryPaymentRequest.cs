using System.ComponentModel.DataAnnotations;

namespace PaymentService.DTOs;

/// <summary>
/// Request to retry a failed payment
/// </summary>
public class RetryPaymentRequest
{
    /// <summary>
    /// Booking ID to retry payment for
    /// </summary>
    [Required(ErrorMessage = "BookingId is required")]
    public Guid BookingId { get; set; }

    /// <summary>
    /// Optional: New payment method to use for retry
    /// </summary>
    public string? PaymentMethod { get; set; }
}
