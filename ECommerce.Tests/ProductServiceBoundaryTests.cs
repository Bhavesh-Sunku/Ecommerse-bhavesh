using ECommerce.ProductService.Context;
using ECommerce.ProductService.Contracts;
using ECommerce.ProductService.Repositories;
using ECommerce.ProductService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ECommerce.Tests;

public class ProductServiceBoundaryTests
{
    [Fact]
    public async Task CreateAndCatalogAsync_PersistsProductAndInitialInventory()
    {
        await using var context = CreateContext();
        var service = new ProductCatalogService(
            new ProductServiceProductRepository(context),
            NullLogger<ProductCatalogService>.Instance);

        var created = await service.CreateAsync(new CreateProductRequest
        {
            Name = "Trail Shoes",
            Description = "Running shoes",
            Price = 79.99m,
            Category = "Footwear",
            ImageUrl = "trail-shoes.png",
            StockQuantity = 4
        });

        var result = await service.GetCatalogAsync(new ProductQuery
        {
            Category = "Footwear"
        });

        Assert.Equal(created.Id, Assert.Single(result.Products).Id);
        Assert.Equal(4, result.Products[0].AvailableStock);
        Assert.Equal(79.99m, result.Products[0].Price);
    }

    [Fact]
    public async Task GetCatalogAsync_RejectsInvalidPriceRange()
    {
        await using var context = CreateContext();
        var service = new ProductCatalogService(
            new ProductServiceProductRepository(context),
            NullLogger<ProductCatalogService>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetCatalogAsync(new ProductQuery
            {
                MinPrice = 100,
                MaxPrice = 20
            }));
    }

    private static ProductDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ProductDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ProductDbContext(options);
    }
}
