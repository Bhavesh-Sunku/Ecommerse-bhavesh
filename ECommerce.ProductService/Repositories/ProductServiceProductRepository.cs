using ECommerce.ProductService.Context;
using ECommerce.ProductService.Models;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.ProductService.Repositories;

public class ProductServiceProductRepository(ProductDbContext context) : IProductRepository
{
    public IQueryable<Product> Query()
    {
        return context.Products.AsQueryable();
    }

    public Task<Product?> GetByIdAsync(int id)
    {
        return context.Products
            .Include(product => product.Inventory)
            .FirstOrDefaultAsync(product => product.Id == id);
    }

    public Task<bool> ExistsAsync(int id)
    {
        return context.Products.AnyAsync(product => product.Id == id);
    }

    public async Task AddAsync(Product product)
    {
        context.Products.Add(product);
        await context.SaveChangesAsync();
    }

    public Task SaveChangesAsync()
    {
        return context.SaveChangesAsync();
    }

    public void Delete(Product product)
    {
        context.Products.Remove(product);
    }
}
