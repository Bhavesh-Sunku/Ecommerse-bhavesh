using System;
using System.Collections.Generic;
using System.Text;

using ECommerce.Domain.Entities;

namespace ECommerce.DataAccess.Repositories.Interfaces;

public interface IProductRepository
{
    Task<List<Product>> GetAllAsync();

    Task<List<string>> GetCategoriesAsync();

    Task<Product?> GetByIdAsync(int id);

    Task<Product> AddAsync(Product product);

    Task UpdateAsync(Product product);

    Task DeleteAsync(Product product);

    Task<(List<Product> Products, int TotalCount)> GetCatalogAsync(
        string? search,
        string? category,
        decimal? minPrice,
        decimal? maxPrice,
        string sortBy,
        bool sortDescending,
        bool includeOutOfStock,
        int pageNumber,
        int pageSize);
}