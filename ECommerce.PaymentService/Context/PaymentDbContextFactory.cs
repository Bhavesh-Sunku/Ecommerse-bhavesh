using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECommerce.Payments.Context;

public class PaymentDbContextFactory : IDesignTimeDbContextFactory<PaymentDbContext>
{
    public PaymentDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("PAYMENT_SERVICE_CONNECTION")
            ?? "Server=(localdb)\\mssqllocaldb;Database=ECommercePayments;Trusted_Connection=True;TrustServerCertificate=True;";
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new PaymentDbContext(options);
    }
}
