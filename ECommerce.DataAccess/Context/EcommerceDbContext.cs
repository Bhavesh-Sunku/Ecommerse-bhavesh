using System;
using System.Collections.Generic;
using System.Text;

using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.DataAccess.Context;

public class EcommerceDbContext : DbContext
{
    public EcommerceDbContext(
        DbContextOptions<EcommerceDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User -> Orders
        modelBuilder.Entity<User>()
            .HasMany(u => u.Orders)
            .WithOne(o => o.User)
            .HasForeignKey(o => o.UserId);

        modelBuilder.Entity<Cart>()
            .HasOne(c => c.User)
            .WithOne()
            .HasForeignKey<Cart>(c => c.UserId);

        modelBuilder.Entity<Cart>()
            .HasIndex(c => c.UserId)
            .IsUnique();

        modelBuilder.Entity<Cart>()
            .HasMany(c => c.Items)
            .WithOne(item => item.Cart)
            .HasForeignKey(item => item.CartId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CartItem>()
            .HasOne(item => item.Product)
            .WithMany(product => product.CartItems)
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CartItem>()
            .HasIndex(item => new { item.CartId, item.ProductId })
            .IsUnique();

        // Order -> OrderItems
        modelBuilder.Entity<Order>()
            .HasMany(o => o.OrderItems)
            .WithOne(oi => oi.Order)
            .HasForeignKey(oi => oi.OrderId);

        // Product -> OrderItems
        modelBuilder.Entity<Product>()
            .HasMany(p => p.OrderItems)
            .WithOne(oi => oi.Product)
            .HasForeignKey(oi => oi.ProductId);

        // Product -> Inventory
        modelBuilder.Entity<Product>()
            .HasOne(p => p.Inventory)
            .WithOne(i => i.Product)
            .HasForeignKey<Inventory>(i => i.ProductId);

        // Order -> Payment
        modelBuilder.Entity<Order>()
            .HasOne(o => o.Payment)
            .WithOne(p => p.Order)
            .HasForeignKey<Payment>(p => p.OrderId);

        // Decimal precision
        modelBuilder.Entity<Product>()
            .Property(p => p.Price)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Order>()
            .Property(o => o.TotalAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<OrderItem>()
            .Property(oi => oi.UnitPrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Payment>()
            .Property(p => p.Amount)
            .HasPrecision(18, 2);

        // Unique Email
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // One inventory per product
        modelBuilder.Entity<Inventory>()
            .HasIndex(i => i.ProductId)
            .IsUnique();

        // Product category index
        modelBuilder.Entity<Product>()
            .HasIndex(p => p.Category);

        // Order user index
        modelBuilder.Entity<Order>()
            .HasIndex(o => o.UserId);

        // Order date index
        modelBuilder.Entity<Order>()
            .HasIndex(o => o.OrderDate);
    }
}