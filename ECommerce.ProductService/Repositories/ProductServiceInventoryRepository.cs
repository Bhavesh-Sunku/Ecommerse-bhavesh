using ECommerce.ProductService.Context;
using ECommerce.ProductService.Models;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.ProductService.Repositories;

public class ProductServiceInventoryRepository(ProductDbContext context) : IInventoryRepository
{
    public IQueryable<Inventory> Query()
    {
        return context.Inventories.AsQueryable();
    }

    public Task<Inventory?> GetByProductIdAsync(int productId)
    {
        return context.Inventories
            .FirstOrDefaultAsync(inventory => inventory.ProductId == productId);
    }

    public async Task AddAsync(Inventory inventory)
    {
        context.Inventories.Add(inventory);
        await context.SaveChangesAsync();
    }

    public Task<bool> ExistsForProductAsync(int productId)
    {
        return context.Inventories.AnyAsync(inventory => inventory.ProductId == productId);
    }

    public Task SaveChangesAsync()
    {
        return context.SaveChangesAsync();
    }
}
