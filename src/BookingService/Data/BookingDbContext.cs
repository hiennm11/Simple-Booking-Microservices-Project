using Microsoft.EntityFrameworkCore;
using BookingService.Models;

namespace BookingService.Data;

/// <summary>
/// Database context for BookingService
/// </summary>
public class BookingDbContext : DbContext
{
    public BookingDbContext(DbContextOptions<BookingDbContext> options) : base(options)
    {
    }

    public DbSet<Booking> Bookings { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.ToTable("bookings");
            
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Id)
                .HasColumnName("id")
                .ValueGeneratedNever();
            
            entity.Property(e => e.UserId)
                .HasColumnName("user_id")
                .IsRequired();
            
            entity.Property(e => e.RoomId)
                .HasColumnName("room_id")
                .HasMaxLength(100)
                .IsRequired();
            
            entity.Property(e => e.Amount)
                .HasColumnName("amount")
                .HasColumnType("decimal(18,2)")
                .IsRequired();
            
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasMaxLength(20)
                .IsRequired()
                .HasDefaultValue("PENDING");
            
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();
            
            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at");
            
            entity.Property(e => e.ConfirmedAt)
                .HasColumnName("confirmed_at");
            
            entity.Property(e => e.CancelledAt)
                .HasColumnName("cancelled_at");
            
            entity.Property(e => e.CancellationReason)
                .HasColumnName("cancellation_reason")
                .HasMaxLength(500);

            // Indexes
            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("idx_bookings_user_id");
            
            entity.HasIndex(e => e.Status)
                .HasDatabaseName("idx_bookings_status");
            
            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("idx_bookings_created_at");
        });
    }
}
