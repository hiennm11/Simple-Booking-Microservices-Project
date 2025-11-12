using InventoryService.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Data;

/// <summary>
/// Database context for Inventory Service
/// </summary>
public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) 
        : base(options)
    {
    }

    public DbSet<InventoryItem> InventoryItems { get; set; } = null!;
    public DbSet<InventoryReservation> InventoryReservations { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure InventoryItem
        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ItemId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.TotalQuantity).IsRequired();
            entity.Property(e => e.AvailableQuantity).IsRequired();
            entity.Property(e => e.ReservedQuantity).IsRequired();
            
            // Create index on ItemId for fast lookups
            entity.HasIndex(e => e.ItemId).IsUnique();
        });

        // Configure InventoryReservation
        modelBuilder.Entity<InventoryReservation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BookingId).IsRequired();
            entity.Property(e => e.InventoryItemId).IsRequired();
            entity.Property(e => e.Quantity).IsRequired();
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.ExpiresAt).IsRequired();
            
            // Create index on BookingId for fast lookups
            entity.HasIndex(e => e.BookingId).IsUnique();
            
            // Create index on Status for filtering
            entity.HasIndex(e => e.Status);
            
            // Create index on ExpiresAt for cleanup queries
            entity.HasIndex(e => e.ExpiresAt);
            
            // Configure relationship
            entity.HasOne(e => e.InventoryItem)
                .WithMany(i => i.Reservations)
                .HasForeignKey(e => e.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Seed initial data
        SeedData(modelBuilder);
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
        // Use fixed date for seed data to avoid migration warnings
        var seedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        
        // Seed some sample rooms/inventory items
        var items = new[]
        {
            new InventoryItem
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                ItemId = "ROOM-101",
                Name = "Deluxe Room 101",
                TotalQuantity = 1,
                AvailableQuantity = 1,
                ReservedQuantity = 0,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            },
            new InventoryItem
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                ItemId = "ROOM-102",
                Name = "Standard Room 102",
                TotalQuantity = 1,
                AvailableQuantity = 1,
                ReservedQuantity = 0,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            },
            new InventoryItem
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                ItemId = "ROOM-201",
                Name = "Suite Room 201",
                TotalQuantity = 1,
                AvailableQuantity = 1,
                ReservedQuantity = 0,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            },
            new InventoryItem
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                ItemId = "ROOM-202",
                Name = "Standard Room 202",
                TotalQuantity = 1,
                AvailableQuantity = 1,
                ReservedQuantity = 0,
                CreatedAt = seedDate,
                UpdatedAt = seedDate
            }
        };

        modelBuilder.Entity<InventoryItem>().HasData(items);
    }
}
