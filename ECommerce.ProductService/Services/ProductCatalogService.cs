using ECommerce.ProductService.Contracts;
using ECommerce.ProductService.Models;
using ECommerce.ProductService.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.ProductService.Services;

public class ProductCatalogService(
    IProductRepository productRepository,
    ILogger<ProductCatalogService> logger)
{
    public async Task<ProductPage> GetCatalogAsync(ProductQuery query)
    {
        ValidateQuery(query);
        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var sortBy = string.IsNullOrWhiteSpace(query.SortBy)
            ? "newest"
            : query.SortBy;

        IQueryable<Product> products = productRepository.Query()
            .AsNoTracking()
            .Include(product => product.Inventory)
            .Where(product => product.IsActive);

        if (!query.IncludeOutOfStock)
        {
            products = products.Where(product =>
                product.Inventory != null && product.Inventory.Quantity > 0);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            products = products.Where(product =>
                product.Name.Contains(query.Search) ||
                product.Description.Contains(query.Search));
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            products = products.Where(product => product.Category == query.Category);
        }

        if (query.MinPrice.HasValue)
        {
            products = products.Where(product => product.Price >= query.MinPrice.Value);
        }

        if (query.MaxPrice.HasValue)
        {
            products = products.Where(product => product.Price <= query.MaxPrice.Value);
        }

        var totalCount = await products.CountAsync();
        products = sortBy.ToLowerInvariant() switch
        {
            "price" => query.SortDescending
                ? products.OrderByDescending(product => product.Price)
                : products.OrderBy(product => product.Price),
            "name" => query.SortDescending
                ? products.OrderByDescending(product => product.Name)
                : products.OrderBy(product => product.Name),
            _ => query.SortDescending
                ? products.OrderByDescending(product => product.CreatedAt)
                : products.OrderBy(product => product.CreatedAt)
        };

        var results = await products
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        logger.LogDebug("Returned {Count} products on page {PageNumber}", results.Count, pageNumber);

        return new ProductPage
        {
            Products = results.Select(Map).ToList(),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<ProductResponse?> GetByIdAsync(int id)
    {
        var product = await productRepository.Query()
            .AsNoTracking()
            .Include(item => item.Inventory)
            .FirstOrDefaultAsync(item => item.Id == id);

        return product is null ? null : Map(product);
    }

    public async Task<List<string>> GetCategoriesAsync()
    {
        return await productRepository.Query()
            .Where(product =>
                product.IsActive &&
                product.Inventory != null &&
                product.Inventory.Quantity > 0)
            .Select(product => product.Category)
            .Distinct()
            .OrderBy(category => category)
            .ToListAsync();
    }

    public async Task<ProductResponse> CreateAsync(CreateProductRequest request)
    {
        var product = new Product
        {
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            Price = request.Price,
            Category = request.Category.Trim(),
            ImageUrl = request.ImageUrl.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Inventory = new Inventory
            {
                Quantity = request.StockQuantity,
                UpdatedAt = DateTime.UtcNow
            }
        };

        await productRepository.AddAsync(product);
        logger.LogInformation("Created product {ProductId}", product.Id);
        return Map(product);
    }

    public async Task<bool> UpdateAsync(int id, UpdateProductRequest request)
    {
        var product = await productRepository.GetByIdAsync(id);

        if (product is null)
        {
            return false;
        }

        product.Name = request.Name.Trim();
        product.Description = request.Description.Trim();
        product.Price = request.Price;
        product.Category = request.Category.Trim();
        product.ImageUrl = request.ImageUrl.Trim();
        product.IsActive = request.IsActive;

        if (product.Inventory is null)
        {
            product.Inventory = new Inventory
            {
                Quantity = request.StockQuantity,
                UpdatedAt = DateTime.UtcNow
            };
        }
        else
        {
            product.Inventory.Quantity = request.StockQuantity;
            product.Inventory.UpdatedAt = DateTime.UtcNow;
        }

        await productRepository.SaveChangesAsync();
        logger.LogInformation("Updated product {ProductId}", id);
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var product = await productRepository.GetByIdAsync(id);
        if (product is null)
        {
            return false;
        }

        productRepository.Delete(product);
        await productRepository.SaveChangesAsync();
        logger.LogInformation("Deleted product {ProductId}", id);
        return true;
    }

    private static void ValidateQuery(ProductQuery query)
    {
        if (query.MinPrice < 0 || query.MaxPrice < 0)
        {
            throw new ArgumentException("Price filters cannot be negative.");
        }

        if (query.MinPrice.HasValue &&
            query.MaxPrice.HasValue &&
            query.MinPrice > query.MaxPrice)
        {
            throw new ArgumentException("Minimum price cannot exceed maximum price.");
        }

        if (!string.IsNullOrWhiteSpace(query.SortBy) &&
            !new[] { "newest", "price", "name" }
                .Contains(query.SortBy, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("SortBy must be newest, price, or name.");
        }
    }

    private static ProductResponse Map(Product product)
    {
        return new ProductResponse
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            Category = product.Category,
            ImageUrl = product.ImageUrl,
            AvailableStock = product.Inventory?.Quantity ?? 0,
            IsActive = product.IsActive,
            CreatedAt = product.CreatedAt
        };
    }
}
