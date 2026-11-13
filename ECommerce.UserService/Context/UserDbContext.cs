using ECommerce.UserService.Models;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.UserService.Context;

public class UserDbContext(DbContextOptions<UserDbContext> options) : DbContext(options)
{
    public DbSet<UserAccount> Users => Set<UserAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Email).HasMaxLength(450).IsRequired();
            entity.HasIndex(user => user.Email).IsUnique();
            entity.Property(user => user.FirstName).IsRequired();
            entity.Property(user => user.LastName).IsRequired();
            entity.Property(user => user.PasswordHash).IsRequired();
            entity.Property(user => user.Role).IsRequired();
        });
    }
}
