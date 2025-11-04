using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UserService.Data;

public class UserDbContextFactory(IConfiguration configuration) : IDesignTimeDbContextFactory<UserDbContext>
{
    public UserDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<UserDbContext>();    
        optionsBuilder.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));

        return new UserDbContext(optionsBuilder.Options);
    }
}
