using System.ComponentModel.DataAnnotations;

namespace PaymentService.DTOs;

/// <summary>
/// Request to process a payment
/// </summary>
public class ProcessPaymentRequest
{
    [Required]
    public Guid BookingId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; } = "CREDIT_CARD";

    public Guid CorrelationId { get; set; }
}
