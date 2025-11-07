using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentService.DTOs;
using PaymentService.Services;

namespace PaymentService.Controllers;

/// <summary>
/// Payment API controller
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize] // Require authentication for all endpoints
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(IPaymentService paymentService, ILogger<PaymentController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    /// <summary>
    /// Process a payment
    /// </summary>
    /// <param name="request">Payment processing request</param>
    /// <returns>Payment response</returns>
    [HttpPost("pay")]
    public async Task<ActionResult<PaymentResponse>> ProcessPayment([FromBody] ProcessPaymentRequest request)
    {
        try
        {
            _logger.LogInformation("Received payment request for BookingId: {BookingId}", request.BookingId);

            var result = await _paymentService.ProcessPaymentAsync(request);

            if (result.Status == "SUCCESS")
            {
                return Ok(result);
            }
            else
            {
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment for BookingId: {BookingId}", request.BookingId);
            return StatusCode(500, new { message = "An error occurred while processing the payment", error = ex.Message });
        }
    }

    /// <summary>
    /// Get payment by ID
    /// </summary>
    /// <param name="id">Payment ID</param>
    /// <returns>Payment details</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<PaymentResponse>> GetPaymentById(Guid id)
    {
        try
        {
            var payment = await _paymentService.GetPaymentByIdAsync(id);

            if (payment == null)
            {
                return NotFound(new { message = $"Payment with ID {id} not found" });
            }

            return Ok(payment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving payment with ID: {PaymentId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the payment", error = ex.Message });
        }
    }

    /// <summary>
    /// Get payment by booking ID
    /// </summary>
    /// <param name="bookingId">Booking ID</param>
    /// <returns>Payment details</returns>
    [HttpGet("booking/{bookingId}")]
    public async Task<ActionResult<PaymentResponse>> GetPaymentByBookingId(Guid bookingId)
    {
        try
        {
            var payment = await _paymentService.GetPaymentByBookingIdAsync(bookingId);

            if (payment == null)
            {
                return NotFound(new { message = $"Payment for booking {bookingId} not found" });
            }

            return Ok(payment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving payment for BookingId: {BookingId}", bookingId);
            return StatusCode(500, new { message = "An error occurred while retrieving the payment", error = ex.Message });
        }
    }
}
