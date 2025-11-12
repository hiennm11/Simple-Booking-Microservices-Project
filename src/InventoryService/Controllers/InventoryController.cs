using InventoryService.DTOs;
using InventoryService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(
        IInventoryService inventoryService,
        ILogger<InventoryController> logger)
    {
        _inventoryService = inventoryService;
        _logger = logger;
    }

    /// <summary>
    /// Get all inventory items
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllInventory()
    {
        try
        {
            var items = await _inventoryService.GetAllInventoryAsync();
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all inventory");
            return StatusCode(500, new { error = "Failed to retrieve inventory" });
        }
    }

    /// <summary>
    /// Get inventory by ItemId
    /// </summary>
    [HttpGet("{itemId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetInventoryByItemId(string itemId)
    {
        try
        {
            var item = await _inventoryService.GetInventoryByItemIdAsync(itemId);
            if (item == null)
            {
                return NotFound(new { error = $"Inventory item {itemId} not found" });
            }

            return Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inventory for ItemId={ItemId}", itemId);
            return StatusCode(500, new { error = "Failed to retrieve inventory" });
        }
    }

    /// <summary>
    /// Check availability of an inventory item
    /// </summary>
    [HttpPost("check-availability")]
    [AllowAnonymous]
    public async Task<IActionResult> CheckAvailability([FromBody] CheckAvailabilityRequest request)
    {
        try
        {
            var result = await _inventoryService.CheckAvailabilityAsync(request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Inventory item not found: ItemId={ItemId}", request.ItemId);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking availability for ItemId={ItemId}", request.ItemId);
            return StatusCode(500, new { error = "Failed to check availability" });
        }
    }

    /// <summary>
    /// Reserve inventory (typically called by booking workflow)
    /// </summary>
    [HttpPost("reserve")]
    public async Task<IActionResult> ReserveInventory([FromBody] ReserveInventoryRequest request)
    {
        try
        {
            var result = await _inventoryService.ReserveAsync(request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to reserve inventory: BookingId={BookingId}, ItemId={ItemId}",
                request.BookingId, request.ItemId);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reserving inventory: BookingId={BookingId}, ItemId={ItemId}",
                request.BookingId, request.ItemId);
            return StatusCode(500, new { error = "Failed to reserve inventory" });
        }
    }

    /// <summary>
    /// Release inventory reservation
    /// </summary>
    [HttpPost("release")]
    public async Task<IActionResult> ReleaseInventory([FromBody] ReleaseInventoryRequest request)
    {
        try
        {
            var result = await _inventoryService.ReleaseAsync(request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to release inventory: BookingId={BookingId}", request.BookingId);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing inventory: BookingId={BookingId}", request.BookingId);
            return StatusCode(500, new { error = "Failed to release inventory" });
        }
    }
}
