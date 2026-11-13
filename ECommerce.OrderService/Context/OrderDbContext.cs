using ECommerce.OrderService.Models;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.OrderService.Context;

public class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    public DbSet<ShoppingCart> Carts => Set<ShoppingCart>();

    public DbSet<ShoppingCartItem> CartItems => Set<ShoppingCartItem>();

    public DbSet<CustomerOrder> Orders => Set<CustomerOrder>();

    public DbSet<CustomerOrderItem> OrderItems => Set<CustomerOrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ShoppingCart>()
            .HasIndex(cart => cart.UserId)
            .IsUnique();

        modelBuilder.Entity<ShoppingCartItem>()
            .HasOne(item => item.Cart)
            .WithMany(cart => cart.Items)
            .HasForeignKey(item => item.CartId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ShoppingCartItem>()
            .HasIndex(item => new { item.CartId, item.ProductId })
            .IsUnique();

        modelBuilder.Entity<CustomerOrder>()
            .Property(order => order.TotalAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<CustomerOrder>()
            .Property(order => order.PaymentTransactionId)
            .HasMaxLength(100);

        modelBuilder.Entity<CustomerOrder>()
            .Property(order => order.PaymentIdempotencyKey)
            .HasMaxLength(100);

        modelBuilder.Entity<CustomerOrderItem>()
            .Property(item => item.UnitPrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<CustomerOrderItem>()
            .HasOne(item => item.Order)
            .WithMany(order => order.Items)
            .HasForeignKey(item => item.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
