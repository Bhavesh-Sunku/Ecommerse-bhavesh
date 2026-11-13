using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECommerce.UserService.Context;

public class UserDbContextFactory : IDesignTimeDbContextFactory<UserDbContext>
{
    public UserDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("USER_SERVICE_CONNECTION")
            ?? "Server=(localdb)\\mssqllocaldb;Database=ECommerceUsers;Trusted_Connection=True;TrustServerCertificate=True;";
        var options = new DbContextOptionsBuilder<UserDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new UserDbContext(options);
    }
}
