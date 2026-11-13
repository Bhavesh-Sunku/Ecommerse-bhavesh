using System;
using System.Collections.Generic;
using System.Text;

using ECommerce.DataAccess.Context;
using ECommerce.DataAccess.Repositories.Interfaces;
using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.DataAccess.Repositories.Implementations;

public class ProductRepository : IProductRepository
{
    private readonly EcommerceDbContext _context;

    public ProductRepository(EcommerceDbContext context)
    {
        _context = context;
    }

    // Get all products
    public async Task<List<Product>> GetAllAsync()
    {
        return await _context.Products
            .AsNoTracking()
            .Include(product => product.Inventory)
            .ToListAsync();
    }

    public Task<List<string>> GetCategoriesAsync()
    {
        return _context.Products
            .Where(product =>
                product.IsActive &&
                product.Inventory != null &&
                product.Inventory.Quantity > 0)
            .Select(product => product.Category)
            .Distinct()
            .OrderBy(category => category)
            .ToListAsync();
    }

    // Get product by ID
    public async Task<Product?> GetByIdAsync(int id)
    {
        return await _context.Products
            .AsNoTracking()
            .Include(product => product.Inventory)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    // Add a new product
    public async Task<Product> AddAsync(Product product)
    {
        _context.Products.Add(product);

        await _context.SaveChangesAsync();

        return product;
    }

    // Update an existing product
    public async Task UpdateAsync(Product product)
    {
        _context.Products.Update(product);

        await _context.SaveChangesAsync();
    }

    // Delete a product
    public async Task DeleteAsync(Product product)
    {
        _context.Products.Remove(product);

        await _context.SaveChangesAsync();
    }

    // Get products for catalog with
    // search, category, price filtering and pagination
    public async Task<(List<Product> Products, int TotalCount)> GetCatalogAsync(
        string? search,
        string? category,
        decimal? minPrice,
        decimal? maxPrice,
        string sortBy,
        bool sortDescending,
        bool includeOutOfStock,
        int pageNumber,
        int pageSize)
    {
        // Start with active products only
        var query = _context.Products
            .AsNoTracking()
            .Include(product => product.Inventory)
            .Where(p => p.IsActive)
            .AsQueryable();

        if (!includeOutOfStock)
        {
            query = query.Where(product =>
                product.Inventory != null &&
                product.Inventory.Quantity > 0);
        }

        // Search by product name or description
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p =>
                p.Name.Contains(search) ||
                p.Description.Contains(search));
        }

        // Filter by category
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(p =>
                p.Category == category);
        }

        // Filter by minimum price
        if (minPrice.HasValue)
        {
            query = query.Where(p =>
                p.Price >= minPrice.Value);
        }

        // Filter by maximum price
        if (maxPrice.HasValue)
        {
            query = query.Where(p =>
                p.Price <= maxPrice.Value);
        }

        // Count products before pagination
        var totalCount = await query.CountAsync();

        // Apply sorting and pagination
        var sortedQuery = sortBy.ToLowerInvariant() switch
        {
            "price" => sortDescending
                ? query.OrderByDescending(product => product.Price)
                : query.OrderBy(product => product.Price),
            "name" => sortDescending
                ? query.OrderByDescending(product => product.Name)
                : query.OrderBy(product => product.Name),
            _ => sortDescending
                ? query.OrderByDescending(product => product.CreatedAt)
                : query.OrderBy(product => product.CreatedAt)
        };

        var products = await sortedQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (products, totalCount);
    }
}