using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECommerce.ProductService.Context;

public class ProductDbContextFactory : IDesignTimeDbContextFactory<ProductDbContext>
{
    public ProductDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("PRODUCT_SERVICE_CONNECTION")
            ?? "Server=(localdb)\\mssqllocaldb;Database=ECommerceProductsDb;Trusted_Connection=True;TrustServerCertificate=True;";

        var options = new DbContextOptionsBuilder<ProductDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ProductDbContext(options);
    }
}
