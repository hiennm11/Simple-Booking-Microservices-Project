using BookingService.DTOs;
using BookingService.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookingService.Controllers;

/// <summary>
/// Controller for managing bookings
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;
    private readonly ILogger<BookingsController> _logger;

    public BookingsController(IBookingService bookingService, ILogger<BookingsController> logger)
    {
        _bookingService = bookingService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new booking
    /// </summary>
    /// <param name="request">Booking creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created booking</returns>
    [HttpPost]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BookingResponse>> CreateBooking(
        [FromBody] CreateBookingRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var booking = await _bookingService.CreateBookingAsync(request, cancellationToken);
            _logger.LogInformation("Booking created successfully: {BookingId}", booking.Id);
            
            return CreatedAtAction(
                nameof(GetBookingById),
                new { id = booking.Id },
                booking);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating booking");
            return StatusCode(500, new { error = "An error occurred while creating the booking" });
        }
    }

    /// <summary>
    /// Get booking by ID
    /// </summary>
    /// <param name="id">Booking ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Booking details</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BookingResponse>> GetBookingById(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var booking = await _bookingService.GetBookingByIdAsync(id, cancellationToken);
            
            if (booking == null)
            {
                _logger.LogWarning("Booking not found: {BookingId}", id);
                return NotFound(new { error = $"Booking with ID {id} not found" });
            }

            return Ok(booking);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving booking: {BookingId}", id);
            return StatusCode(500, new { error = "An error occurred while retrieving the booking" });
        }
    }

    /// <summary>
    /// Get all bookings for a specific user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of user's bookings</returns>
    [HttpGet("user/{userId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<BookingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<BookingResponse>>> GetBookingsByUserId(
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            var bookings = await _bookingService.GetBookingsByUserIdAsync(userId, cancellationToken);
            return Ok(bookings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bookings for user: {UserId}", userId);
            return StatusCode(500, new { error = "An error occurred while retrieving bookings" });
        }
    }

    /// <summary>
    /// Update booking status
    /// </summary>
    /// <param name="id">Booking ID</param>
    /// <param name="request">Status update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated booking</returns>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BookingResponse>> UpdateBookingStatus(
        Guid id,
        [FromBody] UpdateBookingStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var booking = await _bookingService.UpdateBookingStatusAsync(id, request, cancellationToken);
            
            if (booking == null)
            {
                _logger.LogWarning("Booking not found for status update: {BookingId}", id);
                return NotFound(new { error = $"Booking with ID {id} not found" });
            }

            _logger.LogInformation("Booking status updated: {BookingId} -> {Status}", id, request.Status);
            return Ok(booking);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating booking status: {BookingId}", id);
            return StatusCode(500, new { error = "An error occurred while updating the booking status" });
        }
    }
}
