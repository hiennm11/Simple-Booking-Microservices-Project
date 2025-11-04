using Microsoft.EntityFrameworkCore;
using UserService.Models;

namespace UserService.Data;

public static class UserDbSeeder
{
    private const string DefaultPassword = "Password@123";
    
    public static async Task SeedAsync(UserDbContext context, ILogger logger)
    {
        try
        {
            // Check if users already exist
            if (await context.Users.AnyAsync())
            {
                logger.LogInformation("Users already exist. Skipping seed.");
                return;
            }

            logger.LogInformation("Seeding user data...");

            var users = new List<User>
            {
                new User
                {
                    Id = Guid.NewGuid(),
                    Username = "admin",
                    Email = "admin@bookingsystem.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                    FirstName = "Admin",
                    LastName = "User",
                    PhoneNumber = "+1234567890",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Username = "john.doe",
                    Email = "john.doe@example.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
                    FirstName = "John",
                    LastName = "Doe",
                    PhoneNumber = "+1234567891",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Username = "jane.smith",
                    Email = "jane.smith@example.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
                    FirstName = "Jane",
                    LastName = "Smith",
                    PhoneNumber = "+1234567892",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Username = "bob.johnson",
                    Email = "bob.johnson@example.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
                    FirstName = "Bob",
                    LastName = "Johnson",
                    PhoneNumber = "+1234567893",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Username = "alice.williams",
                    Email = "alice.williams@example.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
                    FirstName = "Alice",
                    LastName = "Williams",
                    PhoneNumber = "+1234567894",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }
            };

            await context.Users.AddRangeAsync(users);
            await context.SaveChangesAsync();

            logger.LogInformation("Successfully seeded {Count} users", users.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding user data");
            throw;
        }
    }
}
