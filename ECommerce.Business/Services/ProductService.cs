using System;
using System.Linq;
using ECommerce.Business.DTOs.Products;
using ECommerce.Business.Interfaces;
using ECommerce.DataAccess.Repositories.Interfaces;
using ECommerce.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;

namespace ECommerce.Business.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly IMemoryCache _cache;
    private readonly IInventoryRepository _inventoryRepository;

    private const string ProductCacheVersionKey =
        "product_cache_version";

    public ProductService(
        IProductRepository productRepository,
        IInventoryRepository inventoryRepository,
        IMemoryCache cache)
    {
        _productRepository = productRepository;
        _inventoryRepository = inventoryRepository;
        _cache = cache;
    }

    // ==========================================
    // Get Product Catalog
    // ==========================================

    public async Task<ProductPagedResponseDto> GetCatalogAsync(
        ProductQueryDto query)
    {
        // -------------------------------
        // Validate pagination
        // -------------------------------

        if (query.PageNumber < 1)
        {
            query.PageNumber = 1;
        }

        if (query.PageSize < 1)
        {
            query.PageSize = 10;
        }

        if (query.PageSize > 100)
        {
            query.PageSize = 100;
        }

        // -------------------------------
        // Validate price filters
        // -------------------------------

        if (query.MinPrice.HasValue &&
            query.MinPrice.Value < 0)
        {
            throw new ArgumentException(
                "Minimum price cannot be negative.");
        }

        var sortBy = string.IsNullOrWhiteSpace(query.SortBy)
            ? "newest"
            : query.SortBy;
        if (!new[] { "newest", "price", "name" }
                .Contains(sortBy, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("SortBy must be newest, price, or name.");
        }

        if (query.MaxPrice.HasValue &&
            query.MaxPrice.Value < 0)
        {
            throw new ArgumentException(
                "Maximum price cannot be negative.");
        }

        if (query.MinPrice.HasValue &&
            query.MaxPrice.HasValue &&
            query.MinPrice.Value > query.MaxPrice.Value)
        {
            throw new ArgumentException(
                "Minimum price cannot be greater than maximum price.");
        }

        // -------------------------------
        // Create cache key
        // -------------------------------

        var version = GetProductCacheVersion();

        var cacheKey =
            $"products_{version}_" +
            $"{query.Search}_" +
            $"{query.Category}_" +
            $"{query.MinPrice}_" +
            $"{query.MaxPrice}_" +
            $"{sortBy}_" +
            $"{query.SortDescending}_" +
            $"{query.IncludeOutOfStock}_" +
            $"{query.PageNumber}_" +
            $"{query.PageSize}";

        // -------------------------------
        // Check cache
        // -------------------------------

        if (_cache.TryGetValue(
            cacheKey,
            out ProductPagedResponseDto? cachedResult))
        {
            return cachedResult!;
        }

        // -------------------------------
        // Get data from database
        // -------------------------------

        var result =
            await _productRepository.GetCatalogAsync(
                query.Search,
                query.Category,
                query.MinPrice,
                query.MaxPrice,
                sortBy,
                query.SortDescending,
                query.IncludeOutOfStock,
                query.PageNumber,
                query.PageSize);

        // -------------------------------
        // Convert entities to DTOs
        // -------------------------------

        var products = result.Products
            .Select(MapToResponseDto)
            .ToList();

        // -------------------------------
        // Calculate total pages
        // -------------------------------

        var totalPages = (int)Math.Ceiling(
            result.TotalCount /
            (double)query.PageSize);

        // -------------------------------
        // Create response
        // -------------------------------

        var response = new ProductPagedResponseDto
        {
            Products = products,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            TotalCount = result.TotalCount,
            TotalPages = totalPages
        };

        // -------------------------------
        // Store in cache
        // -------------------------------

        _cache.Set(
            cacheKey,
            response,
            TimeSpan.FromMinutes(5));

        return response;
    }

    // ==========================================
    // Get All Products
    // ==========================================

    public async Task<List<ProductResponseDto>> GetAllAsync()
    {
        var products =
            await _productRepository.GetAllAsync();

        return products
            .Select(MapToResponseDto)
            .ToList();
    }

    public Task<List<string>> GetCategoriesAsync()
    {
        return _productRepository.GetCategoriesAsync();
    }

    // ==========================================
    // Get Product By ID
    // ==========================================

    public async Task<ProductResponseDto?> GetByIdAsync(int id)
    {
        var product =
            await _productRepository.GetByIdAsync(id);

        if (product == null)
        {
            return null;
        }

        return MapToResponseDto(product);
    }

    // ==========================================
    // Create Product
    // ==========================================

    public async Task<ProductResponseDto> CreateAsync(
        CreateProductDto dto)
    {
        // Validate price
        if (dto.Price <= 0)
        {
            throw new ArgumentException(
                "Product price must be greater than zero.");
        }

        // Create entity
        var product = new Product
        {
            Name = dto.Name.Trim(),
            Description = dto.Description.Trim(),
            Price = dto.Price,
            Category = dto.Category.Trim(),
            ImageUrl = dto.ImageUrl.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Inventory = new Inventory
            {
                Quantity = dto.StockQuantity,
                UpdatedAt = DateTime.UtcNow
            }
        };

        // Save product
        var createdProduct =
            await _productRepository.AddAsync(product);

        // Invalidate catalog cache
        InvalidateProductCache();

        return MapToResponseDto(createdProduct);
    }

    // ==========================================
    // Update Product
    // ==========================================

    public async Task<bool> UpdateAsync(
        int id,
        UpdateProductDto dto)
    {
        // Find product
        var product =
            await _productRepository.GetByIdAsync(id);

        if (product == null)
        {
            return false;
        }

        // Validate price
        if (dto.Price <= 0)
        {
            throw new ArgumentException(
                "Product price must be greater than zero.");
        }

        // Update properties
        product.Name = dto.Name.Trim();
        product.Description = dto.Description.Trim();
        product.Price = dto.Price;
        product.Category = dto.Category.Trim();
        product.ImageUrl = dto.ImageUrl.Trim();
        product.IsActive = dto.IsActive;
        if (product.Inventory == null)
        {
            product.Inventory = new Inventory
            {
                ProductId = product.Id,
                Quantity = dto.StockQuantity,
                UpdatedAt = DateTime.UtcNow
            };
        }
        else
        {
            product.Inventory.Quantity = dto.StockQuantity;
            product.Inventory.UpdatedAt = DateTime.UtcNow;
        }

        // Save changes
        await _productRepository.UpdateAsync(product);

        // Invalidate catalog cache
        InvalidateProductCache();

        return true;
    }

    // ==========================================
    // Delete Product
    // ==========================================

    public async Task<bool> DeleteAsync(int id)
    {
        // Find product
        var product =
            await _productRepository.GetByIdAsync(id);

        if (product == null)
        {
            return false;
        }

        // Delete product
        await _productRepository.DeleteAsync(product);

        // Invalidate catalog cache
        InvalidateProductCache();

        return true;
    }

    // ==========================================
    // Get Cache Version
    // ==========================================

    private string GetProductCacheVersion()
    {
        var version =
            _cache.Get<string>(
                ProductCacheVersionKey);

        if (string.IsNullOrEmpty(version))
        {
            version = Guid.NewGuid().ToString();

            _cache.Set(
                ProductCacheVersionKey,
                version);
        }

        return version;
    }

    // ==========================================
    // Invalidate Product Cache
    // ==========================================

    private void InvalidateProductCache()
    {
        _cache.Set(
            ProductCacheVersionKey,
            Guid.NewGuid().ToString());
    }

    public void InvalidateCatalogCache()
    {
        InvalidateProductCache();
    }

    // ==========================================
    // Map Entity → DTO
    // ==========================================

    private static ProductResponseDto MapToResponseDto(
        Product product)
    {
        return new ProductResponseDto
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

    // ==========================================
    // Bulk Import From Excel
    // ==========================================

    public async Task<BulkImportResultDto> ImportFromExcelAsync(
        System.IO.Stream fileStream,
        string fileName)
    {
        var result = new BulkImportResultDto();

        try
        {
            using var workbook = new ClosedXML.Excel.XLWorkbook(fileStream);
            var worksheet = workbook.Worksheets.First();

            var rows = worksheet.RowsUsed().Skip(1);
            int rowNumber = 1;

            foreach (var row in rows)
            {
                rowNumber++;

                try
                {
                    var idCell = row.Cell(1).GetString();
                    var name = row.Cell(2).GetString().Trim();
                    var description = row.Cell(3).GetString().Trim();
                    var priceCell = row.Cell(4).GetDouble();
                    var quantityCell = row.Cell(5).GetDouble();
                    var category = row.Cell(6).GetString().Trim();

                    decimal price = Convert.ToDecimal(priceCell);
                    int quantity = Convert.ToInt32(quantityCell);

                    if (string.IsNullOrWhiteSpace(name) || price <= 0 || quantity < 0)
                    {
                        result.Errors.Add(new BulkImportRowErrorDto
                        {
                            RowNumber = rowNumber,
                            Error = "Invalid data: name, price or quantity"
                        });
                        continue;
                    }

                    if (int.TryParse(idCell, out int productId) && productId > 0)
                    {
                        var existing = await _productRepository.GetByIdAsync(productId);
                        if (existing == null)
                        {
                            result.Errors.Add(new BulkImportRowErrorDto
                            {
                                RowNumber = rowNumber,
                                Error = $"Product with id {productId} not found"
                            });
                            continue;
                        }

                        existing.Name = name;
                        existing.Description = description;
                        existing.Price = price;
                        existing.Category = category;

                        await _productRepository.UpdateAsync(existing);

                        var inventory = await _inventoryRepository.GetByProductIdAsync(existing.Id);
                        if (inventory == null)
                        {
                            await _inventoryRepository.AddAsync(new Domain.Entities.Inventory
                            {
                                ProductId = existing.Id,
                                Quantity = quantity,
                                UpdatedAt = DateTime.UtcNow
                            });
                        }
                        else
                        {
                            inventory.Quantity = quantity;
                            inventory.UpdatedAt = DateTime.UtcNow;
                            await _inventoryRepository.UpdateAsync(inventory);
                        }

                        result.Updated++;
                    }
                    else
                    {
                        var product = new Product
                        {
                            Name = name,
                            Description = description,
                            Price = price,
                            Category = category,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        };

                        var created = await _productRepository.AddAsync(product);

                        await _inventoryRepository.AddAsync(new Domain.Entities.Inventory
                        {
                            ProductId = created.Id,
                            Quantity = quantity,
                            UpdatedAt = DateTime.UtcNow
                        });

                        result.Created++;
                    }
                }
                catch (Exception exRow)
                {
                    result.Errors.Add(new BulkImportRowErrorDto
                    {
                        RowNumber = rowNumber,
                        Error = exRow.Message
                    });
                }
            }

            InvalidateProductCache();
        }
        catch (Exception ex)
        {
            result.Errors.Add(new BulkImportRowErrorDto
            {
                RowNumber = 0,
                Error = "Failed to process file: " + ex.Message
            });
        }

        return result;
    }
}