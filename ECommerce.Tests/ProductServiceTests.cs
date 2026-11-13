using System;
using System.Collections.Generic;
using System.Text;
using ECommerce.Business.Services;
using ECommerce.Business.DTOs.Products;
using LegacyProductService = ECommerce.Business.Services.ProductService;
using ECommerce.DataAccess.Repositories.Interfaces;
using ECommerce.Domain.Entities;
using Moq;
using Microsoft.Extensions.Caching.Memory; // added

namespace ECommerce.Tests;

public class ProductServiceTests
{
    [Fact]
    public async Task GetByIdAsync_ProductExists_ReturnsProduct()
    {
        // Arrange
        var product = new Product
        {
            Id = 1,
            Name = "Laptop",
            Description = "Test laptop",
            Price = 50000,
            Category = "Electronics",
            IsActive = true
        };

        var repository = new Mock<IProductRepository>();

        repository
            .Setup(x => x.GetByIdAsync(1))
            .ReturnsAsync(product);

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var inventoryRepo = new Mock<IInventoryRepository>();
        var service =
            new LegacyProductService(repository.Object, inventoryRepo.Object, memoryCache);

        // Act
        var result =
            await service.GetByIdAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Laptop", result.Name);
        Assert.Equal(50000, result.Price);
    }

    [Fact]
    public async Task CreateAsync_TrimsFieldsAndCreatesInitialInventory()
    {
        var repository = new Mock<IProductRepository>();
        repository
            .Setup(x => x.AddAsync(It.IsAny<Product>()))
            .ReturnsAsync((Product product) =>
            {
                product.Id = 9;
                return product;
            });

        var service = CreateService(repository);

        var result = await service.CreateAsync(new CreateProductDto
        {
            Name = "  Keyboard  ",
            Description = "  Mechanical  ",
            Price = 49.99m,
            Category = "  Electronics  ",
            ImageUrl = "  keyboard.png  ",
            StockQuantity = 12
        });

        Assert.Equal(9, result.Id);
        Assert.Equal("Keyboard", result.Name);
        Assert.Equal("Mechanical", result.Description);
        Assert.Equal("Electronics", result.Category);
        Assert.Equal("keyboard.png", result.ImageUrl);
        Assert.Equal(12, result.AvailableStock);
        repository.Verify(x => x.AddAsync(It.Is<Product>(product =>
            product.Inventory != null &&
            product.Inventory.Quantity == 12 &&
            product.IsActive)), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_RejectsNonPositivePrice()
    {
        var repository = new Mock<IProductRepository>();
        var service = CreateService(repository);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(new CreateProductDto
            {
                Name = "Keyboard",
                Category = "Electronics",
                Price = 0
            }));

        repository.Verify(
            x => x.AddAsync(It.IsAny<Product>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesProductAndInventory()
    {
        var product = new Product
        {
            Id = 9,
            Name = "Old name",
            Price = 10,
            Category = "Old category",
            Inventory = new Inventory { ProductId = 9, Quantity = 2 }
        };
        var repository = new Mock<IProductRepository>();
        repository
            .Setup(x => x.GetByIdAsync(9))
            .ReturnsAsync(product);

        var service = CreateService(repository);
        var updated = await service.UpdateAsync(9, new UpdateProductDto
        {
            Name = "  New name ",
            Description = " Updated ",
            Price = 25,
            Category = " New category ",
            ImageUrl = " new.png ",
            StockQuantity = 7,
            IsActive = true
        });

        Assert.True(updated);
        Assert.Equal("New name", product.Name);
        Assert.Equal("Updated", product.Description);
        Assert.Equal(25, product.Price);
        Assert.Equal("New category", product.Category);
        Assert.Equal("new.png", product.ImageUrl);
        Assert.Equal(7, product.Inventory.Quantity);
        repository.Verify(x => x.UpdateAsync(product), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_CreatesInventoryWhenProductHasNone()
    {
        var product = new Product { Id = 9, Name = "Keyboard" };
        var repository = new Mock<IProductRepository>();
        repository
            .Setup(x => x.GetByIdAsync(9))
            .ReturnsAsync(product);

        var service = CreateService(repository);
        var updated = await service.UpdateAsync(9, new UpdateProductDto
        {
            Name = "Keyboard",
            Price = 10,
            Category = "Electronics",
            StockQuantity = 0
        });

        Assert.True(updated);
        Assert.NotNull(product.Inventory);
        Assert.Equal(9, product.Inventory.ProductId);
        Assert.Equal(0, product.Inventory.Quantity);
        repository.Verify(x => x.UpdateAsync(product), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsFalseWhenProductDoesNotExist()
    {
        var repository = new Mock<IProductRepository>();
        repository
            .Setup(x => x.GetByIdAsync(9))
            .ReturnsAsync((Product?)null);

        var service = CreateService(repository);
        var updated = await service.UpdateAsync(9, new UpdateProductDto());

        Assert.False(updated);
        repository.Verify(
            x => x.UpdateAsync(It.IsAny<Product>()),
            Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_DeletesExistingProduct()
    {
        var product = new Product { Id = 9, Name = "Keyboard" };
        var repository = new Mock<IProductRepository>();
        repository
            .Setup(x => x.GetByIdAsync(9))
            .ReturnsAsync(product);

        var service = CreateService(repository);
        var deleted = await service.DeleteAsync(9);

        Assert.True(deleted);
        repository.Verify(x => x.DeleteAsync(product), Times.Once);
    }

    [Fact]
    public async Task GetCatalogAsync_RejectsInvalidPriceRange()
    {
        var repository = new Mock<IProductRepository>();
        var service = CreateService(repository);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetCatalogAsync(new ProductQueryDto
            {
                MinPrice = 20,
                MaxPrice = 10
            }));

        repository.Verify(
            x => x.GetCatalogAsync(
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<decimal?>(),
                It.IsAny<decimal?>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task GetCatalogAsync_PassesFiltersAndReturnsRequestedPage()
    {
        var repository = new Mock<IProductRepository>();
        repository
            .Setup(x => x.GetCatalogAsync(
                "trail",
                "Footwear",
                25,
                150,
                "price",
                false,
                false,
                2,
                5))
            .ReturnsAsync((
                new List<Product>
                {
                    new()
                    {
                        Id = 7,
                        Name = "Trail Shoes",
                        Price = 79.99m,
                        Category = "Footwear",
                        IsActive = true,
                        Inventory = new Inventory { Quantity = 4 }
                    }
                },
                6));

        var result = await CreateService(repository).GetCatalogAsync(new ProductQueryDto
        {
            Search = "trail",
            Category = "Footwear",
            MinPrice = 25,
            MaxPrice = 150,
            SortBy = "price",
            SortDescending = false,
            IncludeOutOfStock = false,
            PageNumber = 2,
            PageSize = 5
        });

        Assert.Equal(2, result.PageNumber);
        Assert.Equal(5, result.PageSize);
        Assert.Equal(6, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal("Trail Shoes", Assert.Single(result.Products).Name);
        repository.VerifyAll();
    }

    private static LegacyProductService CreateService(
        Mock<IProductRepository> repository)
    {
        return new LegacyProductService(
            repository.Object,
            new Mock<IInventoryRepository>().Object,
            new MemoryCache(new MemoryCacheOptions()));
    }
}
