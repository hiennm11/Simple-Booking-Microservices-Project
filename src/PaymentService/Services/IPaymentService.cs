using PaymentService.DTOs;

namespace PaymentService.Services;

/// <summary>
/// Payment service interface
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Process a payment for a booking
    /// </summary>
    Task<PaymentResponse> ProcessPaymentAsync(ProcessPaymentRequest request);

    /// <summary>
    /// Get payment by ID
    /// </summary>
    Task<PaymentResponse?> GetPaymentByIdAsync(Guid paymentId);

    /// <summary>
    /// Get payment by booking ID
    /// </summary>
    Task<PaymentResponse?> GetPaymentByBookingIdAsync(Guid bookingId);

    /// <summary>
    /// Retry a failed payment for a booking
    /// </summary>
    Task<PaymentResponse> RetryPaymentAsync(RetryPaymentRequest request);
}
