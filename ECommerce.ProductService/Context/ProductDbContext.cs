using ECommerce.ProductService.Models;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.ProductService.Context;

public class ProductDbContext(DbContextOptions<ProductDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    public DbSet<Inventory> Inventories => Set<Inventory>();

    public DbSet<InventoryReservation> InventoryReservations => Set<InventoryReservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>()
            .HasIndex(product => product.Category);

        modelBuilder.Entity<Product>()
            .Property(product => product.Price)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Product>()
            .HasOne(product => product.Inventory)
            .WithOne(inventory => inventory.Product)
            .HasForeignKey<Inventory>(inventory => inventory.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Inventory>()
            .HasIndex(inventory => inventory.ProductId)
            .IsUnique();

        modelBuilder.Entity<InventoryReservation>()
            .HasIndex(reservation => new
            {
                reservation.ReservationId,
                reservation.ProductId
            })
            .IsUnique();
    }
}
