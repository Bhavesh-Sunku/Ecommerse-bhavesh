using ECommerce.ProductService.Models;

namespace ECommerce.ProductService.Repositories;

public interface IProductRepository
{
    IQueryable<Product> Query();

    Task<Product?> GetByIdAsync(int id);

    Task<bool> ExistsAsync(int id);

    Task AddAsync(Product product);

    Task SaveChangesAsync();

    void Delete(Product product);
}
