using InventoryService.Data;
using InventoryService.DTOs;
using InventoryService.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Services;

public class InventoryManagementService : IInventoryService
{
    private readonly InventoryDbContext _dbContext;
    private readonly ILogger<InventoryManagementService> _logger;

    public InventoryManagementService(
        InventoryDbContext dbContext,
        ILogger<InventoryManagementService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ReserveInventoryResponse> ReserveAsync(ReserveInventoryRequest request)
    {
        _logger.LogInformation("Attempting to reserve inventory: ItemId={ItemId}, Quantity={Quantity}, BookingId={BookingId}",
            request.ItemId, request.Quantity, request.BookingId);

        // Check if booking already has a reservation
        var existingReservation = await _dbContext.InventoryReservations
            .FirstOrDefaultAsync(r => r.BookingId == request.BookingId);

        if (existingReservation != null)
        {
            _logger.LogWarning("Booking {BookingId} already has a reservation", request.BookingId);
            
            return new ReserveInventoryResponse
            {
                ReservationId = existingReservation.Id,
                BookingId = existingReservation.BookingId,
                ItemId = request.ItemId,
                Quantity = existingReservation.Quantity,
                Status = existingReservation.Status,
                ExpiresAt = existingReservation.ExpiresAt
            };
        }

        // Find inventory item
        var inventoryItem = await _dbContext.InventoryItems
            .FirstOrDefaultAsync(i => i.ItemId == request.ItemId);

        if (inventoryItem == null)
        {
            _logger.LogError("Inventory item {ItemId} not found", request.ItemId);
            throw new InvalidOperationException($"Inventory item {request.ItemId} not found");
        }

        // Check availability
        if (inventoryItem.AvailableQuantity < request.Quantity)
        {
            _logger.LogWarning("Insufficient inventory: ItemId={ItemId}, Available={Available}, Requested={Requested}",
                request.ItemId, inventoryItem.AvailableQuantity, request.Quantity);
            throw new InvalidOperationException($"Insufficient inventory for {request.ItemId}. Available: {inventoryItem.AvailableQuantity}, Requested: {request.Quantity}");
        }

        // Create reservation
        var reservation = new InventoryReservation
        {
            Id = Guid.NewGuid(),
            BookingId = request.BookingId,
            InventoryItemId = inventoryItem.Id,
            Quantity = request.Quantity,
            Status = "RESERVED",
            ExpiresAt = DateTime.UtcNow.AddMinutes(15), // Reservation expires in 15 minutes
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Update inventory quantities
        inventoryItem.AvailableQuantity -= request.Quantity;
        inventoryItem.ReservedQuantity += request.Quantity;
        inventoryItem.UpdatedAt = DateTime.UtcNow;

        await _dbContext.InventoryReservations.AddAsync(reservation);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Successfully reserved inventory: ReservationId={ReservationId}, ItemId={ItemId}",
            reservation.Id, request.ItemId);

        return new ReserveInventoryResponse
        {
            ReservationId = reservation.Id,
            BookingId = reservation.BookingId,
            ItemId = request.ItemId,
            Quantity = reservation.Quantity,
            Status = reservation.Status,
            ExpiresAt = reservation.ExpiresAt
        };
    }

    public async Task<ReleaseInventoryResponse> ReleaseAsync(ReleaseInventoryRequest request)
    {
        _logger.LogInformation("Attempting to release inventory reservation for BookingId={BookingId}", request.BookingId);

        var reservation = await _dbContext.InventoryReservations
            .Include(r => r.InventoryItem)
            .FirstOrDefaultAsync(r => r.BookingId == request.BookingId && r.Status == "RESERVED");

        if (reservation == null)
        {
            _logger.LogWarning("No active reservation found for BookingId={BookingId}", request.BookingId);
            throw new InvalidOperationException($"No active reservation found for booking {request.BookingId}");
        }

        // Update reservation
        reservation.Status = "RELEASED";
        reservation.ReleasedAt = DateTime.UtcNow;
        reservation.ReleaseReason = request.Reason;
        reservation.UpdatedAt = DateTime.UtcNow;

        // Update inventory quantities
        reservation.InventoryItem.AvailableQuantity += reservation.Quantity;
        reservation.InventoryItem.ReservedQuantity -= reservation.Quantity;
        reservation.InventoryItem.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Successfully released inventory reservation: ReservationId={ReservationId}, BookingId={BookingId}",
            reservation.Id, request.BookingId);

        return new ReleaseInventoryResponse
        {
            ReservationId = reservation.Id,
            BookingId = reservation.BookingId,
            Status = reservation.Status,
            ReleasedAt = reservation.ReleasedAt.Value
        };
    }

    public async Task<CheckAvailabilityResponse> CheckAvailabilityAsync(CheckAvailabilityRequest request)
    {
        var inventoryItem = await _dbContext.InventoryItems
            .FirstOrDefaultAsync(i => i.ItemId == request.ItemId);

        if (inventoryItem == null)
        {
            throw new InvalidOperationException($"Inventory item {request.ItemId} not found");
        }

        return new CheckAvailabilityResponse
        {
            ItemId = inventoryItem.ItemId,
            Name = inventoryItem.Name,
            TotalQuantity = inventoryItem.TotalQuantity,
            AvailableQuantity = inventoryItem.AvailableQuantity,
            ReservedQuantity = inventoryItem.ReservedQuantity,
            IsAvailable = inventoryItem.AvailableQuantity >= request.Quantity
        };
    }

    public async Task ConfirmReservationAsync(Guid bookingId)
    {
        _logger.LogInformation("Confirming reservation for BookingId={BookingId}", bookingId);

        var reservation = await _dbContext.InventoryReservations
            .FirstOrDefaultAsync(r => r.BookingId == bookingId && r.Status == "RESERVED");

        if (reservation == null)
        {
            _logger.LogWarning("No active reservation found for BookingId={BookingId}", bookingId);
            return; // Idempotent - already confirmed or released
        }

        reservation.Status = "CONFIRMED";
        reservation.ConfirmedAt = DateTime.UtcNow;
        reservation.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Successfully confirmed reservation: ReservationId={ReservationId}, BookingId={BookingId}",
            reservation.Id, bookingId);
    }

    public async Task<List<InventoryItemResponse>> GetAllInventoryAsync()
    {
        var items = await _dbContext.InventoryItems
            .AsNoTracking()
            .ToListAsync();

        return items.Select(i => new InventoryItemResponse
        {
            Id = i.Id,
            ItemId = i.ItemId,
            Name = i.Name,
            TotalQuantity = i.TotalQuantity,
            AvailableQuantity = i.AvailableQuantity,
            ReservedQuantity = i.ReservedQuantity
        }).ToList();
    }

    public async Task<InventoryItemResponse?> GetInventoryByItemIdAsync(string itemId)
    {
        var item = await _dbContext.InventoryItems
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.ItemId == itemId);

        if (item == null)
        {
            return null;
        }

        return new InventoryItemResponse
        {
            Id = item.Id,
            ItemId = item.ItemId,
            Name = item.Name,
            TotalQuantity = item.TotalQuantity,
            AvailableQuantity = item.AvailableQuantity,
            ReservedQuantity = item.ReservedQuantity
        };
    }
}
