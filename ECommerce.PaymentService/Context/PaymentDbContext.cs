using ECommerce.Payments.Models;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Payments.Context;

public class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<PaymentRecord> Payments => Set<PaymentRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PaymentRecord>(entity =>
        {
            entity.Property(payment => payment.Amount).HasPrecision(18, 2);
            entity.Property(payment => payment.Status).HasMaxLength(20).IsRequired();
            entity.Property(payment => payment.PaymentMethod).HasMaxLength(20).IsRequired();
            entity.Property(payment => payment.IdempotencyKey).HasMaxLength(100).IsRequired();
            entity.Property(payment => payment.TransactionId).HasMaxLength(100);
            entity.HasIndex(payment => payment.IdempotencyKey).IsUnique();
            entity.HasIndex(payment => payment.OrderId);
            entity.HasIndex(payment => payment.TransactionId)
                .IsUnique()
                .HasFilter("[TransactionId] IS NOT NULL");
        });
    }
}
