using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BookingService.Data;

/// <summary>
/// Factory for creating DbContext instances at design time (for migrations)
/// </summary>
public class BookingDbContextFactory(IConfiguration configuration) : IDesignTimeDbContextFactory<BookingDbContext>
{
    public BookingDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BookingDbContext>();

        // Use a default connection string for migrations
        // This will be overridden at runtime by appsettings.json
        optionsBuilder.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));

        return new BookingDbContext(optionsBuilder.Options);
    }
}
