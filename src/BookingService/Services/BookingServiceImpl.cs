using BookingService.Data;
using BookingService.DTOs;
using BookingService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Contracts;
using Shared.EventBus;
using Shared.Services;
using Polly;
using Serilog.Context;

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
/// Implementation of booking service with database and Outbox pattern for reliable event publishing
/// </summary>
public class BookingServiceImpl : IBookingService
{
    private readonly BookingDbContext _dbContext;
    private readonly IOutboxService _outboxService;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly ILogger<BookingServiceImpl> _logger;

    public BookingServiceImpl(
        BookingDbContext dbContext,
        IOutboxService outboxService,
        ICorrelationIdAccessor correlationIdAccessor,
        ILogger<BookingServiceImpl> logger)
    {
        _dbContext = dbContext;
        _outboxService = outboxService;
        _correlationIdAccessor = correlationIdAccessor;
        _logger = logger;
    }

    public async Task<BookingResponse> CreateBookingAsync(CreateBookingRequest request, CancellationToken cancellationToken = default)
    {
        // Get correlation ID from HTTP context, or generate a new one if not available
        var correlationId = _correlationIdAccessor.GetCorrelationIdAsGuid();
        if (correlationId == Guid.Empty)
        {
            correlationId = Guid.NewGuid();
            _logger.LogWarning("No correlation ID found in context, generated new one: {CorrelationId}", correlationId);
        }
        
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            _logger.LogInformation("Creating booking for user {UserId}, room {RoomId}", request.UserId, request.RoomId);

            // Start a database transaction to ensure atomicity
            using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            
            try
            {
                // 1. Create and save the booking
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
                
                // 2. Create the event with correlation ID
                var bookingCreatedEvent = new BookingCreatedEvent
                {
                    EventId = Guid.NewGuid(),
                    CorrelationId = correlationId,
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

                // 3. Save event to outbox table (in same transaction!)
                await _outboxService.AddToOutboxAsync(
                    bookingCreatedEvent, 
                    "BookingCreated", 
                    cancellationToken);

                // 4. Commit transaction - both booking and outbox message are saved atomically
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "Booking created with ID: {BookingId} and event saved to outbox", 
                    booking.Id);

                return MapToResponse(booking);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create booking and save event to outbox");
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
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
