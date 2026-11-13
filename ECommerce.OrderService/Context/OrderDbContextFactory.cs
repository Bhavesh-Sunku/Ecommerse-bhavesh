using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECommerce.OrderService.Context;

public class OrderDbContextFactory : IDesignTimeDbContextFactory<OrderDbContext>
{
    public OrderDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ORDER_SERVICE_CONNECTION")
            ?? "Server=(localdb)\\mssqllocaldb;Database=ECommerceOrders;Trusted_Connection=True;TrustServerCertificate=True;";
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new OrderDbContext(options);
    }
}
