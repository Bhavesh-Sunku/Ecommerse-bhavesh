using ECommerce.ProductService.Models;

namespace ECommerce.ProductService.Repositories;

public interface IInventoryRepository
{
    IQueryable<Inventory> Query();

    Task<Inventory?> GetByProductIdAsync(int productId);

    Task AddAsync(Inventory inventory);

    Task<bool> ExistsForProductAsync(int productId);

    Task SaveChangesAsync();
}
