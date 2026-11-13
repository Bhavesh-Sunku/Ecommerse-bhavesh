using ECommerce.ProductService.Contracts;
using ECommerce.ProductService.Models;
using ECommerce.ProductService.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.ProductService.Services;

public class InventoryManagementService(
    IProductRepository productRepository,
    IInventoryRepository inventoryRepository,
    ILogger<InventoryManagementService> logger)
{
    public async Task<InventoryResponse?> GetByProductIdAsync(int productId)
    {
        var inventory = await inventoryRepository.Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.ProductId == productId);

        return inventory is null ? null : Map(inventory);
    }

    public async Task<InventoryResponse> CreateAsync(CreateInventoryRequest request)
    {
        if (!await productRepository.ExistsAsync(request.ProductId))
        {
            throw new ArgumentException("Product does not exist.");
        }

        if (await inventoryRepository.ExistsForProductAsync(request.ProductId))
        {
            throw new InvalidOperationException("Inventory already exists for this product.");
        }

        var inventory = new Inventory
        {
            ProductId = request.ProductId,
            Quantity = request.Quantity,
            UpdatedAt = DateTime.UtcNow
        };
        await inventoryRepository.AddAsync(inventory);
        logger.LogInformation("Created inventory for product {ProductId}", request.ProductId);
        return Map(inventory);
    }

    public async Task<bool> UpdateAsync(int productId, UpdateInventoryRequest request)
    {
        var inventory = await inventoryRepository.GetByProductIdAsync(productId);

        if (inventory is null)
        {
            return false;
        }

        inventory.Quantity = request.Quantity;
        inventory.UpdatedAt = DateTime.UtcNow;
        await inventoryRepository.SaveChangesAsync();
        logger.LogInformation("Updated inventory for product {ProductId}", productId);
        return true;
    }

    private static InventoryResponse Map(Inventory inventory)
    {
        return new InventoryResponse
        {
            Id = inventory.Id,
            ProductId = inventory.ProductId,
            Quantity = inventory.Quantity,
            UpdatedAt = inventory.UpdatedAt
        };
    }
}
