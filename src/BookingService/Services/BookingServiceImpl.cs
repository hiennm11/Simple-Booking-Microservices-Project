using BookingService.Data;
using BookingService.DTOs;
using BookingService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Contracts;
using Shared.EventBus;
using Polly;

namespace BookingService.Services;

/// <summary>
/// Service interface for booking operations
/// </summary>
public interface IBookingService
{
    Task<BookingResponse> CreateBookingAsync(CreateBookingRequest request, CancellationToken cancellationToken = default);
    Task<BookingResponse?> GetBookingByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<BookingResponse>> GetBookingsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<BookingResponse?> UpdateBookingStatusAsync(Guid id, UpdateBookingStatusRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of booking service with database and event bus integration
/// </summary>
public class BookingServiceImpl : IBookingService
{
    private readonly BookingDbContext _dbContext;
    private readonly IEventBus _eventBus;
    private readonly ILogger<BookingServiceImpl> _logger;
    private readonly EventBus.RabbitMQSettings _rabbitMQSettings;
    private readonly ResiliencePipeline _eventPublishingPipeline;

    public BookingServiceImpl(
        BookingDbContext dbContext,
        IEventBus eventBus,
        IOptions<EventBus.RabbitMQSettings> rabbitMQSettings,
        IResiliencePipelineService resiliencePipelineService,
        ILogger<BookingServiceImpl> logger)
    {
        _dbContext = dbContext;
        _eventBus = eventBus;
        _logger = logger;
        _rabbitMQSettings = rabbitMQSettings.Value;
        _eventPublishingPipeline = resiliencePipelineService.GetEventPublishingPipeline();
    }

    public async Task<BookingResponse> CreateBookingAsync(CreateBookingRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating booking for user {UserId}, room {RoomId}", request.UserId, request.RoomId);

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            RoomId = request.RoomId,
            Amount = request.Amount,
            Status = "PENDING",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Bookings.Add(booking);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Booking created with ID: {BookingId}", booking.Id);

        // Publish BookingCreated event
        try
        {
            var bookingCreatedEvent = new BookingCreatedEvent
            {
                EventId = Guid.NewGuid(),
                EventName = "BookingCreated",
                Timestamp = DateTime.UtcNow,
                Data = new BookingCreatedData
                {
                    BookingId = booking.Id,
                    UserId = booking.UserId,
                    RoomId = booking.RoomId,
                    Amount = booking.Amount,
                    Status = booking.Status
                }
            };

            var queueName = _rabbitMQSettings.Queues.GetValueOrDefault("BookingCreated", "booking_created");
            
            // Execute with retry policy using Polly resilience pipeline
            await _eventPublishingPipeline.ExecuteAsync(async ct =>
            {
                await _eventBus.PublishAsync(bookingCreatedEvent, queueName, ct);
            }, cancellationToken);

            _logger.LogInformation("BookingCreated event published for booking {BookingId}", booking.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish BookingCreated event after retries for booking {BookingId}", booking.Id);
            // Event is lost after all retry attempts
            // In production, consider storing in outbox table for later retry or manual intervention
            // Don't fail the booking creation if event publishing fails
        }

        return MapToResponse(booking);
    }

    public async Task<BookingResponse?> GetBookingByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving booking with ID: {BookingId}", id);

        var booking = await _dbContext.Bookings
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        return booking == null ? null : MapToResponse(booking);
    }

    public async Task<IEnumerable<BookingResponse>> GetBookingsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving bookings for user: {UserId}", userId);

        var bookings = await _dbContext.Bookings
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);

        return bookings.Select(MapToResponse);
    }

    public async Task<BookingResponse?> UpdateBookingStatusAsync(Guid id, UpdateBookingStatusRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating booking {BookingId} status to {Status}", id, request.Status);

        var booking = await _dbContext.Bookings
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (booking == null)
        {
            _logger.LogWarning("Booking not found with ID: {BookingId}", id);
            return null;
        }

        booking.Status = request.Status;
        booking.UpdatedAt = DateTime.UtcNow;

        if (request.Status == "CONFIRMED" && booking.ConfirmedAt == null)
        {
            booking.ConfirmedAt = DateTime.UtcNow;
        }
        else if (request.Status == "CANCELLED")
        {
            booking.CancelledAt = DateTime.UtcNow;
            booking.CancellationReason = request.CancellationReason;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Booking {BookingId} status updated to {Status}", id, request.Status);

        return MapToResponse(booking);
    }

    private static BookingResponse MapToResponse(Booking booking)
    {
        return new BookingResponse
        {
            Id = booking.Id,
            UserId = booking.UserId,
            RoomId = booking.RoomId,
            Amount = booking.Amount,
            Status = booking.Status,
            CreatedAt = booking.CreatedAt,
            UpdatedAt = booking.UpdatedAt,
            ConfirmedAt = booking.ConfirmedAt,
            CancelledAt = booking.CancelledAt,
            CancellationReason = booking.CancellationReason
        };
    }
}
